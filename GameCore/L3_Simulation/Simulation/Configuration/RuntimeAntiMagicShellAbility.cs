namespace OpenWC3.Game;

/// <summary>CAbilityAntiMagicShell（Aams）：反召唤伤害，或为普通单位建立魔法吸收层。</summary>
public sealed class RuntimeAntiMagicShellAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    public float SummonedUnitDamage { get; private set; }
    public float MagicDamageReduction { get; private set; }
    public float ShieldLife { get; private set; }
    public float ManaLoss { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string ShellBuffRawcode { get; private set; }="";
    public TargetFlags AllowedTargets=>_allowedTargets;

    public void RefreshAntiMagicShellData()
    {
        int level=Math.Max(1,Level);
        SummonedUnitDamage=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"DataA",level,0));
        MagicDamageReduction=Math.Clamp(RuntimeAbilityFactory.ReadFloat(ObjectData,"DataB",level,0),0,1);
        ShieldLife=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"DataC",level,0));
        ManaLoss=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"DataD",level,0));
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"HeroDur",level,NormalDuration));
        var buffs=RuntimeAbilityFactory.ReadString(ObjectData,"BuffID",level)
            .SplitConfig(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        // Original 6112f0 selects buff index 1 when DataC supplies an absorb pool.
        ShellBuffRawcode=Normalize(buffs.ElementAtOrDefault(ShieldLife>0&&buffs.Length>1?1:0));
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if((_allowedTargets&TargetRules.Relations)==0)
            _allowedTargets|=TargetRules.Relations;
        if((_allowedTargets&TargetRules.Shapes)==0)
            _allowedTargets|=TargetFlags.Ground|TargetFlags.Air;
        _allowedTargets|=TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshAntiMagicShellData();
    public override void OnLevelDown()=>RefreshAntiMagicShellData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target?.Alive==true&&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true)
        &&(target.Identity.Is(Classification.Summoned)
            ||!target.Abilities.OfType<RuntimeAntiMagicShellBuff>().Any());
    public override void OnEffect(Unit target,Vec2 point)
    {
        if(target?.Alive!=true)return;
        if(target.Identity.Is(Classification.Summoned))
        {
            target.Mana=Math.Max(0,target.Mana-ManaLoss);
            if(SummonedUnitDamage>0)target.TakeDamage(new DamageData
            {
                Source=Owner,Amount=SummonedUnitDamage,
                AttackType=AttackType.Spells,DamageType=DamageType.Magic,
            });
            return;
        }
        foreach(RuntimeAntiMagicShellBuff old in target.Abilities
                    .OfType<RuntimeAntiMagicShellBuff>().ToArray())
            target.RemoveAbility(old);
        var buff=new RuntimeAntiMagicShellBuff(MagicDamageReduction,ShieldLife);
        BindBuff(buff,objects,ShellBuffRawcode,target);
        buff.Duration=target.IsHero?HeroDuration:NormalDuration;
        target.AddAbility(buff);
    }

    void BindBuff(Buff buff,War3ObjectData objectData,string rawcode,Unit target)
    {
        BuffTypeData data=objectData?.GetBuff(rawcode);
        if(data!=null)buff.BindBuffObjectData(rawcode,data,Math.Max(1,Level),Owner,this);
        else buff.BindObjectData(rawcode,ObjectData,Math.Max(1,Level));
    }
    static string Normalize(string value)=>string.IsNullOrWhiteSpace(value)||value=="-"
        ?"":value.Length>4?value[..4]:value;
}

public sealed class RuntimeAntiMagicShellBuff(float reduction,float shieldLife) : Buff
{
    float _remaining=shieldLife;
    bool _fullImmunity;
    public float RemainingShield=>_remaining;
    public override string Name=>ObjectData?.GetString("Bufftip","Anti-Magic Shell")
        ??"Anti-Magic Shell";
    protected override void OnBuffAdd()
    {
        // 经典 Aams 的 Shield Life=0 表示持续魔免；有生命值时则是可耗尽吸收盾。
        _fullImmunity=_remaining<=0;
        if(_fullImmunity)Owner.AddState(StateModType.MagicImmune);
    }
    protected override void OnBuffRemove()
    {
        if(_fullImmunity)Owner.RemoveState(StateModType.MagicImmune);
        _fullImmunity=false;
    }
    public override void ModifyNativeDamagePacket(ref DamageData damage)
    {
        // Bam2 stage 5: consume raw packet amount before armor/type and
        // ethereal amplification; DataB is not an absorb-pool percentage.
        if(Owner.IsIllusion||damage.Source==null||damage.Source.Removed
            ||damage.AttackType is not (AttackType.Magic or AttackType.Spells)
            ||damage.DamageType==DamageType.Universal||_remaining<=0)return;
        float raw=(damage.Amount+damage.BonusAmount)*damage.DamageMultiplier;
        if(raw<=0)return;
        float absorbed=Math.Min(_remaining,raw);
        _remaining-=absorbed;
        damage.Amount=raw-absorbed;damage.BonusAmount=0;damage.DamageMultiplier=1;
        if(_remaining<=.0001f)Owner.RemoveAbility(this);
    }
}
