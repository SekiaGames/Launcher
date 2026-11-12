namespace OpenWC3.Game;

/// <summary>Unholy Aura（AUau）：范围内提高移动速度与生命回复。</summary>
public sealed class RuntimeUnholyAuraAbility(War3ObjectData objects)
    : AuraAbility
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float MoveSpeedFactor { get; private set; }
    public float LifeRegeneration { get; private set; }
    public bool PercentLifeRegeneration { get; private set; }
    public string AuraBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshUnholyAuraData()
    {
        int level=Math.Max(1,Level);
        MoveSpeedFactor=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0);
        LifeRegeneration=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0);
        PercentLifeRegeneration=RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataC",level,0)!=0;
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));
        string targetRules=RuntimeAbilityFactory.ReadTargetRules(
            ObjectData,level);
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(targetRules);
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self
                |TargetFlags.Alive|TargetFlags.Vulnerable;
        AuraBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(AuraBuffRawcode.Length>4)
            AuraBuffRawcode=AuraBuffRawcode[..4];
    }

    public override void OnLevelUp()=>RefreshUnholyAuraData();
    public override void OnLevelDown()=>RefreshUnholyAuraData();
    public override bool CanRetainBuff(AuraBuff buff)=>buff?.Owner!=null&&CanAffect(buff.Owner);
    protected override AuraBuff FindExistingBuff(Unit target)
    {
        // 3d99a0 -> 46f440 reuses the matching BuffID, independently of source.
        // Preserve first-match order without allocating OfType/enumerator state.
        IReadOnlyList<Ability> abilities = target.Abilities.Items;
        for (int i = 0; i < abilities.Count; i++)
            if (abilities[i] is RuntimeUnholyAuraBuff buff && buff.Rawcode == AuraBuffRawcode)
                return buff;
        return null;
    }
    protected override bool RefreshExistingBuff(AuraBuff buff,int level)
    {
        ((RuntimeUnholyAuraBuff)buff).RefreshFrom(this,level);
        return true;
    }

    protected override bool CanAffect(Unit target)
    {
        if(IsDisabled)return false;
        return target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    }

    protected override AuraBuff CreateBuff(Unit source,Unit target,int level)
    {
        var buff=new RuntimeUnholyAuraBuff(MoveSpeedFactor,
            LifeRegeneration,PercentLifeRegeneration)
        {
            Source=source,
            Duration=4,
        };
        BuffTypeData data=objects?.GetBuff(AuraBuffRawcode);
        if(data!=null)buff.BindBuffObjectData(AuraBuffRawcode,data,
            level,source,this);
        else buff.BindObjectData(AuraBuffRawcode,ObjectData,level);
        return buff;
    }
}

public sealed class RuntimeUnholyAuraBuff(float moveSpeedFactor,
    float lifeRegeneration,bool percentLifeRegeneration) : AuraBuff
{
    float _appliedMove;
    float _appliedLifeRegen;
    float _moveSpeedFactor=moveSpeedFactor;
    float _lifeRegeneration=lifeRegeneration;
    float _remaining=4;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Unholy Aura")??"Unholy Aura";
    public float MoveSpeedFactor=>_moveSpeedFactor;
    public float LifeRegeneration=>_lifeRegeneration;

    protected override void OnBuffAdd()
    {
        RefreshModifiers();
    }
    void RefreshModifiers()
    {
        float move=StandalonePathing.Sim.CantPathSearchPoint.NativeMultiply(Owner.MoveSpeedBase,_moveSpeedFactor);
        float regeneration=percentLifeRegeneration
            ?StandalonePathing.Sim.CantPathSearchPoint.NativeMultiply(Owner.MaxLife,_lifeRegeneration):_lifeRegeneration;
        Owner.SpeedBonus+=move-_appliedMove;
        Owner.AlwaysLifeRegenBonus+=regeneration-_appliedLifeRegen;
        _appliedMove=move;_appliedLifeRegen=regeneration;
    }
    public override void OnTick(float dt)
    {
        RefreshModifiers();
        _remaining-=dt;
        if(_remaining<=0)Owner.RemoveAbility(this);
    }
    public bool RefreshFrom(RuntimeUnholyAuraAbility aura,int level)
    {
        const float epsilon=.001f;
        float move=aura.MoveSpeedFactor,regeneration=aura.LifeRegeneration;
        // 4c9d50 rejects only when BOTH components are meaningfully weaker.
        if(move<_moveSpeedFactor&&Math.Abs(move-_moveSpeedFactor)>=epsilon
            &&regeneration<_lifeRegeneration&&Math.Abs(regeneration-_lifeRegeneration)>=epsilon)return false;
        _remaining=4;Source=aura.Owner;SourceAura=aura;AuraLevel=level;
        if(move>_moveSpeedFactor&&Math.Abs(move-_moveSpeedFactor)>=epsilon)_moveSpeedFactor=move;
        if(regeneration>_lifeRegeneration&&Math.Abs(regeneration-_lifeRegeneration)>=epsilon)_lifeRegeneration=regeneration;
        RefreshModifiers();return true;
    }
    protected override void OnBuffRemove()
    {
        Owner.SpeedBonus-=_appliedMove;
        Owner.AlwaysLifeRegenBonus-=_appliedLifeRegen;
        _appliedMove=0;
        _appliedLifeRegen=0;
    }
}
