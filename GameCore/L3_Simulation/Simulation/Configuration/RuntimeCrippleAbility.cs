namespace OpenWC3.Game;

/// <summary>Cripple（Acri）：DataA/B/C 分别降低移动速度、攻击速度和输出伤害。</summary>
public sealed class RuntimeCrippleAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    public float MoveSpeedReduction { get; private set; }
    public float AttackSpeedReduction { get; private set; }
    public float DamageReduction { get; private set; }
    public float NormalDuration { get; private set; }
    public new float HeroDuration { get; private set; }
    public string CrippleBuffRawcode { get; private set; }="";

    public void RefreshCrippleData()
    {
        int level=Math.Max(1,Level);
        MoveSpeedReduction=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        AttackSpeedReduction=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0));
        DamageReduction=Math.Clamp(RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,0),0,1);
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        CrippleBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(CrippleBuffRawcode.Length>4)CrippleBuffRawcode=CrippleBuffRawcode[..4];
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Enemy|TargetFlags.Organic
                |TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshCrippleData();
    public override void OnLevelDown()=>RefreshCrippleData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target?.Alive==true&&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);

    public override void OnEffect(Unit target,Vec2 point)
    {
        if(target?.Alive!=true)return;
        foreach(RuntimeCrippleBuff existing in target.Abilities
                    .OfType<RuntimeCrippleBuff>()
                    .Where(buff=>buff.Rawcode.Equals(CrippleBuffRawcode,
                        StringComparison.OrdinalIgnoreCase)).ToArray())
            target.RemoveAbility(existing);
        var cripple=new RuntimeCrippleBuff(
            MoveSpeedReduction,AttackSpeedReduction,DamageReduction);
        BuffTypeData data=objects?.GetBuff(CrippleBuffRawcode);
        if(data!=null)cripple.BindBuffObjectData(CrippleBuffRawcode,data,
            Math.Max(1,Level),Owner,this);
        else cripple.BindObjectData(CrippleBuffRawcode,ObjectData,
            Math.Max(1,Level));
        cripple.Duration=target.IsHero?HeroDuration:NormalDuration;
        target.AddAbility(cripple);
    }
}

public sealed class RuntimeCrippleBuff(float moveSpeedReduction,
    float attackSpeedReduction,float damageReduction) : Buff, IAttackDamageStatModifier
{
    float _appliedMove;
    float _appliedAttack;
    public override bool Positive=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Cripple")??"Cripple";

    protected override void OnBuffAdd()
    {
        _appliedMove=Owner.MoveSpeedBase*moveSpeedReduction;
        _appliedAttack=attackSpeedReduction;
        Owner.SpeedBonus-=_appliedMove;
        Owner.AttackSpeedBonusPct-=_appliedAttack;
    }
    protected override void OnBuffRemove()
    {
        Owner.SpeedBonus+=_appliedMove;
        Owner.AttackSpeedBonusPct+=_appliedAttack;
        _appliedMove=0;
        _appliedAttack=0;
    }
    public override void ModifyOutgoingDamage(ref DamageData damage)
    {
        if(damage.IsAttack)damage.DamageMultiplier*=1-damageReduction;
    }
}
