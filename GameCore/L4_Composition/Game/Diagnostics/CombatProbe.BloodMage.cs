using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>
    /// 对战剩余英雄技能：放逐 / 烈焰风暴 / 吸魔 / 凤凰（含变蛋）/ 蝗群 / 地震 / 灼热之箭 / 霜甲自动施法。
    /// 数值全部读物编，断言写成「表里多少就多少」。
    /// </summary>
    private static void CheckBloodMageAndUltimates()
    {
        GD.Print("==== 血法师与剩余终极自检 ====");

        CheckBanish();
        CheckFlameStrike();
        CheckSiphonMana();
        CheckPhoenix();
        CheckLocustSwarm();
        CheckEarthquake();
        CheckSearingArrows();
        CheckFrostArmorAutocast();
    }

    private static void CheckBanish()
    {
        UnitType mageType = UnitCatalog.Get("Hblm");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        AbilityType banishType = AbilityCatalog.Get("AHbn");
        if (mageType == null || footmanType == null || banishType == null) return;

        var world = new World(null, null);
        Unit mage = MakeHero(mageType, 0, 3, world);
        world.Units.Add(mage);
        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = new Vector3(120f, 0f, 0f);
        world.Units.Add(footman);

        Ability banish = mage.GiveAbility("AHbn", 1);
        bool cast = world.TryCast(mage, banish, footman, footman.Position);
        Verify("放逐施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("放逐目标虚无", 1f, footman.State.Ethereal ? 1f : 0f, 0f);
        Verify("放逐移速减少=DataA", banishType.DataAt(1, 'A'), footman.SlowMovePct, 0.001f);
        Verify("放逐时长=Dur", banishType.DurationAt(1), footman.Buffs[0].Remaining, 0.01f);

        footman.ApplySlow(0.3f, 0.3f, 1f);
        for (int index = 0; index < 30; index++) world.TickAbilities();
        Verify("放逐减速有独立槽", banishType.DataAt(1, 'A'), footman.SlowMovePct, 0.01f);
        GD.Print($"[自检] 放逐 步兵 虚无={footman.State.Ethereal} 减速 {footman.SlowMovePct:P0}"
            + $" 时长 {footman.Buffs[0].Remaining:0.0}s");
    }

    private static void CheckFlameStrike()
    {
        UnitType mageType = UnitCatalog.Get("Hblm");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        AbilityType strikeType = AbilityCatalog.Get("AHfs");
        if (mageType == null || footmanType == null || strikeType == null) return;

        var world = new World(null, null);
        Unit mage = MakeHero(mageType, 0, 3, world);
        world.Units.Add(mage);
        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = new Vector3(180f, 0f, 0f);
        footman.MaxHp = 2000f;
        footman.Hp = 2000f;
        world.Units.Add(footman);

        Ability strike = mage.GiveAbility("AHfs", 1);
        bool cast = world.TryCast(mage, strike, null, footman.Position);
        Verify("烈焰风暴施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("烈焰风暴挂上地面效果", 1f, world.GroundSpellCount, 0f);

        float before = footman.Hp;
        for (int tick = 0; tick < 60; tick++) world.Advance();
        float fullPhaseDamage = before - footman.Hp;

        for (int tick = 0; tick < 130; tick++) world.Advance();
        Verify("烈焰风暴满伤阶段有伤害", 1f, fullPhaseDamage > 10f ? 1f : 0f, 0f);
        Verify("烈焰风暴 9 秒后结束", 0f, world.GroundSpellCount, 0f);
        GD.Print($"[自检] 烈焰风暴 3 秒内造成 {fullPhaseDamage:0} 点，总时长 {strikeType.DurationAt(1):0}s");
    }

    private static void CheckSiphonMana()
    {
        UnitType mageType = UnitCatalog.Get("Hblm");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        AbilityType drainType = AbilityCatalog.Get("AHdr");
        if (mageType == null || footmanType == null || drainType == null) return;

        var world = new World(null, null);
        Unit mage = MakeHero(mageType, 0, 3, world);
        mage.Intelligence = 0;
        mage.MaxMana = 200f;
        mage.Mana = 50f;
        world.Units.Add(mage);

        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = new Vector3(150f, 0f, 0f);
        footman.MaxMana = 200f;
        footman.Mana = 100f;
        world.Units.Add(footman);

        Ability drain = mage.GiveAbility("AHdr", 1);
        bool cast = world.TryCast(mage, drain, footman, footman.Position);
        Verify("吸魔施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("吸魔进入引导", 1f, world.ChannelCount, 0f);

        for (int tick = 0; tick < 60; tick++) world.Advance();
        float drained = 100f - footman.Mana;
        Verify("吸魔每跳=DataB（3 秒 3 跳）", drainType.DataAt(1, 'B') * 3f, drained, 8f);
        Verify("吸魔转给施法者", 50f - drainType.CostAt(1) + drained, mage.Mana, 8f);
        GD.Print($"[自检] 吸魔 3 秒抽 {drained:0} 点，施法者 {mage.Mana:0} 点"
            + $"（每跳 {drainType.DataAt(1, 'B'):0}，间隔 {drainType.DataAt(1, 'C'):0}s）");
    }

    private static void CheckPhoenix()
    {
        UnitType mageType = UnitCatalog.Get("Hblm");
        UnitType phoenixType = UnitCatalog.Get("hphx");
        UnitType eggType = UnitCatalog.Get("hpxe");
        if (mageType == null || phoenixType == null || eggType == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitCatalog.Get(id), caster.Player);
        Unit mage = MakeHero(mageType, 0, 6, world);
        world.Units.Add(mage);

        Ability phoenix = mage.GiveAbility("AHpx", 1);
        bool cast = world.TryCast(mage, phoenix, null, mage.Position);
        Verify("凤凰施放成功", 1f, cast ? 1f : 0f, 0f);

        Unit bird = null;
        foreach (Unit unit in world.Units)
        {
            if (unit != mage && unit.Type != null && unit.Type.Id == "hphx") bird = unit;
        }
        Verify("凤凰已召出", 1f, bird != null ? 1f : 0f, 0f);
        if (bird == null) return;

        bird.GiveAbility("Aphx", 1);
        world.DealDamage(World.MakeAbilityDamage(mage, bird, 9999f, AttackType.Spells, DamageType.Magic));
        Verify("凤凰阵亡变蛋", 1f, bird.Type.Id == "hpxe" ? 1f : 0f, 0f);
        Verify("凤凰蛋不死亡", 1f, bird.Alive ? 1f : 0f, 0f);
        Verify("凤凰蛋生命=蛋上限", eggType.Hp, bird.Hp, 0.5f);
        Verify("凤凰蛋排队孵化", 1f, world.PendingPhoenixEggCount, 0f);

        for (int tick = 0; tick < 210; tick++) world.Advance();
        Verify("10 秒后孵化回凤凰", 1f, bird.Type.Id == "hphx" ? 1f : 0f, 0f);
        Verify("孵化后满血附近", phoenixType.Hp, bird.Hp, 60f);
        GD.Print($"[自检] 凤凰 {phoenixType.Hp:0} 血 → 蛋 {eggType.Hp:0} 血 → 孵回 {bird.Hp:0} 血");
    }

    private static void CheckLocustSwarm()
    {
        UnitType lordType = UnitCatalog.Get("Ucrl");
        AbilityType swarmType = AbilityCatalog.Get("AUls");
        if (lordType == null || swarmType == null) return;
        if (UnitCatalog.Get("uloc") == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitCatalog.Get(id), caster.Player);
        Unit lord = MakeHero(lordType, 0, 6, world);
        world.Units.Add(lord);

        Ability swarm = lord.GiveAbility("AUls", 1);
        bool cast = world.TryCast(lord, swarm, null, lord.Position);
        Verify("蝗群施放成功", 1f, cast ? 1f : 0f, 0f);

        for (int tick = 0; tick < 90; tick++) world.Advance();
        int locusts = 0;
        foreach (Unit unit in world.Units)
        {
            if (unit.Type != null && unit.Type.Id == "uloc") locusts++;
        }
        Verify("蝗群数量=DataA", swarmType.DataAt(1, 'A'), locusts, 1f);
        Verify("蝗群放出完毕", 0f, world.GroundSpellCount, 0f);

        Unit agentUnit = null;
        foreach (Unit unit in world.Units)
        {
            if (unit.Type == null || unit.Type.Id != "uloc") continue;
            if (unit.Abilities.Exists(ability => ability is LocustAgent)) agentUnit = unit;
        }
        if (agentUnit != null)
        {
            lord.Hp = lord.MaxHp * 0.5f;
            float before = lord.Hp;
            LocustAgent agent = (LocustAgent)agentUnit.Abilities.Find(ability => ability is LocustAgent);
            var hit = new DamageInfo { Source = agentUnit, Target = lord, Amount = 50f, Raw = 50f };
            agent.OnAttackLanded(world, hit);
            world.KillUnit(agentUnit, null);
            Verify("蝗群代理回血=伤害×DataD", 50f * swarmType.DataAt(1, 'D'), lord.Hp - before, 0.5f);
        }
        GD.Print($"[自检] 蝗群 {locusts} 只，存货 {swarmType.DurationAt(1):0}s，放出间隔 {swarmType.DataAt(1, 'B'):0.00}s，回血系数 DataD={swarmType.DataAt(1, 'D'):0.00}");
    }

    private static void CheckEarthquake()
    {
        UnitType seerType = UnitCatalog.Get("Ofar");
        UnitType towerType = UnitCatalog.Get("hwtw");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        AbilityType quakeType = AbilityCatalog.Get("AOeq");
        if (seerType == null || towerType == null || footmanType == null || quakeType == null) return;

        var world = new World(null, null);
        Unit seer = MakeHero(seerType, 0, 6, world);
        world.Units.Add(seer);

        var spot = new Vector3(400f, 0f, 0f);
        Unit tower = MakeUnit(towerType, 1);
        tower.Position = spot;
        world.Units.Add(tower);

        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = new Vector3(460f, 0f, 0f);
        world.Units.Add(footman);

        Ability quake = seer.GiveAbility("AOeq", 1);
        bool cast = world.TryCast(seer, quake, null, spot);
        Verify("地震施放成功", 1f, cast ? 1f : 0f, 0f);

        float before = tower.Hp;
        for (int tick = 0; tick < 50; tick++) world.Advance();
        float buildingDamage = before - tower.Hp;
        Verify("地震每秒拆塔=DataB", quakeType.DataAt(1, 'B') * 2f, buildingDamage, 12f);
        Verify("地震减速=DataC", quakeType.DataAt(1, 'C'), footman.SlowMovePct, 0.01f);
        GD.Print($"[自检] 地震 2 秒拆塔 {buildingDamage:0} 点，地面减速 {footman.SlowMovePct:P0}"
            + $"（半径 {quakeType.AreaAt(1):0}，时长 {quakeType.DurationAt(1):0}s）");
    }

    private static void CheckSearingArrows()
    {
        UnitType priestessType = UnitCatalog.Get("Etyr");
        AbilityType arrowType = AbilityCatalog.Get("AHfa");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        if (priestessType == null || arrowType == null || footmanType == null) return;

        var world = new World(null, null);
        Unit priestess = MakeHero(priestessType, 0, 3, world);
        world.Units.Add(priestess);
        Unit footman = MakeUnit(footmanType, 1);
        world.Units.Add(footman);

        Ability arrows = priestess.GiveAbility("AHfa", 1);
        priestess.Mana = 100f;
        string arrowArt = world.PrepareArrowMissile(priestess, footman);
        float modified = world.ApplyAttackModifiers(priestess, footman, 50f);
        Verify("灼热之箭附加伤害=DataA", 50f + arrowType.DataAt(1, 'A'), modified, 0.01f);
        Verify("灼热之箭扣蓝=Cost", 100f - arrowType.CostAt(1), priestess.Mana, 0.01f);
        Verify("灼热之箭换成自己的箭模型", 1f, arrowArt != null ? 1f : 0f, 0f);

        priestess.Mana = 0f;
        world.PrepareArrowMissile(priestess, footman);
        float plain = world.ApplyAttackModifiers(priestess, footman, 50f);
        Verify("没蓝时只是普通一箭", 50f, plain, 0.01f);

        arrows.AutoCastEnabled = false;
        priestess.Mana = 100f;
        string offArt = world.PrepareArrowMissile(priestess, footman);
        float offDamage = world.ApplyAttackModifiers(priestess, footman, 50f);
        Verify("关掉自动施法不扣蓝", 100f, priestess.Mana, 0.01f);
        Verify("关掉自动施法不附加伤害", 50f, offDamage, 0.01f);
        Verify("关掉自动施法不换箭模型", 1f, offArt == null ? 1f : 0f, 0f);
        arrows.AutoCastEnabled = true;
        Verify("灼热之箭支持开关", 1f, arrows.SupportsAutoCast ? 1f : 0f, 0f);
        GD.Print($"[自检] 灼热之箭 +{arrowType.DataAt(1, 'A'):0} 伤害 / 每箭 {arrowType.CostAt(1):0} 蓝，"
            + $"蓝不够时 {plain:0} 伤害，开关可关");
    }

    private static void CheckFrostArmorAutocast()
    {
        Verify("霜冻护甲自动施法码 AUfu 已实现", 1f, SkillRegistry.IsImplemented("AUfu") ? 1f : 0f, 0f);
    }
}