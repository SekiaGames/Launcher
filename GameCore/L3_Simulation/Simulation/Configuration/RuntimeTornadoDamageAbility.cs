namespace OpenWC3.Game;

/// <summary>
/// CAbilityTornadoDamage（Atdg）：每秒结算最多三圈，选择包含目标且半径
/// 最小的一圈；伤害不累加。原版取证 Tool/Re/tornado_damage_oracle.py。
/// </summary>
public sealed class RuntimeTornadoDamageAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;
    float _damageTimer=1;

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public float DamagePerSecond { get; private set; }
    /// <summary>第二圈半径DataB；不强制小于Area。</summary>
    public float InnerRadius { get; private set; }
    /// <summary>第二圈每秒伤害DataC，不与DataA累加。</summary>
    public float InnerDamagePerSecond { get; private set; }
    public float DataD { get; private set; }
    public float DataE { get; private set; }
    public float Radius { get; private set; }
    public string DamageBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshTornadoDamageData()
    {
        int level=Math.Max(1,Level);
        DamagePerSecond=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));

        InnerRadius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0));
        InnerDamagePerSecond=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,0));
        DataD=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataD",level,0);
        DataE=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataE",level,0);
        Radius=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Area",level,ConfiguredAreaOfEffect));

        DamageBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(DamageBuffRawcode.Length>4)
            DamageBuffRawcode=DamageBuffRawcode[..4];
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetFlags.Enemy|TargetFlags.Alive|TargetFlags.Vulnerable;
        else if((_allowedTargets&TargetRules.Relations)==0)
            _allowedTargets|=TargetFlags.Enemy;
        if((_allowedTargets&(TargetFlags.Alive|TargetFlags.Dead))==0)
            _allowedTargets|=TargetFlags.Alive;
        if((_allowedTargets&(TargetFlags.Vulnerable|TargetFlags.Invulnerable))==0)
            _allowedTargets|=TargetFlags.Vulnerable;
        if((_allowedTargets&(TargetFlags.Hero|TargetFlags.NonHero))
            ==(TargetFlags.Hero|TargetFlags.NonHero))
            _allowedTargets&=~(TargetFlags.Hero|TargetFlags.NonHero);
        _damageTimer=1;
    }

    public override void OnLevelUp()=>RefreshTornadoDamageData();
    public override void OnLevelDown()=>RefreshTornadoDamageData();
    // Native +e0 stops the pulse timer; +e4 starts a fresh one-second timer.
    public override void OnDisabledStateChanged()=>_damageTimer=1;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;

    public override void OnTick(float dt)
    {
        base.OnTick(dt);
        if(Owner?.World==null||!Owner.Alive||Level<=0||IsDisabled)return;
        _damageTimer-=dt;
        while(_damageTimer<=0)
        {
            _damageTimer+=1;
            ApplyDamagePulse();
        }
    }

    void ApplyDamagePulse()
    {
        var world=Owner.World;
        float queryRadius=DamagePerSecond>0?Radius:0;
        if(InnerDamagePerSecond>0)queryRadius=Math.Max(queryRadius,InnerRadius);
        if(DataE>0)queryRadius=Math.Max(queryRadius,DataD);
        if(queryRadius<=0)return;
        using var query=RuntimeSpatialUnitQuery.RentAround(world,Owner.Pos,queryRadius);
        var targets=query.Units;int count=0;
        for(int i=0;i<targets.Count;i++)
        {
            Unit unit=targets[i];
            if(unit.Alive&&!unit.Removed&&unit.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true)
                &&Vec2.Dist(Owner.Pos,unit.Pos)<=queryRadius)targets[count++]=unit;
        }
        targets.RemoveRange(count,targets.Count-count);world.SortUnitsBySimulationId(targets);
        foreach(Unit target in targets)
        {
            float distance=Vec2.Dist(Owner.Pos,target.Pos),bestRadius=float.PositiveInfinity,amount=0;
            SelectRing(Radius,DamagePerSecond,distance,ref bestRadius,ref amount);
            SelectRing(InnerRadius,InnerDamagePerSecond,distance,ref bestRadius,ref amount);
            SelectRing(DataD,DataE,distance,ref bestRadius,ref amount);
            if(amount<=0)continue;
            target.TakeDamage(new DamageData
            {
                Source=Owner,
                Amount=amount,
                AttackType=AttackType.Spells,
                DamageType=DamageType.Magic,
            });
            if(!target.Alive||DamageBuffRawcode.Length!=4)continue;
            foreach(RuntimeTornadoDamageBuff existing in target.Abilities
                        .OfType<RuntimeTornadoDamageBuff>()
                        .Where(buff=>ReferenceEquals(buff.SourceAbility,this))
                        .ToArray())
                target.RemoveAbility(existing);
            var buff=new RuntimeTornadoDamageBuff();
            BuffTypeData buffData=objects?.GetBuff(DamageBuffRawcode);
            if(buffData!=null)buff.BindBuffObjectData(DamageBuffRawcode,
                buffData,Math.Max(1,Level),Owner,this);
            else buff.BindObjectData(DamageBuffRawcode,ObjectData,
                Math.Max(1,Level));
            buff.Duration=1.05f;
            target.AddAbility(buff);
        }
    }
    static void SelectRing(float radius,float damage,float distance,ref float bestRadius,ref float amount)
    {
        // 6af6c0 retains the first ring on equal radii, irrespective of damage.
        if(radius>0&&damage>0&&distance<=radius&&radius<bestRadius)
        {bestRadius=radius;amount=damage;}
    }
}

public sealed class RuntimeTornadoDamageBuff : Buff
{
    public override bool Positive=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Tornado Damage")??"Tornado Damage";
    protected override void OnBuffAdd() { }
    protected override void OnBuffRemove() { }
}
