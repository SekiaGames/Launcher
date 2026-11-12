namespace OpenWC3.Game;

/// <summary>
/// CAbilityNeutralRegen（ANre）：为范围内非机械单位挂载 ANrl/ANrm。
/// 两者都按目标最大生命/法力计算，而不是普通法术书或固定值光环。
/// </summary>
public sealed class RuntimeNeutralRegenAbility(War3ObjectData objects) : AuraAbility
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool CheckCanTarget(Unit target,Vec2 point)=>false;
    public float LifeRegenerationFactor { get; private set; }
    public float ManaRegenerationFactor { get; private set; }

    public void RefreshNeutralRegenData()
    {
        int level=Math.Max(1,Level);
        LifeRegenerationFactor=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",level,0);
        ManaRegenerationFactor=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataB",level,0);
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air|TargetFlags.Organic
                |TargetFlags.Vulnerable|TargetFlags.Invulnerable;
    }

    public override void OnLevelUp()=>RefreshNeutralRegenData();
    public override void OnLevelDown()=>RefreshNeutralRegenData();

    protected override bool CanAffect(Unit target)=>!IsDisabled
        &&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);

    protected override AuraBuff FindExistingBuff(Unit target)=>
        target.Abilities.Items.OfType<RuntimeNeutralRegenBuff>().FirstOrDefault();

    protected override bool RefreshExistingBuff(AuraBuff buff,int level)
    {
        ((RuntimeNeutralRegenBuff)buff).RefreshFrom(this,level);
        return true;
    }

    protected override AuraBuff CreateBuff(Unit source,Unit target,int level)
    {
        var buff=new RuntimeNeutralRegenBuff(
            LifeRegenerationFactor,ManaRegenerationFactor) { Source=source };
        // ★ 生命之泉(nfoh)/魔法之泉(nmoo) 的回复特效来自 **buff 物编的 Targetart**:
        //     [ANrl] Targetart=Abilities\Spells\Other\ANrm\ANrmTarget.mdx
        //     [ANrm] Targetart=Abilities\Spells\Other\ANrl\ANrlTarget.mdx
        //   两者都带 Effectsoundlooped=FountainOfLifeLoop(Units\NeutralAbilityFunc.txt),
        //   模型文件在 war3.mpq 中确实存在。Game.dll 侧 CAbilityNeutralRegenLife /
        //   CAbilityNeutralRegenMana 的 +0x1c 类型码 getter(0x6f3fcfa0/0x6f3fcfb0)
        //   直接返回 'ANrl'/'ANrm', 说明这两个 buff 就是回复光环的挂载表现载体。
        //   旧实现把 buff 绑到 **ANre 技能**数据上 —— 技能表里既没有 Targetart 也没有
        //   Targetattach, BuffVisualDataCache 取到空模型, 所以回复"能回血但不显示特效"。
        string rawcode=LifeRegenerationFactor>0?"ANrl":"ANrm";
        BuffTypeData data=objects?.GetBuff(rawcode);
        buff.BuffRawcode=rawcode;
        if(data!=null)buff.BindBuffObjectData(rawcode,data,level,source,this);
        else buff.BindObjectData(rawcode,ObjectData,level);
        return buff;
    }
}

/// <summary>ANrl 与 ANrm 的成对实现；离开来源后由 AuraBuff 负责移除。</summary>
public sealed class RuntimeNeutralRegenBuff(
    float lifeRegenerationFactor,float manaRegenerationFactor) : AuraBuff
{
    float _lifeFactor=lifeRegenerationFactor;
    float _manaFactor=manaRegenerationFactor;
    float _appliedLife;
    float _appliedMana;

    public override string Name=>ObjectData?.GetString("Name","Neutral Regeneration")
        ??"Neutral Regeneration";
    /// <summary>实际挂载的 buff 物编(ANrl/ANrm); 表现层按它的 Targetart 生成回复特效。</summary>
    public string BuffRawcode { get; set; } = "";

    protected override void OnBuffAdd()=>RefreshRegeneration();

    void RefreshRegeneration()
    {
        // Game.dll 53fe90 sets the percentage flag for both attached abilities.
        float life=Owner.MaxLife*_lifeFactor;
        float mana=Owner.MaxMana*_manaFactor;
        Owner.AlwaysLifeRegenBonus+=life-_appliedLife;
        Owner.ManaRegen+=mana-_appliedMana;
        _appliedLife=life;_appliedMana=mana;
    }

    public void RefreshFrom(RuntimeNeutralRegenAbility aura,int level)
    {
        _lifeFactor=aura.LifeRegenerationFactor;
        _manaFactor=aura.ManaRegenerationFactor;
        Source=aura.Owner;Caster=aura.Owner;SourceAura=aura;SourceAbility=aura;
        AuraLevel=level;
        Refresh();
        RefreshRegeneration();
    }

    public override void OnTick(float dt)
    {
        // Native maximum-life/mana notifications rebuild the modifier. Polling
        // also covers changes from hero stats, items and upgrades.
        RefreshRegeneration();
        base.OnTick(dt);
    }

    protected override void OnBuffRemove()
    {
        Owner.AlwaysLifeRegenBonus-=_appliedLife;
        Owner.ManaRegen-=_appliedMana;
        _appliedLife=0;_appliedMana=0;
    }
}
