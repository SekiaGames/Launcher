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
        CheckHealingPotionCodes();
        CheckTargetedItems();
        CheckPermanentItemCooldown();
        CheckItemCharges();
        CheckReincarnationCharm();
        CheckMineItem();
    }

    /// <summary>地精地雷：点落点埋一颗，敌人靠近就引爆（Amnx 结算范围伤害）。</summary>
    private static void CheckMineItem()
    {
        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) =>
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null) return null;
            Unit created = MakeUnit(type, caster.Player);
            created.Position = position;
            foreach (string abilityId in type.Abilities) created.GiveAbility(abilityId, 1);
            return created;
        };

        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);

        bool placed = world.UseItem(hero, GiveItem(hero, "gobm"), null, new Vector3(300f, 0f, 0f));
        Unit mine = world.Units.Find(unit => string.Equals(unit.Type?.Id, "nglm", StringComparison.OrdinalIgnoreCase));
        Verify("地雷可埋", 1f, placed && mine != null ? 1f : 0f, 0f);
        if (mine == null) return;
        Verify("地雷埋下即隐形", 1f, mine.State.Invisible ? 1f : 0f, 0f);

        Unit enemy = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));
        Verify("敌人踩雷前活着", 1f, mine.Alive ? 1f : 0f, 0f);
        for (int index = 0; index < 20; index++) world.TickAbilities();

        Verify("踩雷后地雷炸掉", 0f, mine.Alive ? 1f : 0f, 0f);
        Verify("踩雷的人吃到爆炸伤害", 1f, enemy.Hp < enemy.MaxHp ? 1f : 0f, 0f);
    }

    /// <summary>重生十字章：装备时授重生，死亡后 7s 原地满血复活，十字章一次性吃掉。</summary>
    private static void CheckReincarnationCharm()
    {
        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 3, world);
        world.Units.Add(hero);

        Item ankh = GiveItem(hero, "ankh");
        if (ankh == null) return;
        world.ApplyItemBonus(hero, ankh);
        Verify("十字章装备后授重生", 1f, hero.FindAbility("AIrc") != null ? 1f : 0f, 0f);

        world.KillUnit(hero, null);
        Verify("重生期间算死亡", 0f, hero.Alive ? 1f : 0f, 0f);
        Verify("十字章已消耗", 0f, hero.ItemCount, 0f);

        for (int index = 0; index < 200; index++) world.TickReincarnation();
        Verify("重生到点满血起身", 1f, hero.Alive && hero.Hp >= hero.MaxHp - 0.5f ? 1f : 0f, 0f);
    }

    /// <summary>物编 uses：盗取权杖 2 次、蜘蛛丝饰针 4 次——用完才销毁。</summary>
    private static void CheckItemCharges()
    {
        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        Unit enemy = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));
        enemy.Mana = 500f;

        Item wand = GiveItem(hero, "woms");
        int before = World.ChargesLeft(wand);
        bool first = world.UseItem(hero, wand, enemy, enemy.Position);
        Verify("充能物品首用后仍在", 1f, hero.ItemCount, 0f);
        Verify("充能物品次数递减", before - 1, World.ChargesLeft(wand), 0f);

        wand.CooldownRemaining = 0f;
        bool second = world.UseItem(hero, wand, enemy, enemy.Position);
        Verify("两次都用出去了", 1f, first && second ? 1f : 0f, 0f);
        Verify("次数用完才销毁", 0f, hero.ItemCount, 0f);
    }

    /// <summary>治疗/法力药剂：phea/pman 挂的是 AIh1/AIm1，以前只认 AIhe/AIma 导致喝不动。</summary>
    private static void CheckHealingPotionCodes()
    {
        ItemType potion = ItemCatalog.Get("phea");
        ItemType mana = ItemCatalog.Get("pman");
        if (potion == null || mana == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);

        hero.MaxHp = 2000f;
        hero.Hp = 1000f;
        float before = hero.Hp;
        bool used = world.UseItem(hero, GiveItem(hero, "phea"));
        Verify("治疗药水可用", 1f, used ? 1f : 0f, 0f);
        Verify("治疗药水回 DataA", 250f, hero.Hp - before, 0.5f);

        hero.Mana = 0f;
        bool manaUsed = world.UseItem(hero, GiveItem(hero, "pman"));
        Verify("法力药水可用", 1f, manaUsed ? 1f : 0f, 0f);
        Verify("法力药水回 DataA", 150f, hero.Mana, 0.5f);
    }

    /// <summary>目标型物品：逃脱匕首（点地面）、沉默/蛛网/净化/偷蓝（点单位）、保存（点友军）、恶魔岩石。</summary>
    private static void CheckTargetedItems()
    {
        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
        hero.Position = Vector3.Zero;
        world.Units.Add(hero);

        Verify("逃脱匕首要点落点", (float)ItemTargetMode.Point, (float)ItemUseRules.TargetModeFor(ItemCatalog.Get("desc")), 0f);
        bool blinked = world.UseItem(hero, GiveItem(hero, "desc"), null, new Vector3(400f, 0f, 0f));
        Verify("逃脱匕首可用", 1f, blinked ? 1f : 0f, 0f);
        Verify("逃脱匕首已位移", 400f, hero.Position.X, 4f);

        Unit enemy = ProbeUnit(world, "hfoo", 1, new Vector3(500f, 0f, 0f));
        bool silenced = world.UseItem(hero, GiveItem(hero, "ssil"), null, enemy.Position);
        Verify("沉默权杖可用", 1f, silenced ? 1f : 0f, 0f);
        Verify("沉默权杖按住敌人", 1f, enemy.State.Silenced ? 1f : 0f, 0f);

        world.MoveUnitTo(hero, Vector3.Zero);
        Unit flyer = ProbeUnit(world, "hgyr", 1, new Vector3(-360f, 0f, 0f));
        if (flyer != null)
        {
            bool webbed = world.UseItem(hero, GiveItem(hero, "silk"), flyer, flyer.Position);
            Verify("蜘蛛丝饰针可用", 1f, webbed ? 1f : 0f, 0f);
            Verify("空中单位被拉下地", 1f, flyer.State.Snared ? 1f : 0f, 0f);
        }

        enemy.Mana = 200f;
        bool stolen = world.UseItem(hero, GiveItem(hero, "woms"), enemy, enemy.Position);
        Verify("偷蓝法杖可用", 1f, stolen ? 1f : 0f, 0f);
        Verify("偷蓝法杖只抽 DataA", 150f, enemy.Mana, 0.5f);

        bool purged = world.UseItem(hero, GiveItem(hero, "shtm"), enemy, enemy.Position);
        Verify("萨满图腾可用", 1f, purged ? 1f : 0f, 0f);
        Verify("萨满图腾减速", 1f, enemy.Slowed ? 1f : 0f, 0f);

        bool roared = world.UseItem(hero, GiveItem(hero, "sror"));
        Verify("野兽卷轴可用", 1f, roared ? 1f : 0f, 0f);
        Verify("野兽卷轴加攻击", 0.25f, hero.BonusDamagePct, 0.001f);

        bool inferno = world.UseItem(hero, GiveItem(hero, "infs"), null, new Vector3(300f, 0f, 0f));
        Unit demon = world.Units.Find(unit => string.Equals(unit.Type?.Id, "ninf", StringComparison.OrdinalIgnoreCase));
        Verify("恶魔岩石可用", 1f, inferno ? 1f : 0f, 0f);
        Verify("恶魔岩石召出地狱火", 1f, demon != null ? 1f : 0f, 0f);

        Unit hall = ProbeUnit(world, "htow", 0, new Vector3(6000f, 0f, 4000f));
        Unit ally = ProbeUnit(world, "hfoo", 0, new Vector3(-800f, 0f, 0f));
        bool rescued = world.UseItem(hero, GiveItem(hero, "spre"), ally, ally.Position);
        Verify("保存权杖可用", 1f, rescued ? 1f : 0f, 0f);
        Verify("保存权杖把友军送回基地", 1f, ally.DistanceTo(hall) < 400f ? 1f : 0f, 0f);
    }

    /// <summary>永久可用物品：用一次不消失，只进冷却（沉默权杖 20s / 逃脱匕首 30s）。</summary>
    private static void CheckPermanentItemCooldown()
    {
        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        Unit enemy = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));

        Item staff = GiveItem(hero, "ssil");
        bool first = world.UseItem(hero, staff, null, enemy.Position);
        Verify("沉默权杖首用成功", 1f, first ? 1f : 0f, 0f);
        Verify("永久物品不消耗", 1f, hero.ItemCount, 0f);
        Verify("永久物品进冷却", 20f, staff.CooldownRemaining, 0.5f);

        bool second = world.UseItem(hero, staff, null, enemy.Position);
        Verify("冷却中不能再用", 0f, second ? 1f : 0f, 0f);

        world.TickItemCooldowns();
        Verify("冷却逐 tick 递减", 20f - World.Tick, staff.CooldownRemaining, 0.01f);
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
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
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
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
        world.Units.Add(hero);
        float baseSpeed = World.SpeedOf(hero);

        Item item = GiveItem(hero, "pspd");
        bool used = world.UseItem(hero, item);
        Verify("加速卷轴可用", 1f, used ? 1f : 0f, 0f);
        // 客户端移速上限 400（NativeMovementSpeed.Limit），加速后要按上限截。
        Verify("加速提升=DataA", Mathf.Min(baseSpeed * (1f + speedType.DataAt(1, 'A')), MiscRules.MaxUnitSpeed), World.SpeedOf(hero), 1f);
    }

    private static void CheckDispelWand()
    {
        ItemType wand = ItemCatalog.Get("wneg");
        if (wand == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
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
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
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
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
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
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
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
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
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
        Unit hero = MakeHero(UnitFactory.Get("Hpal"), 0, 1, world);
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