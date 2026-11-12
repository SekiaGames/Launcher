namespace OpenWC3.Game;

/// <summary>Amtc stores dead units independently of living transport passengers.</summary>
public sealed class RuntimeMeatCargoAbility : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    readonly List<(Unit unit,bool hidden)> _corpses=new();
    public override bool IsPassive=>true;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public int Capacity=>Math.Max(0,RuntimeAbilityFactory.ReadInt(ObjectData,"DataA",Math.Max(1,Level),0));
    public IReadOnlyList<Unit> Corpses=>_corpses.Where(c=>!c.unit.Removed&&c.unit.Death.IsCorpse
        &&c.unit.CorpseCarrier==Owner).Select(c=>c.unit).ToArray();
    public int CorpseCount
    {
        get
        {
            int count=0;
            foreach(var entry in _corpses)
                if(!entry.unit.Removed&&entry.unit.Death.IsCorpse&&entry.unit.CorpseCarrier==Owner)count++;
            return count;
        }
    }
    public bool CanStore(Unit corpse)=>Owner?.Alive==true
        &&corpse!=null&&corpse.World==Owner.World&&!corpse.Removed&&!corpse.Alive&&corpse.IsCorpse&&!corpse.IsHero
        &&!corpse.IsBuilding&&!corpse.Is(Classification.Mechanical)&&corpse.MovementType!=MovementType.Fly
        &&CorpseCount<Capacity;
    public bool Store(Unit corpse)
    {
        if(!CanStore(corpse))return false;
        // 6129e0 -> 582330(param_3=0): an already-dead passenger still
        // receives 6568b0/d0144 before attachment. Retire movement independently
        // of the public order head, just as at the original death boundary.
        corpse.CancelOrders();
        corpse.World.ReleaseUnitSpatialOnDeath(corpse);
        _corpses.Add((corpse,corpse.Hidden));corpse.CorpseCarrier=Owner;
        corpse.Hidden=true;corpse.Death.SuspendDecay(true);corpse.Pos=Owner.Pos;return true;
    }
    public void DropAll()
    {
        int index=0;
        foreach(var entry in _corpses.ToArray())
        {
            Unit corpse=entry.unit;
            if(corpse.CorpseCarrier!=Owner)continue;
            corpse.CorpseCarrier=null;
            if(corpse.Removed)continue;
            corpse.Hidden=entry.hidden;corpse.Death.SuspendDecay(false);
            float angle=index++*2.399963f;
            corpse.Pos=Owner.Pos+new Vec2(MathF.Cos(angle),MathF.Sin(angle))*48;
            // Cargo is hidden, so its presentation peer may still be at the
            // pickup point. Unloading places it immediately, even below the
            // renderer's 64-unit large-displacement threshold.
            corpse.TeleportVersion++;
        }
        _corpses.Clear();
    }
    public bool ReleaseForRevival(Unit corpse)
    {
        int index=_corpses.FindIndex(entry=>ReferenceEquals(entry.unit,corpse)&&corpse.CorpseCarrier==Owner);
        if(index<0||corpse.Removed||Owner?.Alive!=true)return false;
        var entry=_corpses[index];_corpses.RemoveAt(index);
        corpse.CorpseCarrier=null;corpse.Hidden=entry.hidden;corpse.Death.SuspendDecay(false);
        corpse.Pos=Owner.Pos;corpse.TeleportVersion++;return true;
    }
    public override void OnTick(float dt)
    {
        base.OnTick(dt);
        for(int index=_corpses.Count-1;index>=0;index--)
        {
            var c=_corpses[index];
            if(c.unit.CorpseCarrier==Owner&&(c.unit.Removed||c.unit.Alive||!c.unit.Death.IsCorpse))
            {
                c.unit.CorpseCarrier=null;
                if(!c.unit.Removed){c.unit.Hidden=c.hidden;c.unit.Death.SuspendDecay(false);}
            }
            if(c.unit.Removed||c.unit.CorpseCarrier!=Owner||!c.unit.Death.IsCorpse)_corpses.RemoveAt(index);
        }
        foreach(var c in _corpses)c.unit.Pos=Owner.Pos;
    }
    public override void OnDeath(){base.OnDeath();DropAll();}
    public override void OnRemove(){DropAll();base.OnRemove();}
    public static bool Accessible(Unit corpse,Unit user)=>corpse!=null&&!corpse.Removed&&!corpse.Alive
        &&corpse.World==user?.World
        &&corpse.Death.IsCorpse&&(corpse.CorpseCarrier==null
            ||user.World.AreAllied(user,corpse.CorpseCarrier));
}

public sealed class RuntimeMeatLoadAbility : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    RuntimeMeatCargoAbility Hold=>Owner?.Abilities.Get<RuntimeMeatCargoAbility>();
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    public override bool SupportsAutoCast=>true;
    public override bool AutoCastPreemptsOrders=>false;
    public Unit FindCorpse()
    {
        World world=Owner?.World;
        var hold=Hold;
        if(world==null||hold==null||hold.CorpseCount>=hold.Capacity)return null;
        var candidates=world.RentUnitList();
        Unit best=null;float bestDistance=float.PositiveInfinity;
        try
        {
            world.CollectNearbyUnits(Owner.Pos,ConfiguredAreaOfEffect,candidates);
            foreach(Unit unit in candidates)
            {
                float distance=Vec2.Dist(Owner.Pos,unit.Pos);
                if(distance>ConfiguredAreaOfEffect||!hold.CanStore(unit)||!CheckConfiguredTarget(unit))continue;
                if(best!=null&&(distance>bestDistance||distance==bestDistance&&unit.SimulationId>=best.SimulationId))continue;
                best=unit;bestDistance=distance;
            }
            return best;
        }
        finally {world.ReturnUnitList(candidates);}
    }
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null&&FindCorpse()!=null;
    public override Unit ResolveCastApproachTarget(Unit target,Vec2 point)=>FindCorpse();
    public override bool TryAcquireAutoCastTarget(out Unit target,out Vec2 point)
    {target=null;point=default;return !IsDisabled&&Owner?.Alive==true&&FindCorpse()!=null;}
    public override void OnEffect(Unit target,Vec2 point)
    {
        Unit corpse=FindCorpse();
        if(corpse!=null&&Owner.DistanceTo(corpse)<=CastRange+.01f)Hold.Store(corpse);
    }
}

public sealed class RuntimeMeatDropAbility : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    public void RefreshMeatDropData()=>OrderId="unloadallcorpses";
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null
        &&Owner?.Abilities.OfType<RuntimeMeatCargoAbility>().FirstOrDefault()?.Corpses.Count>0;
    public override void OnEffect(Unit target,Vec2 point)
    {
        var loader=Owner?.Abilities.OfType<RuntimeMeatLoadAbility>().FirstOrDefault();
        if(loader!=null)loader.AutoCastActive=false;
        Owner?.Abilities.OfType<RuntimeMeatCargoAbility>().FirstOrDefault()?.DropAll();
    }
}
