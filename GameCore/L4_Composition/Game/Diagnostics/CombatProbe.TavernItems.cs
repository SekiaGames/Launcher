using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    private static void CheckTavernAndItems()
    {
        GD.Print("==== 酒馆剩余与法球自检 ====");

        CheckShadowMeld();
        CheckRegenerationAura();
        CheckVolcano();
        CheckStormEarthFire();
        CheckDrunkenCombo();
        CheckHowlOfTerror();
        CheckCleavingAttack();
        CheckDoom();
        CheckStampede();
        CheckOrbItems();
        CheckItemPassives();
    }

    private static void CheckShadowMeld()
    {
        UnitType archerType = UnitFactory.Get("earc");
        if (archerType == null) return;

        var world = new World(null, null) { IsNight = true };
        Unit archer = ProbeUnit(world, "earc", 0, Vector3.Zero, "Ashm");

        for (int tick = 0; tick < 60; tick++) world.TickAbilities();
        Verify("影遁：夜间静止 3 秒后隐身", 1f, archer.State.Invisible ? 1f : 0f, 0f);

        archer.MoveTarget = new Vector3(400f, 0f, 0f);
        world.TickAbilities();
        Verify("影遁：一移动就现形", 0f, archer.State.Invisible ? 1f : 0f, 0f);

        archer.MoveTarget = null;
        for (int tick = 0; tick < 60; tick++) world.TickAbilities();
        world.IsNight = false;
        world.TickAbilities();
        Verify("影遁：白天不隐身", 0f, archer.State.Invisible ? 1f : 0f, 0f);
    }

    private static void CheckRegenerationAura()
    {
        AbilityType regenType = AbilityCatalog.Get("Aoar");
        if (regenType == null) return;

        var world = new World(null, null);
        ProbeUnit(world, "ohwd", 0, Vector3.Zero, "Aoar");
        Unit ally = ProbeUnit(world, "ugho", 0, new Vector3(200f, 0f, 0f));
        ally.Hp = 100f;

        world.RefreshAuras();
        Verify("再生光环百分比=DataA", regenType.DataAt(1, 'A'), ally.AuraHpRegenPct, 0.0001f);

        float before = ally.Hp;
        for (int tick = 0; tick < 20; tick++) world.TickRegeneration();
        float expected = (ally.Type.RegenHp + ally.MaxHp * regenType.DataAt(1, 'A')) * 1f;
        Verify("再生光环按最大生命回血", expected, ally.Hp - before, 0.5f);
    }

    private static void CheckVolcano()
    {
        AbilityType volcanoType = AbilityCatalog.Get("ANvc");
        if (volcanoType == null) return;

        var world = new World(null, null);
        Unit caster = ProbeUnit(world, "Hpal", 0, Vector3.Zero, "ANvc");
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(460f, 0f, 0f));

        Ability volcano = caster.FindAbility("ANvc");
        Vector3 point = new Vector3(500f, 0f, 0f);
        bool cast = world.TryCast(caster, volcano, null, point);
        Verify("火山开始引导", 1f, cast ? 1f : 0f, 0f);

        float before = victim.Hp;
        for (int tick = 0; tick < 110; tick++) world.TickChannels();
        Verify("火山第一波=DataE", volcanoType.DataAt(1, 'E'), before - victim.Hp, 2f);
    }

    private static void CheckStormEarthFire()
    {
        AbilityType stormType = AbilityCatalog.Get("Acef");
        if (stormType == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit caster = ProbeUnit(world, "Nsjs", 0, Vector3.Zero, "Acef");

        Ability storm = caster.FindAbility("Acef");
        bool cast = world.TryCast(caster, storm, null, caster.Position);

        int elemental = 0;
        Unit first = null;
        foreach (Unit unit in world.Units)
        {
            if (unit.Type == null || !unit.Type.Id.StartsWith("npn", StringComparison.OrdinalIgnoreCase)) continue;
            elemental++;
            first ??= unit;
        }
        Verify("风暴大地火焰施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("风暴大地火焰召出三只", 3f, elemental, 0f);
        Verify("三只各自有召唤时长", stormType.DurationAt(1), first?.SummonRemaining ?? 0f, 0.1f);
    }

    private static void CheckDrunkenCombo()
    {
        AbilityType hazeType = AbilityCatalog.Get("Acdh");
        AbilityType fireType = AbilityCatalog.Get("ANcf");
        if (hazeType == null || fireType == null) return;

        var world = new World(null, null);
        Unit caster = ProbeUnit(world, "Nsjs", 0, Vector3.Zero, "Acdh", "ANcf");
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));

        float baseSpeed = World.SpeedOf(victim);
        Ability haze = caster.FindAbility("Acdh");
        world.TryCast(caster, haze, victim, victim.Position);
        Verify("醉酒云雾未命中=DataB", hazeType.DataAt(1, 'B'), victim.AttackMissChance, 0.001f);
        // 客户端移速下限 150（NativeMovementSpeed.Limit），减速再狠也被托住。
        Verify("醉酒云雾减速=DataC", Mathf.Max(baseSpeed * (1f - hazeType.DataAt(1, 'C')), MiscRules.MinUnitSpeed), World.SpeedOf(victim), 0.5f);

        float before = victim.Hp;
        Ability fire = caster.FindAbility("ANcf");
        bool cast = world.TryCast(caster, fire, victim, victim.Position);
        float expected = War3GameplayConfig.Resolve(fireType.DataAt(1, 'A'), AttackType.Spells,
            victim.Type.DefenseType, DamageType.Magic, 0f, false, false);
        Verify("火焰吐息施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("火焰吐息命中伤害=DataA", expected, before - victim.Hp, 3f);
        Verify("火焰吐息点燃", 1f, victim.FindBuff<PeriodicDamageBuff>() != null ? 1f : 0f, 0f);
    }

    private static void CheckHowlOfTerror()
    {
        AbilityType howlType = AbilityCatalog.Get("ANht");
        if (howlType == null) return;

        var world = new World(null, null);
        Unit caster = ProbeUnit(world, "Nmag", 0, Vector3.Zero, "ANht");
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));

        Ability howl = caster.FindAbility("ANht");
        bool cast = world.TryCast(caster, howl, null, caster.Position);
        Verify("恐惧嚎叫施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("恐惧嚎叫减攻=DataA", -howlType.DataAt(1, 'A'), victim.BonusDamagePct, 0.001f);
    }

    private static void CheckCleavingAttack()
    {
        AbilityType cleaveType = AbilityCatalog.Get("ANca");
        if (cleaveType == null) return;

        var world = new World(null, null);
        Unit caster = ProbeUnit(world, "Nmag", 0, Vector3.Zero, "ANca");
        Unit first = ProbeUnit(world, "hfoo", 1, new Vector3(100f, 0f, 0f));
        Unit second = ProbeUnit(world, "hfoo", 1, new Vector3(260f, 0f, 0f));

        var info = new DamageData { Source = caster, Target = first, Amount = 100f, IsAttack = true };
        float before = second.Hp;
        caster.FindAbility("ANca").OnAttackLanded(world, info);

        float expected = War3GameplayConfig.Resolve(100f * cleaveType.DataAt(1, 'A'), AttackType.Normal,
            second.Type.DefenseType, DamageType.Universal, 0f, false, false);
        Verify("分裂攻击溅射=DataA×伤害", before - expected, second.Hp, 0.5f);
    }

    private static void CheckDoom()
    {
        AbilityType doomType = AbilityCatalog.Get("ANdo");
        if (doomType == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit caster = ProbeUnit(world, "Nmag", 0, Vector3.Zero, "ANdo");
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));

        Ability doom = caster.FindAbility("ANdo");
        bool cast = world.TryCast(caster, doom, victim, victim.Position);
        Verify("末日施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("末日挂上持续伤害", 1f, victim.FindBuff<DoomBuff>() != null ? 1f : 0f, 0f);

        float before = victim.Hp;
        for (int tick = 0; tick < 24; tick++) world.TickAbilities();
        Verify("末日每秒=DataA", doomType.DataAt(1, 'A'), before - victim.Hp, 5f);

        world.KillUnit(victim, caster);
        int demons = 0;
        foreach (Unit unit in world.Units)
        {
            if (string.Equals(unit.Type?.Id, "nba2", StringComparison.OrdinalIgnoreCase)) demons++;
        }
        Verify("末日死亡召出恶魔", 1f, demons >= 1 ? 1f : 0f, 0f);
    }

    private static void CheckStampede()
    {
        AbilityType stampedeType = AbilityCatalog.Get("Arsp");
        if (stampedeType == null) return;

        var world = new World(null, null);
        Unit caster = ProbeUnit(world, "Orex", 0, Vector3.Zero, "Arsp");
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));

        Ability stampede = caster.FindAbility("Arsp");
        bool cast = world.TryCast(caster, stampede, null, new Vector3(320f, 0f, 0f));
        Verify("兽群狂奔开始引导", 1f, cast ? 1f : 0f, 0f);

        float before = victim.Hp;
        for (int tick = 0; tick < 22; tick++) world.TickChannels();
        Verify("兽群狂奔每 5 波结算一次", stampedeType.DataAt(1, 'B'), before - victim.Hp, 5f);
    }

    private static void CheckOrbItems()
    {
        ItemType fireOrb = ItemCatalog.Get("ofir");
        ItemType frostOrb = ItemCatalog.Get("ofro");
        if (fireOrb == null || frostOrb == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);

        var item = new Item { Type = fireOrb };
        world.ApplyItemBonus(hero, item);
        Verify("火焰之球附加伤害=DataA", 5f, hero.OrbDamage, 0.01f);
        Verify("火焰之球标记为火焰法球", (float)OrbEffect.Fire, (float)hero.OrbEffectKind, 0f);

        Unit target = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));
        Unit near = ProbeUnit(world, "hfoo", 1, new Vector3(260f, 0f, 0f));

        float before = near.Hp;
        float splash = War3GameplayConfig.Resolve(5f, AttackType.Spells, near.Type.DefenseType,
            DamageType.Magic, 0f, false, false);
        var info = new DamageData { Source = hero, Target = target, Amount = 30f, IsAttack = true };
        world.ApplyOrbEffect(info);
        Verify("火焰法球溅射到旁边的人", before - splash, near.Hp, 0.5f);

        world.RemoveItemBonus(hero, item);
        Verify("法球卸下后清空", 0f, hero.OrbDamage, 0f);

        var frost = new Item { Type = frostOrb };
        world.ApplyItemBonus(hero, frost);
        world.ApplyOrbEffect(new DamageData { Source = hero, Target = target, Amount = 10f, IsAttack = true });
        Verify("霜冻法球减速", 0.3f, target.SlowMovePct, 0.001f);
    }

    private static void CheckItemPassives()
    {
        ItemBonus evasion = ItemBonus.ForAbility(AbilityCatalog.Get("AIev"));
        ItemBonus mana = ItemBonus.ForAbility(AbilityCatalog.Get("AIrn"));
        ItemBonus sight = ItemBonus.ForAbility(AbilityCatalog.Get("AIsi"));
        ItemBonus attackSpeed = ItemBonus.ForAbility(AbilityCatalog.Get("AIsx"));

        Verify("闪避物品=DataA", 0.15f, evasion.EvasionPct, 0.001f);
        Verify("回蓝物品=DataA", 0.25f, mana.ManaRegen, 0.001f);
        Verify("视野物品=DataA", 500f, sight.Sight, 0.001f);
        Verify("攻速物品=DataA", 0.15f, attackSpeed.AttackSpeedPct, 0.001f);
    }
}