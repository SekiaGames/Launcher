namespace OpenWC3.Game;

/// <summary>ANsi: area control mask, miss chance and signed speed modifiers.</summary>
public sealed class RuntimeSilenceAbility(War3ObjectData objects) : Ability
{
    TargetFlags _allowedTargets;
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.UnitOrPoint;
    public int AttacksPrevented { get; private set; }
    public float MissChance { get; private set; }
    public float MoveSpeedFactor { get; private set; }
    public float AttackSpeedFactor { get; private set; }
    public float NormalDuration { get; private set; }
    public float HeroDuration { get; private set; }
    public string SilenceBuffRawcode { get; private set; }="";

    public void RefreshSilenceData()
    {
        int level=Math.Max(1,Level);
        AttacksPrevented=RuntimeAbilityFactory.ReadInt(ObjectData,"DataA",level,0);
        MissChance=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataB",level,0);
        // 4e36d0 -> 4fab00/512d90 forwards DataC/DataD unchanged.
        // These are signed modifiers, not positive reduction magnitudes.
        MoveSpeedFactor=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataC",level,0);
        AttackSpeedFactor=RuntimeAbilityFactory.ReadFloat(ObjectData,"DataD",level,0);
        NormalDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"Dur",level,0));
        HeroDuration=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"HeroDur",level,NormalDuration));
        SilenceBuffRawcode=RuntimeAbilityFactory.ReadString(
            ObjectData,"BuffID",level);
        if(SilenceBuffRawcode.Length>4)
            SilenceBuffRawcode=SilenceBuffRawcode[..4];
        _allowedTargets=RuntimeAbilityFactory.ParseTargets(
            RuntimeAbilityFactory.ReadTargetRules(ObjectData,level));
        if((_allowedTargets&TargetRules.Relations)==0)
            _allowedTargets|=TargetFlags.Enemy;
        _allowedTargets|=TargetFlags.Alive|TargetFlags.Vulnerable;
    }

    public override void OnLevelUp()=>RefreshSilenceData();
    public override void OnLevelDown()=>RefreshSilenceData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null
        ||target.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true);
    public override void OnEffect(Unit target,Vec2 point)
    {
        World world=Owner?.World;
        if(world==null)return;
        Vec2 center=target?.Pos??point;
        float radius=Math.Max(0,ConfiguredAreaOfEffect);
        foreach(Unit candidate in RuntimeSpatialUnitQuery.Around(world,center,radius).Where(unit=>unit.Alive
                    &&!unit.Removed
                    &&unit.CanBeTargetedBy(Owner,_allowedTargets,useHeroSpellRules:true)
                    &&Vec2.Dist(center,unit.Pos)<=radius)
                .OrderBy(unit=>unit.SimulationId).ToArray())
        {
            foreach(RuntimeSilenceBuff existing in candidate.Abilities
                        .OfType<RuntimeSilenceBuff>()
                        .Where(buff=>buff.Rawcode.Equals(SilenceBuffRawcode,
                            StringComparison.OrdinalIgnoreCase)).ToArray())
                candidate.RemoveAbility(existing);
            var buff=new RuntimeSilenceBuff(AttacksPrevented,MissChance,MoveSpeedFactor,AttackSpeedFactor)
            {Caster=Owner,SourceAbility=this};
            BuffTypeData data=objects?.GetBuff(SilenceBuffRawcode);
            if(data!=null)buff.BindBuffObjectData(SilenceBuffRawcode,data,
                Math.Max(1,Level),Owner,this);
            else buff.BindObjectData(SilenceBuffRawcode,ObjectData,
                Math.Max(1,Level));
            buff.Duration=candidate.UsesHeroSpellRules?HeroDuration:NormalDuration;
            candidate.AddAbility(buff);
        }
    }
}

public sealed class RuntimeSilenceBuff(int attacksPrevented,float missChance=0,
    float moveSpeedFactor=0,float attackSpeedFactor=0) : Buff
{
    bool _silenced;
    bool _disarmed;
    float _appliedMove,_appliedAttack;
    public float MissChance=>missChance;
    public float MoveSpeedFactor=>moveSpeedFactor;
    public float AttackSpeedFactor=>attackSpeedFactor;
    public override bool Positive=>false;
    public override string Name=>ObjectData?.GetString(
        "Bufftip","Silence")??"Silence";
    protected override void OnBuffAdd()
    {
        _appliedMove=Owner.MoveSpeedBase*moveSpeedFactor;
        _appliedAttack=attackSpeedFactor;
        Owner.SpeedBonus+=_appliedMove;
        Owner.AttackSpeedBonusPct+=_appliedAttack;
        if((attacksPrevented&8)!=0)
        {
            Owner.AddState(StateModType.Silence);
            _silenced=true;
        }
        if((attacksPrevented&3)!=0)
        {
            Owner.AddState(StateModType.Disarm);
            _disarmed=true;
        }
    }
    protected override void OnBuffRemove()
    {
        Owner.SpeedBonus-=_appliedMove;Owner.AttackSpeedBonusPct-=_appliedAttack;
        _appliedMove=_appliedAttack=0;
        if(_silenced)Owner.RemoveState(StateModType.Silence);
        if(_disarmed)Owner.RemoveState(StateModType.Disarm);
        _silenced=false;
        _disarmed=false;
    }
    public override void ModifyOutgoingDamage(ref DamageData damage)
    {
        // Same attack-miss bridge used by Curse/Drunken Haze/Cloud of Fog.
        if(damage.IsAttack&&!damage.CannotMiss&&missChance>0&&Owner.World.Rng.NextSingle()<missChance)
            damage.DamageMultiplier=0;
    }

}
