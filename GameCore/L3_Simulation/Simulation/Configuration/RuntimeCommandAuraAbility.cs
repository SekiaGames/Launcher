namespace OpenWC3.Game;

/// <summary>
/// Command Aura（AOac）：DataA 为伤害增量；DataD=0 时按倍率，非零时按固定值。
/// DataB/DataC 分别控制近战/远程武器。
/// </summary>
public class RuntimeCommandAuraAbility(War3ObjectData objects) : AuraAbility
{
    TargetFlags _allowedTargets;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float DamageFactor { get; private set; }
    public float TechDamageFactorBonus { get; private set; }
    public bool AffectsMelee { get; private set; }
    public bool AffectsRanged { get; private set; }
    public bool FlatBonus { get; private set; }
    public string AuraBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshCommandAuraData()
    {
        int level=Math.Max(1,Level);
        DamageFactor=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",level,0);
        AffectsMelee=RuntimeAbilityFactory.ReadInt(ObjectData,"DataB",level,1)!=0;
        AffectsRanged=RuntimeAbilityFactory.ReadInt(ObjectData,"DataC",level,1)!=0;
        FlatBonus=RuntimeAbilityFactory.ReadInt(ObjectData,"DataD",level,0)!=0;
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self|TargetFlags.Alive
                |TargetFlags.Vulnerable;
        AuraBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(AuraBuffRawcode.Length>4)AuraBuffRawcode=AuraBuffRawcode[..4];
    }

    public void SetTechDamageFactorBonus(float bonus)
    {
        TechDamageFactorBonus=bonus;
    }

    public override void OnLevelUp()=>RefreshCommandAuraData();
    public override void OnLevelDown()=>RefreshCommandAuraData();
    protected override bool IsBuffCurrent(AuraBuff buff)=>buff is RuntimeCommandAuraBuff command
        &&command.Matches(DamageFactor+TechDamageFactorBonus,AffectsMelee,AffectsRanged,FlatBonus);
    public override bool CanRetainBuff(AuraBuff buff)=>buff?.Owner!=null&&CanAffect(buff.Owner);

    protected override bool CanAffect(Unit target)
    {
        if(IsDisabled)return false;
        return target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    }

    protected override AuraBuff CreateBuff(Unit source,Unit target,int level)
    {
        var buff=new RuntimeCommandAuraBuff(DamageFactor+TechDamageFactorBonus,AffectsMelee,
            AffectsRanged,FlatBonus)
        {
            Source=source,
            Duration=CheckInterval+.1f,
        };
        BuffTypeData data=objects?.GetBuff(AuraBuffRawcode);
        if(data!=null)buff.BindBuffObjectData(AuraBuffRawcode,data,
            level,source,this);
        else buff.BindObjectData(AuraBuffRawcode,ObjectData,level);
        buff.Duration=CheckInterval+.1f;
        return buff;
    }
}

public sealed class RuntimeCommandAuraBuff(
    float damageFactor,bool affectsMelee,bool affectsRanged,bool flatBonus) : AuraBuff, IAttackDamageStatModifier
{
    public override bool Positive=>damageFactor>=0;
    public override bool Magic=>false;
    public override bool AutoDispel=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Command Aura")??"Command Aura";
    public float DamageFactor=>damageFactor;
    public bool FlatBonus=>flatBonus;
    public bool Matches(float factor,bool melee,bool ranged,bool flat)=>
        damageFactor==factor&&affectsMelee==melee&&affectsRanged==ranged&&flatBonus==flat;

    public bool AppliesTo(Weapon weapon)=>weapon!=null
        &&(weapon.WeaponType==WeaponType.Missile?affectsRanged:affectsMelee)
        &&IsDominantCopy();

    protected override void OnBuffAdd() { }
    protected override void OnBuffRemove() { }

    public override void ModifyOutgoingDamage(ref DamageData d)
    {
        if(!d.IsAttack||(d.IsRanged?!affectsRanged:!affectsMelee))return;
        // Game.dll resolves identical aura buffs by strength; two sources of
        // BEar (or another shared BuffID) do not multiply the same attack twice.
        // Different aura BuffIDs remain independently stackable.
        if(!IsDominantCopy())return;
        if(flatBonus)d.BonusAmount+=damageFactor;
        else d.DamageMultiplier*=Math.Max(0,1f+damageFactor);
    }

    bool IsDominantCopy()
    {
        if(Owner==null)return true;
        foreach(RuntimeCommandAuraBuff other in Owner.Abilities
            .OfType<RuntimeCommandAuraBuff>())
        {
            if(ReferenceEquals(other,this)
                ||!string.Equals(other.Rawcode,Rawcode,
                    StringComparison.OrdinalIgnoreCase))continue;
            if(other.DamageFactor>DamageFactor+0.0001f)return false;
            if(Math.Abs(other.DamageFactor-DamageFactor)<=0.0001f
                &&(other.Source?.SimulationId??int.MaxValue)
                    <(Source?.SimulationId??int.MaxValue))return false;
        }
        return true;
    }
}

/// <summary>
/// CAbilityAuraTrueshot（AEar）与 Command Aura 共享 DataA-D 的武器类型/
/// 固定值开关合同；保留独立运行时类型以对应原生 RTTI。
/// </summary>
public sealed class RuntimeTrueshotAuraAbility(War3ObjectData objects)
    : RuntimeCommandAuraAbility(objects);
