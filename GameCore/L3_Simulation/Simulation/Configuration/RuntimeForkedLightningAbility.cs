namespace OpenWC3.Game;

/// <summary>CAbilityForkedLightning（ANfl）：以主目标方向的扇形选择 DataB 个目标。</summary>
public sealed class RuntimeForkedLightningAbility : Ability
{
    protected override bool SpawnTargetArtsOnCast=>false;
    TargetFlags _allowedTargets;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    public float Damage { get; private set; }
    public int TargetCount { get; private set; }
    public float MaximumDistance { get; private set; }
    public float FinalArea { get; private set; }

    public void RefreshForkedLightningData()
    {
        int level=Math.Max(1,Level);
        Damage=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        TargetCount=(int)RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,1);
        MaximumDistance=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,CastRange));
        FinalArea=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataD",level,ConfiguredAreaOfEffect));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Enemy|TargetFlags.Alive|TargetFlags.Vulnerable;
    }
    public override void OnLevelUp()=>RefreshForkedLightningData();
    public override void OnLevelDown()=>RefreshForkedLightningData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target?.Alive==true&&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    public override void OnEffect(Unit target,Vec2 point)
    {
        World world=Owner?.World;
        if(world==null||target?.Alive!=true)return;
        Vec2 direction=(target.Pos-Owner.Pos).Normalized;
        float length=MaximumDistance>0?MaximumDistance:
            Vec2.Dist(Owner.Pos,target.Pos);
        float initialArea=Math.Max(0,ConfiguredAreaOfEffect);
        var candidates=new List<Unit>();
        if(CheckCanTarget(target,point)&&!target.Removed)candidates.Add(target);
        candidates.AddRange(RuntimeSpatialUnitQuery.Around(world,Owner.Pos,Math.Max(0,length)+Math.Max(initialArea,Math.Max(0,FinalArea))).Where(candidate=>
        {
            if(ReferenceEquals(candidate,target)||!candidate.Alive
                ||candidate.Removed
                ||!candidate.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true))return false;
            Vec2 offset=candidate.Pos-Owner.Pos;
            float projection=offset.X*direction.X+offset.Y*direction.Y;
            if(projection<0||projection>length)return false;
            float perpendicular=MathF.Abs(offset.X*direction.Y
                -offset.Y*direction.X);
            float width=initialArea+(FinalArea-initialArea)
                *(length<=0?0:projection/length);
            return perpendicular<=Math.Max(0,width);
        }));
        // 5bd270 uses an unsigned DataB limit and sorts candidates about the
        // targeted point (5bd7a0/5bd740), rather than about the caster.
        var selected=candidates.OrderBy(candidate=>Vec2.Dist(target.Pos,candidate.Pos))
          .ThenBy(candidate=>candidate.SimulationId)
          .Take(TargetCount<0?int.MaxValue:TargetCount);
        foreach(Unit victim in selected)
        {
            SpellLightningVisuals.Spawn(world,Owner,victim,LightningEffects,0,
                TargetArts,TargetAttachments);
            if(Damage>0)victim.TakeDamage(new DamageData
            {
                Source=Owner,
                Amount=Damage,
                AttackType=AttackType.Spells,
                DamageType=DamageType.Magic,
            });
        }
    }
}
