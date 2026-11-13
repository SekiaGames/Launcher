namespace OpenWC3.Game;

/// <summary>CAbilityCleavingAttack（ANca）：近战命中后向攻击者前方分裂伤害。</summary>
public sealed class RuntimeCleavingAttackAbility : Ability
{
    TargetFlags _allowedTargets;
    bool _pendingCleave;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float DamageFactor { get; private set; }
    public float Radius { get; private set; }
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshCleavingAttackData()
    {
        int level=Math.Max(1,Level);
        DamageFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,0));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if((_allowedTargets&TargetRules.Shapes)==0)
            _allowedTargets|=TargetFlags.Ground;
        if((_allowedTargets&TargetRules.Relations)==0)
            _allowedTargets|=TargetFlags.Enemy;
        _allowedTargets|=TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshCleavingAttackData();
    public override void OnLevelDown()=>RefreshCleavingAttackData();
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
    public override void ModifyOutgoingDamage(Unit target,ref DamageData damage)
    {
        _pendingCleave=Level>0&&!IsDisabled&&Owner?.IsIllusion!=true
            &&damage.IsAttack&&!damage.IsRanged
            &&target?.Alive==true&&DamageFactor>0;
    }

    public override void OnAttackHit(Unit target,float dealt)
    {
        bool active=_pendingCleave;
        _pendingCleave=false;
        World world=Owner?.World;
        if(!active||world==null||target==null||dealt<=0)return;
        // 4afe70 uses the owner's position plus Area along its current facing,
        // independently of the primary target's position/collision radius.
        Vec2 center=Owner.Pos+new Vec2(MathF.Cos(Owner.Facing),MathF.Sin(Owner.Facing))*Radius;
        foreach(Unit secondary in RuntimeSpatialUnitQuery.Around(world,center,Radius)
                    .Where(unit=>unit.Alive&&!unit.Removed
                        &&!ReferenceEquals(unit,target)
                        &&!ReferenceEquals(unit,Owner)
                        &&Vec2.Dist(unit.Pos,center)<=Radius
                        &&unit.CanBeTargetedBy(Owner,_allowedTargets))
                    .OrderBy(unit=>unit.SimulationId).ToArray())
        {
            secondary.TakeDamage(new DamageData
            {
                Source=Owner,Amount=dealt*DamageFactor,
                AttackType=Owner.Stats.AttackType,
                DamageType=DamageType.Universal,
                IsAttack=false,IsRanged=false,
            });
            foreach(string art in SpecialArts)
                SpawnAttachedArt(art,secondary,SpecialAttachments,1.2f);
        }
    }
}
