using Godot;
using System;

namespace Launcher;

public static partial class CombatProbe
{
    private static void CheckItemUseAndAuras()
    {
        GD.Print("==== 主动物品与物品光环自检 ====");

        CheckInvulnerabilityPotion();
        CheckSpeedScroll();
        CheckDispelWand();
        CheckHealingScroll();
        CheckHealingWard();
        CheckTownPortal();
        CheckItemAura();
        CheckUltravision();
        CheckResistantSkin();
    }

    private static Item GiveItem(Unit hero, string itemId)
    {
        ItemType type = ItemCatalog.Get(itemId);
        if (type == null) return null;

        var item = new Item { Type = type };
        if (!hero.AddItem(item)) return null;
        return item;
    }

    private static void CheckInvulnerabilityPotion()
    {
        if (ItemCatalog.Get("pnvu") == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        Item item = GiveItem(hero, "pnvu");

        bool used = world.UseItem(hero, item);
        Verify("无敌药水可用", 1f, used ? 1f : 0f, 0f);
        Verify("无敌药水无敌时长=Dur", 15f, hero.State.Remaining(StateMod.Invulnerable), 0.5f);
        Verify("用掉后退格", 0f, hero.ItemCount, 0f);
    }

    private static void CheckSpeedScroll()
    {
        ItemType scroll = ItemCatalog.Get("pspd");
        AbilityType speedType = AbilityCatalog.Get("AIsp");
        if (scroll == null || speedType == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        float baseSpeed = World.SpeedOf(hero);

        Item item = GiveItem(hero, "pspd");
        bool used = world.UseItem(hero, item);
        Verify("加速卷轴可用", 1f, used ? 1f : 0f, 0f);
        Verify("加速提升=DataA", baseSpeed * (1f + speedType.DataAt(1, 'A')), World.SpeedOf(hero), 1f);
    }

    private static void CheckDispelWand()
    {
        ItemType wand = ItemCatalog.Get("wneg");
        if (wand == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        Unit enemy = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));

        world.ApplyBuff(enemy, new StatBuff { DamagePct = 0.5f }, 60f);
        Item item = GiveItem(hero, "wneg");
        bool used = world.UseItem(hero, item);
        Verify("净化棒可用", 1f, used ? 1f : 0f, 0f);
        Verify("净化棒驱散敌方增益", 0f, enemy.BonusDamagePct, 0.001f);
    }

    private static void CheckHealingScroll()
    {
        ItemType scroll = ItemCatalog.Get("shea");
        AbilityType healType = AbilityCatalog.Get("AIha");
        if (scroll == null || healType == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        hero.Hp = hero.MaxHp * 0.4f;
        Unit ally = ProbeUnit(world, "hfoo", 0, new Vector3(200f, 0f, 0f));
        ally.Hp = 100f;

        float before = ally.Hp;
        Item item = GiveItem(hero, "shea");
        world.UseItem(hero, item);
        Verify("群体治疗卷轴=DataA", healType.DataAt(1, 'A'), ally.Hp - before, 0.5f);
    }

    private static void CheckHealingWard()
    {
        ItemType ward = ItemCatalog.Get("whwd");
        if (ward == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitCatalog.Get(id), caster.Player);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);

        Item item = GiveItem(hero, "whwd");
        bool used = world.UseItem(hero, item);

        Unit summoned = world.Units.Find(unit => string.Equals(unit.Type?.Id, "ohwd", StringComparison.OrdinalIgnoreCase));
        Verify("治疗守卫可用", 1f, used ? 1f : 0f, 0f);
        Verify("治疗守卫已召出", 1f, summoned != null ? 1f : 0f, 0f);
    }

    private static void CheckTownPortal()
    {
        ItemType portal = ItemCatalog.Get("stwp");
        if (portal == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        hero.Position = Vector3.Zero;
        world.Units.Add(hero);
        Unit hall = ProbeUnit(world, "htow", 0, new Vector3(6000f, 0f, 4000f));

        Item item = GiveItem(hero, "stwp");
        bool used = world.UseItem(hero, item);
        Verify("回城卷轴可用", 1f, used ? 1f : 0f, 0f);
        Verify("回城后落在基地旁边", 1f, hero.DistanceTo(hall) < 400f ? 1f : 0f, 0f);
    }

    private static void CheckItemAura()
    {
        ItemType helm = ItemCatalog.Get("kpin");
        AbilityType auraType = AbilityCatalog.Get("AIba");
        if (helm == null || auraType == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        Unit ally = ProbeUnit(world, "hfoo", 0, new Vector3(300f, 0f, 0f));

        Item item = GiveItem(hero, "kpin");
        world.ApplyItemBonus(hero, item);
        world.RefreshAuras();
        Verify("物品辉煌光环回蓝=DataA", auraType.DataAt(1, 'A'), ally.AuraManaRegenPerSecond, 0.001f);

        world.RemoveItemBonus(hero, item);
        world.RefreshAuras();
        Verify("卸下后光环消失", 0f, ally.AuraManaRegenPerSecond, 0.001f);
    }

    private static void CheckUltravision()
    {
        var world = new World(null, null);
        Unit hero = MakeHero(UnitCatalog.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);

        Ability night = hero.GiveAbility("Ault", 1);
        Verify("夜视生效", 1f, night != null && night.NightSight ? 1f : 0f, 0f);

        ItemType lens = ItemCatalog.Get("tels");
        if (lens == null) return;
        Item item = GiveItem(hero, "tels");
        if (item == null) return;
        world.ApplyItemBonus(hero, item);
        Ability granted = hero.FindAbility("AIuv");
        Verify("物品夜视装备后生效", 1f, granted != null && granted.NightSight ? 1f : 0f, 0f);

        world.RemoveItemBonus(hero, item);
        Verify("物品夜视卸下后移除", 0f, hero.FindAbility("AIuv") != null ? 1f : 0f, 0f);
    }

    private static void CheckResistantSkin()
    {
        var world = new World(null, null);
        Unit giant = ProbeUnit(world, "emtg", 0, Vector3.Zero, "Arsk");

        world.TickAbilities();
        Verify("抗性皮肤缩放 0.5", 0.5f, giant.State.DebuffScale, 0.001f);

        giant.State.Add(StateMod.Stun, 4f);
        Verify("眩晕 4 秒变 2 秒", 2f, giant.State.Remaining(StateMod.Stun), 0.01f);
    }
}