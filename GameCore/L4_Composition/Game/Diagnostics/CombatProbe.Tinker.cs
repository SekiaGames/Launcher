using Godot;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    /// <summary>地精修补匠：粉碎（对建筑倍率）/ 工程升级（移速·攻击 + 技能升级）/ 机器人地精 / 口袋工厂。</summary>
    private static void CheckTinkerSkills()
    {
        CheckDemolish();
        CheckEngineeringUpgrade();
        CheckRoboGoblin();
        CheckSummonFactory();
    }

    private static void CheckDemolish()
    {
        AbilityType type = AbilityCatalog.Get("ANde");
        if (type == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Ntin"), 0, 3, world);
        world.Units.Add(hero);
        hero.GiveAbility("ANde", 1);

        Unit hall = ProbeUnit(world, "htow", 1, new Vector3(200f, 0f, 0f));
        Unit footman = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));
        if (hall == null || footman == null) return;

        float vsBuilding = world.ApplyAttackModifiers(hero, hall, 100f);
        float vsUnit = world.ApplyAttackModifiers(hero, footman, 100f);
        Verify("粉碎对建筑 ×DataB", 100f * type.DataAt(1, 'B'), vsBuilding, 0.5f);
        Verify("粉碎对单位不加成", 100f, vsUnit, 0.5f);
    }

    private static void CheckEngineeringUpgrade()
    {
        AbilityType type = AbilityCatalog.Get("ANeg");
        if (type == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Ntin"), 0, 3, world);
        world.Units.Add(hero);

        float baseSpeed = hero.Type.Speed;
        hero.GiveAbility("ANeg", 1);
        world.TickAbilities();

        Verify("工程升级移速 =DataA×基础", baseSpeed * type.DataAt(1, 'A'), hero.BonusMoveSpeedFlat, 0.01f);
        Verify("工程升级攻击 =DataB", type.DataAt(1, 'B'), hero.BonusDamageFlat, 0.01f);
    }

    private static void CheckRoboGoblin()
    {
        AbilityType type = AbilityCatalog.Get("ANrg");
        UnitType tinker = UnitFactory.Get("Ntin");
        if (type == null || tinker == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(tinker, 0, 3, world);
        hero.Mana = 400f;
        hero.MaxMana = 400f;
        world.Units.Add(hero);

        Ability robo = hero.GiveAbility("ANrg", 1);
        if (robo == null) return;
        float strengthBefore = hero.Strength;

        bool morphed = world.TryCast(hero, robo, null, hero.Position);
        Verify("机器人地精可用", 1f, morphed ? 1f : 0f, 0f);
        Verify("变成机器人类型", 1f, string.Equals(hero.Type?.Id, type.SummonAt(1), System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("机器人 +DataF 护甲", type.DataAt(1, 'F'), hero.BonusArmor, 0.01f);
        Verify("机器人 +DataE 力量", type.DataAt(1, 'E'), hero.Strength - strengthBefore, 0.5f);

        robo.CooldownRemaining = 0f;
        bool reverted = world.TryCast(hero, robo, null, hero.Position);
        Verify("再按一次变回人类", 1f, reverted && string.Equals(hero.Type?.Id, "Ntin", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("变回后护甲恢复", 0f, hero.BonusArmor, 0.01f);
        Verify("变回后力量恢复", 0f, hero.Strength - strengthBefore, 0.5f);
    }

    private static void CheckSummonFactory()
    {
        AbilityType type = AbilityCatalog.Get("ANsy");
        if (type == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit hero = MakeHero(UnitFactory.Get("Ntin"), 0, 3, world);
        hero.Mana = 400f;
        hero.MaxMana = 400f;
        world.Units.Add(hero);

        Ability factory = hero.GiveAbility("ANsy", 1);
        if (factory == null) return;

        int spellsBefore = world.GroundSpellCount;
        bool cast = world.TryCast(hero, factory, null, new Vector3(300f, 0f, 0f));
        Unit built = world.Units.Find(unit => string.Equals(unit.Type?.Id, type.SummonAt(1), System.StringComparison.OrdinalIgnoreCase));

        Verify("口袋工厂可用", 1f, cast ? 1f : 0f, 0f);
        Verify("工厂已立起来", 1f, built != null ? 1f : 0f, 0f);
        Verify("工厂挂上出兵计时", 1f, world.GroundSpellCount > spellsBefore ? 1f : 0f, 0f);
    }
}