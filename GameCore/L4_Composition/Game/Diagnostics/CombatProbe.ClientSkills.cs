using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    /// <summary>
    /// 从客户端搬来的技能：远视 / 魅惑 / 死亡之指 / 灵魂保存 / 黑暗转换 / 吸血攻击（物品）。
    /// 数值全部读物编，断言写成「表里是多少就多少」。
    /// </summary>
    private static void CheckClientSkills()
    {
        GD.Print("==== 客户端搬运技能自检 ====");

        CheckFarSight();
        CheckCharm();
        CheckFingerOfDeath();
        CheckSoulPreservation();
        CheckDarkConversion();
        CheckVampiricAttack();
    }

    private static void CheckFarSight()
    {
        UnitType farSeerType = UnitFactory.Get("Ofar");
        AbilityType sightType = AbilityCatalog.Get("AOfs");
        if (farSeerType == null || sightType == null) return;

        var world = new World(null, null);
        Unit seer = MakeHero(farSeerType, 0, 6, world);
        seer.Mana = seer.MaxMana;
        world.Units.Add(seer);

        Ability sight = seer.GiveAbility("AOfs", 1);
        var point = new Vector3(1200f, 0f, -800f);
        bool cast = sight != null && world.TryCast(seer, sight, null, point);

        Verify("远视施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("远视留下一块揭示区域", 1f, world.Reveals.Count, 0f);
        if (world.Reveals.Count > 0)
        {
            FogReveal reveal = world.Reveals[0];
            Verify("远视半径=物编 Area", sightType.AreaAt(1), reveal.Radius, 0.01f);
            Verify("远视持续=物编 Dur", sightType.DurationAt(1), reveal.Remaining, 0.2f);
            Verify("远视归属于施法者", seer.Player, reveal.Player, 0f);
            GD.Print($"[自检] 远视 半径{reveal.Radius:0} 持续{reveal.Remaining:0}s 落点({reveal.Center.X:0},{reveal.Center.Z:0})");
        }

        for (int index = 0; index < 200; index++) world.TickFogReveals();
        Verify("远视到期后揭示区域消失", 0f, world.Reveals.Count, 0f);
    }

    private static void CheckCharm()
    {
        UnitType rangerType = UnitFactory.Get("Nbrn");
        UnitType footmanType = UnitFactory.Get("hfoo");
        UnitType dragonType = UnitFactory.Get("ufro");
        AbilityType charmType = AbilityCatalog.Get("ANch");
        if (rangerType == null || footmanType == null || charmType == null) return;

        var world = new World(null, null);
        Unit ranger = MakeHero(rangerType, 0, 6, world);
        ranger.Mana = ranger.MaxMana;
        world.Units.Add(ranger);

        Ability charm = ranger.GiveAbility("ANch", 1);
        var charmAbility = charm as RuntimeCharmAbility;
        Verify("魅惑最高单位等级=物编 DataA", charmType.DataAt(1, 'A'), charmAbility?.MaxUnitLevel ?? 0, 0f);

        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = new Vector3(200f, 0f, 0f);
        footman.Target = ranger;
        world.Units.Add(footman);

        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 1, 3, world);
        hero.Position = new Vector3(220f, 0f, 0f);
        world.Units.Add(hero);

        Unit dragon = dragonType != null ? MakeUnit(dragonType, 1) : null;
        if (dragon != null)
        {
            dragon.Position = new Vector3(240f, 0f, 0f);
            world.Units.Add(dragon);
        }

        bool heroBlocked = !charm.CheckTarget(world, hero, hero.Position);
        bool levelBlocked = dragon == null || !charm.CheckTarget(world, dragon, dragon.Position);
        Verify("魅惑不能对英雄使用", 1f, heroBlocked ? 1f : 0f, 0f);
        Verify("魅惑不能对高等级单位使用", 1f, levelBlocked ? 1f : 0f, 0f);

        bool cast = world.TryCast(ranger, charm, footman, footman.Position);
        Verify("魅惑施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("魅惑后单位归施法者", ranger.Player, footman.Player, 0f);
        Verify("魅惑后清空原指令", 0f, footman.Target == null ? 0f : 1f, 0f);
        GD.Print($"[自检] 魅惑 步兵 P1→P{footman.Player}（上限 {charmAbility?.MaxUnitLevel ?? 0} 级，英雄/6 级单位被拒）");
    }

    private static void CheckFingerOfDeath()
    {
        UnitType archimondeType = UnitFactory.Get("Uwar");
        UnitType archerType = UnitFactory.Get("earc");
        AbilityType fingerType = AbilityCatalog.Get("ANfd");
        if (archimondeType == null || archerType == null || fingerType == null) return;

        var world = new World(null, null);
        Unit archimonde = MakeHero(archimondeType, 0, 6, world);
        world.Units.Add(archimonde);

        Unit victim = MakeUnit(archerType, 1);
        victim.Position = new Vector3(300f, 0f, 0f);
        world.Units.Add(victim);

        Ability finger = archimonde.GiveAbility("ANfd", 1);
        float before = victim.Hp;
        bool cast = finger != null && world.TryCast(archimonde, finger, victim, victim.Position);
        float dealt = before - victim.Hp;
        float expected = Mathf.Min(before, fingerType.DataAt(1, 'C'));

        Verify("死亡之指施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("死亡之指按 DataC 扣血", expected, dealt, 0.5f);
        GD.Print($"[自检] 死亡之指 {before:0}→{victim.Hp:0}（DataC={fingerType.DataAt(1, 'C'):0}，Spells/Magic）");
    }

    private static void CheckSoulPreservation()
    {
        UnitType malganisType = UnitFactory.Get("Umal");
        UnitType footmanType = UnitFactory.Get("hfoo");
        AbilityType soulType = AbilityCatalog.Get("ANsl");
        if (malganisType == null || footmanType == null || soulType == null) return;

        var world = new World(null, null);
        Unit malganis = MakeHero(malganisType, 0, 6, world);
        world.Units.Add(malganis);

        Unit victim = MakeUnit(footmanType, 1);
        victim.Position = new Vector3(200f, 0f, 0f);
        world.Units.Add(victim);

        Ability soul = malganis.GiveAbility("ANsl", 1);
        bool cast = soul != null && world.TryCast(malganis, soul, victim, victim.Position);
        Verify("灵魂保存施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("灵魂保存期间无敌", 1f, victim.State.Invulnerable ? 1f : 0f, 0f);
        Verify("灵魂保存期间魔免", 1f, victim.State.MagicImmune ? 1f : 0f, 0f);

        float duration = soulType.DurationAt(1);
        int ticks = Mathf.CeilToInt(duration / World.Tick) + 5;
        for (int index = 0; index < ticks; index++) world.TickAbilities();

        Verify("灵魂保存到期归施法者", malganis.Player, victim.Player, 0f);
        Verify("灵魂保存到期单位死亡", 0f, victim.Alive ? 1f : 0f, 0f);
        Verify("灵魂保存到期撤掉无敌", 0f, victim.State.Invulnerable ? 1f : 0f, 0f);
        GD.Print($"[自检] 灵魂保存 {duration:0}s 后 P1→P{victim.Player}，存活={victim.Alive}");
    }

    private static void CheckDarkConversion()
    {
        UnitType malganisType = UnitFactory.Get("Umal");
        UnitType villagerType = UnitFactory.Get("nvil");
        UnitType footmanType = UnitFactory.Get("hfoo");
        AbilityType convertType = AbilityCatalog.Get("ANdc");
        if (malganisType == null || villagerType == null || footmanType == null || convertType == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);

        Unit malganis = MakeHero(malganisType, 0, 6, world);
        world.Units.Add(malganis);

        Unit villager = MakeUnit(villagerType, 1);
        villager.Position = new Vector3(240f, 0f, 0f);
        world.Units.Add(villager);

        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = new Vector3(260f, 0f, 0f);
        world.Units.Add(footman);

        Ability convert = malganis.GiveAbility("ANdc", 1);
        var convertAbility = convert as DarkConversion;
        Verify("黑暗转换拒绝非 commoner 种族", 0f, convert.CheckTarget(world, footman, footman.Position) ? 1f : 0f, 0f);

        bool cast = world.TryCast(malganis, convert, villager, villager.Position);
        Verify("黑暗转换施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("黑暗转换期间目标沉睡", 1f, villager.State.Stunned ? 1f : 0f, 0f);

        float duration = convertType.DurationAt(1);
        int ticks = Mathf.CeilToInt(duration / World.Tick) + 5;
        for (int index = 0; index < ticks; index++) world.TickAbilities();

        int zombies = 0;
        string resultId = convertAbility?.ResultUnitId ?? "nzom";
        foreach (Unit unit in world.Units)
        {
            if (unit.Alive && unit.Type != null && unit.Type.Id == resultId) zombies++;
        }

        Verify("黑暗转换生成 UnitID 单位", 1f, zombies, 0f);
        Verify("黑暗转换后原单位死亡", 0f, villager.Alive ? 1f : 0f, 0f);
        GD.Print($"[自检] 黑暗转换 {duration:0}s 后 {villager.Type.Id}→{resultId}（新单位 {zombies} 个）");
    }

    private static void CheckVampiricAttack()
    {
        ItemType mask = ItemCatalog.Get("modt");
        UnitType footmanType = UnitFactory.Get("hfoo");
        if (mask == null || footmanType == null) return;

        var world = new World(null, null);
        Unit attacker = MakeUnit(footmanType, 0);
        attacker.Position = new Vector3(-80f, 0f, 0f);
        Unit victim = MakeUnit(footmanType, 1);
        victim.Position = new Vector3(80f, 0f, 0f);
        world.Units.Add(attacker);
        world.Units.Add(victim);

        var item = new Item { Type = mask };
        world.ApplyItemBonus(attacker, item);
        Verify("吸血物品生效（AIva DataA）", 0.5f, attacker.BonusLifestealPct, 0.001f);

        attacker.Hp = 200f;
        float attackerBefore = attacker.Hp;
        float victimBefore = victim.Hp;
        world.DealDamage(world.MakeAttackDamage(attacker, victim, 100f));

        float dealt = victimBefore - victim.Hp;
        float healed = attacker.Hp - attackerBefore;
        Verify("吸血按实际伤害回血", dealt * 0.5f, healed, 0.5f);

        world.RemoveItemBonus(attacker, item);
        Verify("摘掉装备后吸血清零", 0f, attacker.BonusLifestealPct, 0.001f);
        GD.Print($"[自检] {mask.Name} 造成 {dealt:0.0} 伤害回 {healed:0.0} 血（50%）");
    }
}