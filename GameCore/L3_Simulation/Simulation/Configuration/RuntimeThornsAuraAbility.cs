namespace OpenWC3.Game;

/// <summary>CAbilityAuraThorns（AEah）：范围内友军受到近战普通类型伤害时反弹伤害。</summary>
public sealed class RuntimeThornsAuraAbility(War3ObjectData objects)
    : AuraAbility
{
    TargetFlags _allowedTargets;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float ReturnedDamage { get; private set; }
    public bool Percentage { get; private set; }
    public string AuraBuffRawcode { get; private set; }="";

    public void RefreshThornsAuraData()
    {
        int level=Math.Max(1,Level);
        ReturnedDamage=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        Percentage=RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataB",level,1)!=0;
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));
        AuraBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(AuraBuffRawcode.Length>4)AuraBuffRawcode=AuraBuffRawcode[..4];
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Player|TargetFlags.Ally|TargetFlags.Self
                |TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshThornsAuraData();
    public override void OnLevelDown()=>RefreshThornsAuraData();
    protected override bool CanAffect(Unit target)
    {
        return target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    }
    protected override AuraBuff CreateBuff(Unit source,Unit target,int level)
    {
        var buff=new RuntimeThornsAuraBuff(ReturnedDamage,Percentage)
        {
            Source=source,Duration=CheckInterval+.1f,
        };
        BuffTypeData data=objects?.GetBuff(AuraBuffRawcode);
        if(data!=null)buff.BindBuffObjectData(
            AuraBuffRawcode,data,level,source,this);
        else buff.BindObjectData(AuraBuffRawcode,ObjectData,level);
        return buff;
    }
}

public sealed class RuntimeThornsAuraBuff(
    float returnedDamage,bool percentage) : AuraBuff
{
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Thorns Aura")??"Thorns Aura";
    public float ReturnedDamage=>returnedDamage;
    public bool Percentage=>percentage;
    protected override void OnBuffAdd() { }
    protected override void OnBuffRemove() { }
    public override void ModifyIncomingDamage(ref DamageData damage)
    {
        Unit attacker=damage.Source;
        // CBuffAuraThorns +f8=1, +fc=49af30: before armor/final shields,
        // Normal packet mask and ranged bit, independently of attack-event bit.
        if(Owner?.World==null||attacker==null||attacker.Removed||damage.IsRanged
            ||damage.DamageType!=DamageType.Normal||Owner.IsIllusion
            ||Owner.World.AreAllied(Owner,attacker))return;
        float incoming=(damage.Amount+damage.BonusAmount)*damage.DamageMultiplier;
        float reflected=percentage
            ?StandalonePathing.Sim.CantPathSearchPoint.NativeMultiply(incoming,returnedDamage)
            :returnedDamage;
        attacker.TakeDamage(new DamageData
        {
            Source=Owner,Amount=Math.Max(1f,reflected),
            AttackType=AttackType.Spells,DamageType=DamageType.Plant,
        });
    }
}
