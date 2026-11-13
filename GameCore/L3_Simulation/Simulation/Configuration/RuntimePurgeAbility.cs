namespace OpenWC3.Game;

/// <summary>
/// Purge（Aprg）：驱散魔法，DataC 伤害召唤物、DataF 消耗法力。
/// DataA/B 为移速/攻速恢复阶段数，DataD/E 为普通/英雄固定时间。
/// </summary>
public sealed class RuntimePurgeAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.Unit;
    /// <summary>DataD：普通单位固定时间。</summary>
    public float UnitPauseDuration { get; private set; }
    /// <summary>DataE：英雄固定时间。</summary>
    public float HeroPauseDuration { get; private set; }
    public float SummonedUnitDamage { get; private set; }
    public float MoveSpeedFactor { get; private set; }
    public float AttackSpeedFactor { get; private set; }
    public float MovementUpdateCount=>MoveSpeedFactor;
    public float AttackUpdateCount=>AttackSpeedFactor;
    public float ManaLoss { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string PurgeBuffRawcode { get; private set; }="";

    public void RefreshPurgeData()
    {
        int level=Math.Max(1,Level);
        UnitPauseDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataD",level,0));
        HeroPauseDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataE",level,0));
        SummonedUnitDamage=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,0));
        MoveSpeedFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,0));
        AttackSpeedFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataB",level,0));
        ManaLoss=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataF",level,0));
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        PurgeBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(PurgeBuffRawcode.Length>4)PurgeBuffRawcode=PurgeBuffRawcode[..4];
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if(_allowedTargets==TargetFlags.None)
            _allowedTargets=TargetFlags.Ground|TargetFlags.Air
                |TargetRules.Relations
                |TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshPurgeData();
    public override void OnLevelDown()=>RefreshPurgeData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>
        target?.Alive==true&&target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);

    public bool TryAcquireComputerTarget(out Unit target,out Vec2 point)
    {
        target=null;point=default;
        World world=Owner?.World;
        if(world==null||IsDisabled||Level<=0||CdRemaining>0||Owner.Mana<ManaCost)return false;
        int Score(Unit unit)
        {
            bool enemy=world.AreEnemies(Owner,unit);
            bool usefulMagic=unit.Abilities.OfType<Buff>()
                .Any(buff=>buff.Magic&&buff.AutoDispel&&buff.Positive==enemy);
            if(enemy&&(unit.IsIllusion||unit.Identity.Is(Classification.Summoned)))return 4;
            if(usefulMagic)return 3;
            if(enemy&&!unit.Abilities.OfType<RuntimePurgeBuff>().Any()
                &&(Owner.CurrentTarget()!=null||unit.CurrentTarget()!=null))return 1;
            return 0;
        }
        target=RuntimeSpatialUnitQuery.InCastRange(Owner,CastRange).Where(unit=>Owner.DistanceTo(unit)<=CastRange
                &&Score(unit)>0&&CheckCanUse(unit,unit.Pos))
            .OrderByDescending(Score).ThenBy(unit=>Owner.DistanceTo(unit))
            .ThenBy(unit=>unit.SimulationId).FirstOrDefault();
        point=target?.Pos??default;return target!=null;
    }

    public override void OnEffect(Unit target,Vec2 point)
    {
        if(target?.Alive!=true)return;
        foreach(Buff buff in target.Abilities.OfType<Buff>()
                    .Where(buff=>buff.AutoDispel&&buff.Magic).ToArray())
            target.RemoveAbility(buff);
        if((target.IsIllusion||(target.Classifications&Classification.Summoned)!=0)
            &&Owner.World.AreEnemies(Owner,target)
            &&SummonedUnitDamage>0)
            target.TakeDamage(new DamageData
            {
                Source=Owner,
                Amount=SummonedUnitDamage,
                AttackType=AttackType.Spells,
                DamageType=DamageType.Magic,
            });
        if(ManaLoss>0)target.Mana=Math.Max(0,target.Mana-ManaLoss);
        bool hostile=Owner?.World?.AreEnemies(Owner,target)==true;
        if(!hostile||!target.Alive)return;
        float duration=target.UsesHeroSpellRules?HeroDuration:NormalDuration;
        if(duration<=0)return;
        foreach(var previous in target.Abilities.OfType<RuntimePurgeBuff>()
            .Where(buff=>buff.Rawcode.Equals(PurgeBuffRawcode,StringComparison.OrdinalIgnoreCase)).ToArray())
            target.RemoveAbility(previous);
        var purge=new RuntimePurgeBuff(MoveSpeedFactor,AttackSpeedFactor,duration,
            target.UsesHeroSpellRules?HeroPauseDuration:UnitPauseDuration);
        BuffTypeData data=objects?.GetBuff(PurgeBuffRawcode);
        if(data!=null)purge.BindBuffObjectData(PurgeBuffRawcode,data,
            Math.Max(1,Level),Owner,this);
        else purge.BindObjectData(PurgeBuffRawcode,ObjectData,
            Math.Max(1,Level));
        purge.Duration=duration+.01f;
        target.AddAbility(purge);
    }
}

