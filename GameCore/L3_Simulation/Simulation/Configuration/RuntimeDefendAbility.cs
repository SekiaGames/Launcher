namespace OpenWC3.Game;

/// <summary>Footman Defend (Adef): attack-type mitigation and movement penalty.</summary>
public sealed class RuntimeDefendAbility : Ability
{
    float _appliedSpeed;
    string _activateOrder="defend",_deactivateOrder="undefend";

    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
    public bool Active { get; private set; }
    internal int? LastDirectionalOrderTick { get; set; }
    internal bool? LastDirectionalOrderActive { get; set; }
    public float RangedDamageFactor { get; private set; }
    public float MoveSpeedReduction { get; private set; }
    public float DeflectChance { get; private set; }
    public float MagicDamageFactor { get; private set; }
    public float DeflectedPierceFactor { get; private set; }
    public float DeflectedMagicFactor { get; private set; }
    public bool BenefitsFrom(AttackType type)=>type switch
    {
        AttackType.Pierce=>RangedDamageFactor<1||DeflectChance>0&&DeflectedPierceFactor<1,
        AttackType.Spells or AttackType.Magic=>MagicDamageFactor<1||DeflectChance>0&&DeflectedMagicFactor<1,
        _=>false,
    };

    public void RefreshDefendData()
    {
        int level=Math.Max(1,Level);
        RangedDamageFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataA",level,1));
        MoveSpeedReduction=Math.Clamp(RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataC",level,0),0,1);
        DeflectChance=Math.Clamp(RuntimeAbilityFactory.ReadFloat(
            ObjectData,"DataF",level,0)/100f,0,1);
        MagicDamageFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"DataE",level,1));
        DeflectedPierceFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"DataG",level,0));
        DeflectedMagicFactor=Math.Max(0,RuntimeAbilityFactory.ReadFloat(ObjectData,"DataH",level,1));
        _activateOrder=RuntimeAbilityFactory.ReadString(ObjectData,"Order",level);
        _deactivateOrder=RuntimeAbilityFactory.ReadString(ObjectData,"Unorder",level);
        if(string.IsNullOrWhiteSpace(_activateOrder))_activateOrder="defend";
        if(string.IsNullOrWhiteSpace(_deactivateOrder))_deactivateOrder="undefend";
        OrderId=Active?_deactivateOrder:_activateOrder;
    }

    public override void OnLevelUp()=>RefreshDefendData();
    public override void OnLevelDown()=>RefreshDefendData();
    public override bool CheckCanTarget(Unit target,Vec2 point)=>target==null;
    public override int ResolveCastOrderId(int requested)=>requested!=0?requested:Active?852056:852055;
    public override bool CheckCastOrder(int orderId)=>orderId==852055&&!Active
        ||orderId==852056&&Active;
    public override void OnEffect(Unit target,Vec2 point)=>SetActive(CastOrderId==852055);
    public override void OnRemove()=>SetActive(false);
    public override void OnDeath()=>SetActive(false);

    public void SetActive(bool active)
    {
        if(Active==active)return;
        Active=active;
        if(Owner!=null)
        {
            if(active)
            {
                _appliedSpeed=Owner.MoveSpeedBase*MoveSpeedReduction;
                Owner.SpeedBonus-=_appliedSpeed;
                Owner.AnimationProperties.Add("defend");
            }
            else
            {
                Owner.SpeedBonus+=_appliedSpeed;
                _appliedSpeed=0;
                Owner.AnimationProperties.Remove("defend");
            }
            Owner.AnimationRequestVersion++;
        }
        OrderId=Active?_deactivateOrder:_activateOrder;
    }

    public override int IncomingDamagePriority=>6; // 5dbeb0.
    public override void ModifyIncomingDamage(ref DamageData damage)
    {
        if(!Active)return;
        // Native attack types 2 and 0/4 map to Pierce and Spells/Magic here.
        // The mitigation callback does not inspect attack/ranged flags.
        float factor=damage.AttackType switch
        {
            AttackType.Pierce=>RangedDamageFactor,
            AttackType.Spells or AttackType.Magic=>MagicDamageFactor,
            _=>1f,
        };
        float deflected=damage.AttackType switch
        {
            AttackType.Pierce=>DeflectedPierceFactor,
            AttackType.Spells or AttackType.Magic=>DeflectedMagicFactor,
            _=>1f,
        };
        if(!IsDisabled&&!damage.CannotMiss&&damage.IsAttack&&damage.IsRanged&&deflected!=1f
            &&damage.Source!=null&&damage.Source.CanBeTargetedBy(Owner,ConfiguredTargets,useHeroSpellRules:true)
            &&DeflectChance>0&&Owner?.World?.Rng.NextSingle()<DeflectChance)
            factor=deflected;
        damage.DamageMultiplier*=factor;
    }
}
