using Godot;

namespace OpenWC3.Game;

/// <summary>Roar（Aroa）：范围内目标获得攻击、护甲和生命/法力回复 Buff。</summary>
public sealed class RuntimeRoarAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority=>AbilityCastPriority.Support;
    public float DamageIncreaseFactor { get; private set; }
    public float ArmorBonus { get; private set; }
    public float LifeRegeneration { get; private set; }
    public float ManaRegeneration { get; private set; }
    public bool AllowsRecipientsWithoutAttack { get; private set; }
    public int TargetLimit { get; private set; }
    public bool PreferSharedSpellRecipients { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string RoarBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;
    public float Radius=>Math.Max(0,ConfiguredAreaOfEffect);

    public void RefreshRoarData()
    {
        int level=Math.Max(1,Level);
        DamageIncreaseFactor=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0);
        ArmorBonus=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataB",level,0);
        LifeRegeneration=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataC",level,0);
        ManaRegeneration=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataD",level,0);
        AllowsRecipientsWithoutAttack=ArmorBonus!=0
            ||LifeRegeneration!=0;
        TargetLimit=(int)RuntimeAbilityFactory.ReadFloat(ObjectData,"DataG",level,0);
        PreferSharedSpellRecipients=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataF",level,0)!=0;
        NormalDuration=RuntimeAbilityFactory.ReadFloat(ObjectData,"Dur",level,0);
        HeroDuration=RuntimeAbilityFactory.ReadFloat(ObjectData,"HeroDur",level,NormalDuration);
        RoarBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(RoarBuffRawcode.Length>4)RoarBuffRawcode=RoarBuffRawcode[..4];
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self|TargetFlags.Alive
                |TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshRoarData();
    public override void OnLevelDown()=>RefreshRoarData();
    public override bool OnCast(World world,Unit target,Vector3 point)
    {
        if(Owner?.World==null)return false;
        if(ObjectData==null&&Type!=null)
            BindObjectData(Type.Id,new AbilityTypeObjectData(Type),Math.Max(1,Level));
        RefreshRoarData();
        OnEffect(target,new Vec2(point.X,point.Z));
        return true;
    }
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null
        &&(TargetLimit==0||EligibleRecipients().Any());
    public override bool TryAutoCast(World world)
    {
        if(!HasEnemyNearby(world,Radius))return false;
        return world.TryCast(Owner,this,null,Owner.Position);
    }

    IEnumerable<Unit> EligibleRecipients()
    {
        if(Owner?.World==null)return Array.Empty<Unit>();
        float radius=Math.Max(0,ConfiguredAreaOfEffect);
        IEnumerable<Unit> units=radius<=0?new[]{Owner}:RuntimeSpatialUnitQuery.Around(Owner.World,Owner.Pos,radius);
        return units
            .Where(unit=>unit.Alive&&!unit.Removed
                &&unit.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true)
                &&(HasAttack(unit)||AllowsRecipientsWithoutAttack)
                &&Vec2.Dist(Owner.Pos,unit.Pos)<=radius)
            .OrderBy(unit=>unit.SimulationId);
    }

    public override void OnEffect(Unit target,Vec2 point)
    {
        World world=Owner?.World;
        if(world==null)return;
        IEnumerable<Unit> candidates=EligibleRecipients();
        if(TargetLimit!=0)
        {
            candidates=candidates.OrderByDescending(unit=>PreferSharedSpellRecipients
                    &&world.SharedSpellsQuery(Owner,unit))
                .ThenBy(DistanceScore).ThenBy(RemainingScore)
                .ThenByDescending(unit=>unit.SimulationId);
            if(TargetLimit>0)candidates=candidates.Take(TargetLimit);
        }
        foreach(Unit candidate in candidates.ToArray())
        {
            if(!candidate.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true))continue;
            if(!HasAttack(candidate)&&!AllowsRecipientsWithoutAttack)continue;
            float duration=candidate.UsesHeroSpellRules?HeroDuration:NormalDuration;
            RuntimeRoarBuff existing=candidate.Abilities.OfType<RuntimeRoarBuff>()
                .FirstOrDefault(buff=>buff.Rawcode.Equals(RoarBuffRawcode,StringComparison.OrdinalIgnoreCase));
            if(existing!=null)
            {
                existing.RefreshRoar(DamageIncreaseFactor,duration,Owner,this,ArmorBonus,LifeRegeneration,ManaRegeneration);
                continue;
            }
            var buff=new RuntimeRoarBuff(DamageIncreaseFactor,ArmorBonus,LifeRegeneration,ManaRegeneration);
            BindRoarBuff(buff);
            buff.Caster=Owner;buff.SourceAbility=this;
            buff.Duration=duration;
            buff.Permanent=buff.Duration<=0;
            candidate.AddAbility(buff);
        }
    }

    void BindRoarBuff(RuntimeRoarBuff buff)
    {
        int level=Math.Max(1,Level);
        BuffTypeData data=objects?.GetBuff(RoarBuffRawcode);
        if(data!=null)buff.BindBuffObjectData(RoarBuffRawcode,data,level,Owner,this);
        else buff.BindObjectData(RoarBuffRawcode,ObjectData,level);
    }

    static bool HasAttack(Unit unit)=>unit?.Type?.CanAttack==true;

    float DistanceScore(Unit unit)
    {
        Vec2 delta=unit.Pos-Owner.Pos;
        return delta.X*delta.X+delta.Y*delta.Y;
    }

    float RemainingScore(Unit unit)
    {
        var buff=unit.Abilities.OfType<RuntimeRoarBuff>()
            .FirstOrDefault(buff=>buff.Rawcode.Equals(RoarBuffRawcode,StringComparison.OrdinalIgnoreCase));
        return buff==null?0:buff.Permanent?-1:buff.RemainingSeconds;
    }
}