/// <summary>
/// 原版 5400e0 初始化完全减速，53e920/53e740 按固定阶段恢复。
/// 移动固定独立于攻击/施法，不使用 Stun。
/// </summary>
public class RuntimePurgeBuff(
    float moveSpeedFactor,float attackSpeedFactor,float slowDuration,float pauseDuration=0) : Buff
{
    float _appliedMove;
    float _appliedAttack;
    float _elapsed,_moveClock,_attackClock;
    float _moveReduction,_attackReduction;
    bool _rooted;
    public override bool Positive=>false;
    public override bool Magic=>false;
    public override bool Physical=>false;
    public override bool AutoDispel=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Purge")??"Purge";

    protected override void OnBuffAdd()
    {
        _moveReduction=moveSpeedFactor>0?1:0;
        _attackReduction=attackSpeedFactor>0?1:0;
        if(pauseDuration>0)
        {
            _rooted=true;Owner.AddState(StateModType.Snare);
            Owner.World?.MovementBatch.Cancel(Owner,null);
        }
        ApplySlow();
    }

    public override void OnTick(float dt)
    {
        dt=Math.Max(0,dt);_elapsed+=dt;
        if(_rooted&&_elapsed>=pauseDuration)
        {Owner.RemoveState(StateModType.Snare);_rooted=false;}
        Recover(ref _moveClock,ref _moveReduction,moveSpeedFactor,dt);
        Recover(ref _attackClock,ref _attackReduction,attackSpeedFactor,dt);
        ApplySlow();
        base.OnTick(dt);
    }

    void Recover(ref float clock,ref float reduction,float stages,float dt)
    {
        if(stages<=0||slowDuration<=0)return;
        float step=1f/stages,period=slowDuration/ stages;
        clock+=dt;
        while(clock>=period)
        {clock-=period;if(Math.Abs(reduction)>1e-6f)reduction-=step;}
    }

    void ApplySlow()
    {
        Owner.SpeedBonus+=_appliedMove;
        Owner.AttackSpeedBonusPct+=_appliedAttack;
        _appliedMove=Owner.MoveSpeedBase*_moveReduction;
        _appliedAttack=_attackReduction;
        Owner.SpeedBonus-=_appliedMove;
        Owner.AttackSpeedBonusPct-=_appliedAttack;
    }

    protected override void OnBuffRemove()
    {
        if(_rooted){Owner.RemoveState(StateModType.Snare);_rooted=false;}
        Owner.SpeedBonus+=_appliedMove;
        Owner.AttackSpeedBonusPct+=_appliedAttack;
        _appliedMove=0;
        _appliedAttack=0;
    }
}
