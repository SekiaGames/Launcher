namespace OpenWC3.Game;

/// <summary>Brilliance Aura（AHab）：范围内提高法力回复。</summary>
public sealed class RuntimeBrillianceAuraAbility(War3ObjectData objects)
    : AuraAbility
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float ManaRegeneration { get; private set; }
    public bool PercentBonus { get; private set; }
    public string AuraBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshBrillianceAuraData()
    {
        int level=Math.Max(1,Level);
        ManaRegeneration=RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0);
        PercentBonus=RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataB",level,0)!=0;
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self
                |TargetFlags.Alive|TargetFlags.Vulnerable;
        AuraBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(AuraBuffRawcode.Length>4)
            AuraBuffRawcode=AuraBuffRawcode[..4];
    }

    public override void OnLevelUp()=>RefreshBrillianceAuraData();
    public override void OnLevelDown()=>RefreshBrillianceAuraData();

    protected override bool CanAffect(Unit target)
    {
        // Native +340=4A19B0 rejects a zero mana-property maximum.
        if(target==null||target.MaxMana<=0)return false;
        // 3D99A0 skips illusion/invisible source gates for self recipients.
        if(!ReferenceEquals(target,Owner))
        {
            var config=Owner.World?.GameplayConfig??War3GameplayConfig.Default;
            if(Owner.IsIllusion&&!config.IllusionsBestowAuras)return false;
            if(Owner.Invisible&&!config.InvisibleUnitsBestowAuras)return false;
        }
        return target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    }

    protected override AuraBuff CreateBuff(Unit source,Unit target,int level)
    {
        var buff=new RuntimeBrillianceAuraBuff(
            ManaRegeneration,PercentBonus)
        {
            Source=source,
            Duration=4,
        };
        BuffTypeData data=objects?.GetBuff(AuraBuffRawcode);
        if(data!=null)buff.BindBuffObjectData(AuraBuffRawcode,data,
            level,source,this);
        else buff.BindObjectData(AuraBuffRawcode,ObjectData,level);
        buff.Duration=4;
        return buff;
    }

    protected override bool RefreshExistingBuff(AuraBuff buff,int level)
    {
        ((RuntimeBrillianceAuraBuff)buff).RefreshFrom(this,level);
        // Rejected weaker input must leave the stronger timed buff intact.
        return true;
    }

    protected override AuraBuff FindExistingBuff(Unit target)
    {
        IReadOnlyList<Ability> abilities=target.Abilities.Items;
        for(int i=0;i<abilities.Count;i++)
            if(abilities[i] is RuntimeBrillianceAuraBuff buff&&buff.Rawcode==AuraBuffRawcode)
                return buff;
        return null;
    }
}

public sealed class RuntimeBrillianceAuraBuff(
    float manaRegeneration,bool percentBonus) : AuraBuff
{
    float _appliedManaRegen;
    float _remaining=4;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Brilliance Aura")??"Brilliance Aura";
    public float ManaRegeneration=>manaRegeneration;
    public bool PercentBonus=>percentBonus;

    protected override void OnBuffAdd()
    {
        _appliedManaRegen=ComputeContribution();
        Owner.ManaRegen+=_appliedManaRegen;
    }
    float ComputeContribution()
    {
        // 49DD90 -> 4BC210: the buff can exist on an illusion without
        // applying a mana modifier when IllusionsCanRestoreMana is disabled.
        var config=Owner.World?.GameplayConfig??War3GameplayConfig.Default;
        if(Owner.IsIllusion&&!config.IllusionsCanRestoreMana)return 0;
        // 49DD90 -> 03AE60 samples the mana property's maximum.
        return percentBonus?Owner.MaxMana*manaRegeneration:manaRegeneration;
    }

    protected override void OnBuffRemove()
    {
        Owner.ManaRegen-=_appliedManaRegen;
        _appliedManaRegen=0;
    }

    public void RefreshParameters(float amount)
    {
        Owner.ManaRegen-=_appliedManaRegen;
        manaRegeneration=amount;
        _appliedManaRegen=ComputeContribution();
        Owner.ManaRegen+=_appliedManaRegen;
    }

    public bool RefreshFrom(RuntimeBrillianceAuraAbility aura,int level)
    {
        const float epsilon=.001f; // DAT_6FB59398, native 514410.
        float incoming=aura.ManaRegeneration;
        if(incoming<manaRegeneration&&MathF.Abs(incoming-manaRegeneration)>=epsilon)return false;
        _remaining=4;
        Source=aura.Owner;Caster=aura.Owner;SourceAura=aura;SourceAbility=aura;AuraLevel=level;
        // Equal and nearly equal values only refresh duration. The original
        // fixed/percentage bit belongs to the buff and is not replaced here.
        if(incoming>manaRegeneration&&MathF.Abs(incoming-manaRegeneration)>=epsilon)
            RefreshParameters(incoming);
        return true;
    }

    public override void OnTick(float dt)
    {
        _remaining-=dt;
        if(_remaining<=0)Owner.RemoveAbility(this);
    }
}