public sealed class RuntimeRoarBuff(float damageIncreaseFactor,float armorBonus=0,
    float lifeRegeneration=0,float manaRegeneration=0) : Buff
{
    float _appliedArmor;
    float _appliedLife,_appliedMana;

    public override bool Positive=>true;
    public override bool IsHarmful=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Roar")??"Roar";
    public float DamageIncreaseFactor { get; private set; }=damageIncreaseFactor;
    public float ArmorBonus { get; private set; }=armorBonus;
    public float LifeRegeneration { get; private set; }=lifeRegeneration;
    public float ManaRegeneration { get; private set; }=manaRegeneration;

    public void RefreshRoar(float factor,float duration,Unit caster,Ability source,float armor=0,float life=0,float mana=0)
    {
        RemoveRegeneration();RemoveArmorBonus();RemoveDamageBonus();
        DamageIncreaseFactor=factor;ArmorBonus=armor;LifeRegeneration=life;ManaRegeneration=mana;Caster=caster;SourceAbility=source;
        ApplyDamageBonus();
        ApplyArmorBonus();
        ApplyRegeneration();
        if(duration<=0)
        {
            Duration=duration;Permanent=true;RefreshExpiration();return;
        }
        float remaining=Permanent?0:RemainingSeconds;
        Permanent=false;
        if(duration>remaining){Duration=duration;RefreshExpiration();}
    }

    protected override void OnBuffAdd(){ApplyDamageBonus();ApplyArmorBonus();ApplyRegeneration();}
    protected override void OnBuffRemove(){RemoveRegeneration();RemoveArmorBonus();RemoveDamageBonus();}

    public override bool OnTick(World world,float delta)=>Permanent||base.OnTick(world,delta);

    void ApplyDamageBonus()
    {
        if(Owner!=null)Owner.BonusDamagePct+=DamageIncreaseFactor;
    }
    void RemoveDamageBonus()
    {
        if(Owner!=null)Owner.BonusDamagePct-=DamageIncreaseFactor;
    }
    void ApplyRegeneration()
    {
        if(Owner==null)return;
        var config=Owner.World?.GameplayConfig??War3GameplayConfig.Default;
        if(Math.Abs(LifeRegeneration)>=.001f&&(!Owner.IsIllusion||config.IllusionsCanRestoreLife))
        {
            _appliedLife=LifeRegeneration;
            Owner.AlwaysLifeRegenBonus+=_appliedLife;
        }
        if(Math.Abs(ManaRegeneration)>=.001f&&(!Owner.IsIllusion||config.IllusionsCanRestoreMana))
        {
            _appliedMana=ManaRegeneration;
            Owner.ManaRegen+=_appliedMana;
        }
    }
    void RemoveRegeneration()
    {
        if(Owner!=null)
        {
            Owner.AlwaysLifeRegenBonus-=_appliedLife;
            Owner.ManaRegen-=_appliedMana;
        }
        _appliedLife=_appliedMana=0;
    }
    void ApplyArmorBonus()
    {
        if(Owner==null||Owner.IsIllusion
            &&!(Owner.World?.GameplayConfig??War3GameplayConfig.Default).IllusionsGetDefenseBonus)return;
        _appliedArmor=ArmorBonus;
        Owner.BonusArmor+=_appliedArmor;
    }
    void RemoveArmorBonus()
    {
        if(Owner!=null)Owner.BonusArmor-=_appliedArmor;
        _appliedArmor=0;
    }
}