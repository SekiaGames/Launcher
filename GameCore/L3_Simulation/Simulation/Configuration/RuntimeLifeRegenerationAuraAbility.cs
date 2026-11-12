namespace OpenWC3.Game;

/// <summary>CAbilityAuraRegenLife（Aoar）：固定值或按目标最大生命计算的生命回复光环。</summary>
public sealed class RuntimeLifeRegenerationAuraAbility(War3ObjectData objects)
    : AuraAbility
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool CheckCanTarget(Unit target,Vec2 point)=>false;
    public float LifeRegeneration { get; private set; }
    public bool PercentOfMaximumLife { get; private set; }
    public string AuraBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshLifeRegenerationAuraData()
    {
        int level=Math.Max(1,Level);
        LifeRegeneration=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",level,0);
        PercentOfMaximumLife=RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataB",level,0)!=0;
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air|TargetFlags.Ally
                |TargetFlags.Self|TargetFlags.Alive|TargetFlags.Vulnerable;
        if((_allowedTargets&(TargetFlags.Hero|TargetFlags.NonHero))
            ==(TargetFlags.Hero|TargetFlags.NonHero))
            _allowedTargets&=~(TargetFlags.Hero|TargetFlags.NonHero);
        AuraBuffRawcode=RuntimeAbilityFactory.ReadString(ObjectData,"BuffID",level);
        if(AuraBuffRawcode.Length>4)AuraBuffRawcode=AuraBuffRawcode[..4];
        // ★ 生命之泉(nfoh) 的 ACnr 在 AbilityData.slk 里 **没有 BuffID 列**, 但原版
        //   CAbilityNeutralRegenLife 仍然给半径内单位挂 ANrl 生命回复 buff; 回复特效
        //   就是该 buff 物编的 Targetart(Units\NeutralAbilityFunc.txt: [ANrl]
        //   Targetart=Abilities\Spells\Other\ANrm\ANrmTarget.mdx + FountainOfLifeLoop)。
        //   缺省空串时 BuffVisualDataCache 读到空模型 —— 泉水会回血却完全没有特效。
        if(AuraBuffRawcode.Length==0
            &&Rawcode.Equals("ACnr",StringComparison.OrdinalIgnoreCase))
            AuraBuffRawcode="ANrl";
    }

    public override void OnLevelUp()=>RefreshLifeRegenerationAuraData();
    public override void OnLevelDown()=>RefreshLifeRegenerationAuraData();
    public override bool CanRetainBuff(AuraBuff buff)=>buff?.Owner!=null&&CanAffect(buff.Owner);
    protected override AuraBuff FindExistingBuff(Unit target)=>target.Abilities.Items
        .OfType<RuntimeLifeRegenerationAuraBuff>().FirstOrDefault(buff=>buff.Rawcode==AuraBuffRawcode);
    protected override bool RefreshExistingBuff(AuraBuff buff,int level)
    {
        ((RuntimeLifeRegenerationAuraBuff)buff).RefreshFrom(this,level);
        return true;
    }

    protected override bool CanAffect(Unit target)
    {
        if(IsDisabled)return false;
        return target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    }

    protected override AuraBuff CreateBuff(Unit source,Unit target,int level)
    {
        var buff=new RuntimeLifeRegenerationAuraBuff(
            LifeRegeneration,PercentOfMaximumLife)
        {
            Source=source,
            Duration=4,
        };
        BuffTypeData data=objects?.GetBuff(AuraBuffRawcode);
        if(data!=null)buff.BindBuffObjectData(
            AuraBuffRawcode,data,level,source,this);
        else buff.BindObjectData(AuraBuffRawcode,ObjectData,level);
        return buff;
    }
}

public sealed class RuntimeLifeRegenerationAuraBuff(
    float lifeRegeneration,bool percentOfMaximumLife) : AuraBuff
{
    float _applied;
    float _regeneration=lifeRegeneration;
    float _remaining=4;

    public override string Name=>ObjectData?.GetString(
        "Bufftip","Life Regeneration Aura")??"Life Regeneration Aura";
    public float LifeRegeneration=>_regeneration;
    public bool PercentOfMaximumLife=>percentOfMaximumLife;
    public override bool VisualAllowedWhileAirborne=>true;

    protected override void OnBuffAdd()
    {
        RefreshRegeneration();
    }

    void RefreshRegeneration()
    {
        float amount=percentOfMaximumLife
            ?Owner.MaxLife*_regeneration:_regeneration;
        Owner.AlwaysLifeRegenBonus+=amount-_applied;
        _applied=amount;
    }

    public override void OnTick(float dt)
    {
        // Original d02a8 maximum-life notification rebuilds the modifier.
        // Polling here also covers max-life changes from upgrades and items.
        RefreshRegeneration();
        _remaining-=dt;
        if(_remaining<=0)Owner.RemoveAbility(this);
    }

    public bool RefreshFrom(RuntimeLifeRegenerationAuraAbility aura,int level)
    {
        const float epsilon=.001f; // Original DAT_6fb59398.
        float incoming=aura.LifeRegeneration;
        if(incoming<_regeneration&&Math.Abs(incoming-_regeneration)>=epsilon)return false;
        _remaining=4;
        Source=aura.Owner;SourceAura=aura;AuraLevel=level;
        // Original 41db10 updates the value, retaining the existing fixed/% bit.
        if(incoming>_regeneration&&Math.Abs(incoming-_regeneration)>=epsilon)
        {_regeneration=incoming;RefreshRegeneration();}
        return true;
    }

    protected override void OnBuffRemove()
    {
        Owner.AlwaysLifeRegenBonus-=_applied;
        _applied=0;
    }
}
