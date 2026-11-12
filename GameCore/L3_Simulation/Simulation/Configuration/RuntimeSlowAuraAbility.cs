namespace OpenWC3.Game;

/// <summary>
/// Slow Aura（Aasl）：DataA/DataB 是带符号的移动/攻击速度倍率增量。
/// DotA 也大量用零数值、自身目标版本仅承载 Buff 图标。
/// </summary>
public sealed class RuntimeSlowAuraAbility(War3ObjectData objects) : AuraAbility
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float MoveSpeedFactor { get; private set; }
    public float AttackSpeedFactor { get; private set; }
    public string AuraBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshSlowAuraData()
    {
        int level=Math.Max(1,Level);
        MoveSpeedFactor=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0);
        AttackSpeedFactor=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0);
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Self;
        AuraBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(AuraBuffRawcode.Length>4)AuraBuffRawcode=AuraBuffRawcode[..4];
    }

    public override void OnLevelUp()=>RefreshSlowAuraData();
    public override void OnLevelDown()=>RefreshSlowAuraData();

    protected override bool CanAffect(Unit target)
    {
        return target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    }

    protected override AuraBuff CreateBuff(Unit source,Unit target,int level)
    {
        var buff=new RuntimeSlowAuraBuff(MoveSpeedFactor,AttackSpeedFactor)
        {
            Source=source,
            Duration=CheckInterval+0.1f,
        };
        BuffTypeData data=objects?.GetBuff(AuraBuffRawcode);
        if(data!=null)buff.BindBuffObjectData(AuraBuffRawcode,data,
            level,source,this);
        else buff.BindObjectData(AuraBuffRawcode,ObjectData,level);
        return buff;
    }
}

public sealed class RuntimeSlowAuraBuff(
    float moveSpeedFactor,float attackSpeedFactor) : AuraBuff
{
    float _appliedMove;
    float _appliedAttack;

    public override bool Positive=>moveSpeedFactor>=0&&attackSpeedFactor>=0;
    public override string Name=>ObjectData?.GetString("Bufftip","Slow Aura")
        ??"Slow Aura";
    public float MoveSpeedFactor=>moveSpeedFactor;
    public float AttackSpeedFactor=>attackSpeedFactor;

    protected override void OnBuffAdd()
    {
        _appliedMove=Owner.MoveSpeedBase*moveSpeedFactor;
        _appliedAttack=attackSpeedFactor;
        Owner.SpeedBonus+=_appliedMove;
        Owner.AttackSpeedBonusPct+=_appliedAttack;
    }

    protected override void OnBuffRemove()
    {
        Owner.SpeedBonus-=_appliedMove;
        Owner.AttackSpeedBonusPct-=_appliedAttack;
        _appliedMove=0;
        _appliedAttack=0;
    }
}
