namespace OpenWC3.Game;

/// <summary>
/// Attribute Bonus（Aamk / CAbilityAttributeModSkill）。
/// Game.dll 每次等级变化都会撤销旧三围，再读取当前等级并重新施加：
/// DataA=敏捷、DataB=智力、DataC=力量。
/// </summary>
public sealed class RuntimeAttributeModSkillAbility : Ability
{
    public override bool IsPassive => true;
    // CAbilityAttributeModSkill::+1c0 (54b3c0) registers its normal
    // button only when DataD is zero; learning registration is independent.
    public bool HidesCommandButton => RuntimeAbilityFactory.ReadFloat(
        ObjectData,"DataD",Math.Max(1,Level),0)!=0;
    Hero _hero;
    int _appliedAgility;
    int _appliedIntelligence;
    int _appliedStrength;

    public override string Name=>ObjectData?.GetString("Name",
        ObjectData.GetString("EditorName",Rawcode))??Rawcode;
    public int AgilityBonus=>_appliedAgility;
    public int IntelligenceBonus=>_appliedIntelligence;
    public int StrengthBonus=>_appliedStrength;

    public override void OnAdd()=>RefreshAttributeBonus();
    public override void OnSpawn()=>RefreshAttributeBonus();
    public override void OnLevelUp()=>RefreshAttributeBonus();
    public override void OnLevelDown()=>RefreshAttributeBonus();

    public override void OnRemove()
    {
        Apply(0,0,0);
        _hero=null;
    }

    public void RefreshAttributeBonus()
    {
        Hero hero=Owner?.Abilities.Get<Hero>();
        if(hero==null)
        {
            // abilList 在 UnitFactory 中早于 CAbilityHero 装配；OnSpawn 会重试。
            Apply(0,0,0);
            _hero=null;
            return;
        }

        if(_hero!=null&&!ReferenceEquals(_hero,hero))
            Apply(0,0,0);
        _hero=hero;

        int level=Level>0?Level:0;
        int agility=level==0?0:RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataA",level,0);
        int intelligence=level==0?0:RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataB",level,0);
        int strength=level==0?0:RuntimeAbilityFactory.ReadInt(
            ObjectData,"DataC",level,0);
        Apply(agility,intelligence,strength);
    }

    void Apply(int agility,int intelligence,int strength)
    {
        if(_hero==null)
        {
            _appliedAgility=0;
            _appliedIntelligence=0;
            _appliedStrength=0;
            return;
        }
        _hero.Agi.Bonus+=agility-_appliedAgility;
        _hero.Int.Bonus+=intelligence-_appliedIntelligence;
        _hero.Str.Bonus+=strength-_appliedStrength;
        _appliedAgility=agility;
        _appliedIntelligence=intelligence;
        _appliedStrength=strength;
        _hero.Recalculate();
    }
}
