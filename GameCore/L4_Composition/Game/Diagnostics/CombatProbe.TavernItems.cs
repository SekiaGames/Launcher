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
        CheckSpellArtLoops();
    }

    /// <summary>烈焰风暴余烬、云雾雾团、季风雨幕/落雷、哨兵猫头鹰、火山喷发/熔岩弹。</summary>
    private static void CheckSpellArtLoops()
    {
        Verify("烈焰风暴预载美术", 1f, PreloadArtCount("AHfs") > 0 ? 1f : 0f, 0f);
        Verify("云雾预载美术", 1f, PreloadArtCount("Aclf") > 0 ? 1f : 0f, 0f);
        Verify("季风预载美术", 2f, PreloadArtCount("ACmo"), 0f);
        Verify("哨兵预载美术", 1f, PreloadArtCount("Aesn") > 0 ? 1f : 0f, 0f);
        Verify("火山预载美术", 2f, PreloadArtCount("ANvc"), 0f);

        var fireWorld = new World(null, null);
        Unit mage = ProbeUnit(fireWorld, "hfoo", 0, Vector3.Zero, "AHfs");
        bool fireCast = fireWorld.TryCast(mage, mage.FindAbility("AHfs"), null, new Vector3(300f, 0f, 0f));
        Verify("烈焰风暴喷发模型", 1f, fireCast && VisualCount(fireWorld, "FlameStrike") > 0 ? 1f : 0f, 0f);
        fireWorld.TickGroundSpells();
        Verify("烈焰风暴余烬", 10f, VisualCount(fireWorld, "FlameStrikeEmbers"), 0f);

        var fogWorld = new World(null, null);
        Unit dragon = ProbeUnit(fogWorld, "hfoo", 0, Vector3.Zero, "Aclf");
        bool fogCast = fogWorld.TryCast(dragon, dragon.FindAbility("Aclf"), null, new Vector3(300f, 0f, 0f));
        Verify("云雾雾团模型", 1f, fogCast && VisualCount(fogWorld, "CloudOfFog") > 0 ? 1f : 0f, 0f);

        var rainWorld = new World(null, null);
        Unit warlock = ProbeUnit(rainWorld, "hfoo", 0, Vector3.Zero, "ACmo");
        Unit soaked = ProbeUnit(rainWorld, "ogru", 1, new Vector3(300f, 0f, 0f));
        soaked.Hp = soaked.MaxHp = 100000f;
        bool rainCast = rainWorld.TryCast(warlock, warlock.FindAbility("ACmo"), null, soaked.Position);
        Verify("季风雨幕模型", 1f, rainCast && VisualCount(rainWorld, "MonsoonRain") > 0 ? 1f : 0f, 0f);
        for (int tick = 0; tick < 40; tick++) rainWorld.TickChannels();
        Verify("季风落雷模型", 1f, VisualCount(rainWorld, "MonsoonBoltTarget") > 0 ? 1f : 0f, 0f);
        rainWorld.CancelChannel(warlock);
        Verify("季风断引导雨幕离场", 0f, VisualCount(rainWorld, "MonsoonRain"), 0f);

        var owlWorld = new World(null, null);
        Unit huntress = ProbeUnit(owlWorld, "hfoo", 0, Vector3.Zero, "Aesn");
        bool owlCast = owlWorld.TryCast(huntress, huntress.FindAbility("Aesn"), null, new Vector3(300f, 0f, 0f));
        Verify("哨兵猫头鹰模型", 1f, owlCast && VisualCount(owlWorld, "Owl.mdl") > 0 ? 1f : 0f, 0f);

        var volcanoWorld = new World(null, null);
        Unit fireLord = ProbeUnit(volcanoWorld, "hfoo", 0, Vector3.Zero, "ANvc");
        bool volcanoCast = volcanoWorld.TryCast(fireLord, fireLord.FindAbility("ANvc"), null, new Vector3(300f, 0f, 0f));
        Verify("火山喷发模型", 1f, volcanoCast && VisualCount(volcanoWorld, "Volcano") > 0 ? 1f : 0f, 0f);
        for (int tick = 0; tick < 110; tick++) volcanoWorld.TickChannels();
        Verify("火山熔岩弹", 1f, MissileCount(volcanoWorld, "VolcanoMissile") > 0 ? 1f : 0f, 0f);
        volcanoWorld.CancelChannel(fireLord);
        Verify("火山断引导喷发离场", 0f, VisualCount(volcanoWorld, "Volcano.mdl"), 0f);
    }

    static int PreloadArtCount(string abilityId)
    {
        AbilityType type = AbilityCatalog.Get(abilityId);
        if (type == null) return 0;
        Ability ability = RuntimeAbilityFactory.Create(type);
        if (ability == null) return 0;
        ability.BindObjectData(type.Id, new AbilityTypeObjectData(type), 1);
        return ability.RuntimePreloadArts.Count;
    }

    static int VisualCount(World world, string fragment)
    {
        int count = 0;
        foreach (Effect effect in world.Effects)
        {
            if (effect is not SpecialEffect special || special.Destroyed) continue;
            if (special.VisualModelPath.Contains(fragment, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    static int MissileCount(World world, string fragment)
    {
        int count = 0;
        foreach (Effect effect in world.Effects)
        {
            if (effect is MissileEffect missile
                && missile.MissileArt.Contains(fragment, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
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

        world.TickAbilities();
        Verify("再生光环百分比=DataA", ally.MaxHp * regenType.DataAt(1, 'A'), ally.AlwaysLifeRegenBonus, 0.01f);

        float before = ally.Hp;
        for (int tick = 0; tick < 20; tick++) world.TickRegeneration();
        float expected = (ally.Type.RegenHp + ally.AlwaysLifeRegenBonus) * 1f;
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
        float expected = War3GameplayConfig.Default.Resolve(fireType.DataAt(1, 'A'), AttackType.Spells,
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
        // 走和武器系统一致的顺序：先出伤修正（分裂攻击在里头挂 _pendingCleave），再结算
        world.ApplyDamagePacketModifiers(info);
        world.DealDamage(info);

        float expected = War3GameplayConfig.Default.Resolve(100f * cleaveType.DataAt(1, 'A'), AttackType.Normal,
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
        float splash = War3GameplayConfig.Default.Resolve(5f, AttackType.Spells, near.Type.DefenseType,
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