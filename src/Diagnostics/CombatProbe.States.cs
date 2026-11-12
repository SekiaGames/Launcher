using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    private static void CheckDuel()
    {
        UnitType footman = UnitCatalog.Get("hfoo");
        UnitType grunt = UnitCatalog.Get("ogru");
        if (footman == null || grunt == null) return;

        var sim = new World(null, null);
        Unit a = MakeUnit(footman, 0);
        Unit b = MakeUnit(grunt, 12);
        b.Position = new Vector3(100f, 0f, 0f);
        sim.Units.Add(a);
        sim.Units.Add(b);
        a.Target = b;
        b.Target = a;

        int ticks = 0;
        for (; ticks < 6000 && a.Alive && b.Alive; ticks++) sim.Advance();

        GD.Print($"[自检] 步兵 vs 兽族步兵 单挑 {ticks * World.Tick:0.0}s"
            + $" 步兵存活={a.Alive}({a.Hp:0}/{a.MaxHp:0}) 兽兵存活={b.Alive}({b.Hp:0}/{b.MaxHp:0})");
        Verify("单挑能打到一方阵亡", 0f, a.Alive && b.Alive ? 1f : 0f, 0f);
        Verify("步兵 HP 未越界", 1f, a.Hp <= a.MaxHp + 0.01f ? 1f : 0f, 0f);
    }

    private static void CheckImmunity()
    {
        UnitType footman = UnitCatalog.Get("hfoo");
        UnitType grunt = UnitCatalog.Get("ogru");
        if (footman == null || grunt == null)
        {
            GD.Print("[自检] 单位表缺失，跳过免疫检查");
            return;
        }

        var sim = new World(null, null);
        sim.Units.Add(MakeUnit(grunt, 0));
        sim.Units.Add(MakeUnit(footman, 1));
        Unit attacker = sim.Units[0];
        Unit victim = sim.Units[1];

        victim.State.Add(StateMod.Invulnerable, 5f);
        DamageInfo blocked = sim.MakeAttackDamage(attacker, victim, 100f);
        sim.DealDamage(blocked);
        Verify("无敌目标伤害归零", 0f, blocked.Amount, 0.001f);
        Verify("无敌目标不掉血", victim.MaxHp, victim.Hp, 0.001f);
        victim.State.Clear();

        victim.State.Add(StateMod.MagicImmune, 5f);
        DamageInfo magic = World.MakeAbilityDamage(attacker, victim, 100f, AttackType.Spells, DamageType.Magic);
        sim.DealDamage(magic);
        Verify("魔免挡法术伤害", 0f, magic.Amount, 0.001f);

        DamageInfo physical = sim.MakeAttackDamage(attacker, victim, 100f);
        sim.DealDamage(physical);
        Verify("魔免不挡物理伤害", 1f, physical.Amount > 0f ? 1f : 0f, 0f);
        GD.Print($"[自检] 物理 {physical.Raw:0} 经护甲与类别换算后 {physical.Amount:0.00}");
        victim.State.Clear();

        float armor = sim.ArmorOf(victim);
        GD.Print($"[自检] 步兵护甲 {armor:0.00} 折算系数 {War3GameplayConfig.ArmorFactor(armor):0.0000}");
    }

    private static void CheckBounty()
    {
        UnitType creep = UnitCatalog.Get("nban");
        UnitType footman = UnitCatalog.Get("hfoo");
        if (creep == null || footman == null) return;

        var sim = new World(null, null);
        int awards = 0;
        int lastGold = 0;
        sim.BountyAwarded += (player, gold, victim) => { awards++; lastGold = gold; };

        Unit killer = MakeUnit(footman, 0);
        Unit neutral = MakeUnit(creep, 12);
        sim.Units.Add(killer);
        sim.Units.Add(neutral);
        sim.DealDamage(World.MakeAbilityDamage(killer, neutral, 100000f, AttackType.Spells, DamageType.Universal));

        Verify("野怪被击杀给赏金", 1f, awards, 0f);
        int low = creep.BountyPlus + creep.BountyDice;
        int high = creep.BountyPlus + creep.BountyDice * creep.BountySides;
        GD.Print($"[自检] 野怪赏金 {lastGold}（bountyplus={creep.BountyPlus} {creep.BountyDice}d{creep.BountySides}，区间 {low}~{high}）");
        Verify("赏金落在 dice 区间", 1f, lastGold >= low && lastGold <= high ? 1f : 0f, 0f);

        awards = 0;
        Unit enemyKiller = MakeUnit(footman, 0);
        Unit enemyVictim = MakeUnit(footman, 1);
        sim.Units.Add(enemyKiller);
        sim.Units.Add(enemyVictim);
        sim.DealDamage(World.MakeAbilityDamage(enemyKiller, enemyVictim, 100000f, AttackType.Spells, DamageType.Universal));
        Verify("击杀敌方玩家单位不给赏金", 0f, awards, 0f);

        CheckKillExperienceSharing();
    }

    /// <summary>
    /// 击杀经验按收到经验的英雄数均分（客户端 ShareKillExperience = 赏金 / 接收者数，四舍五入），
    /// 召唤物的赏金先乘 SummonedKillFactor 再分。
    /// </summary>
    private static void CheckKillExperienceSharing()
    {
        UnitType heroType = UnitCatalog.Get("Hpal");
        UnitType soldier = UnitCatalog.Get("hfoo");
        UnitType creepType = UnitCatalog.Get("nban");
        if (heroType == null || soldier == null || creepType == null) return;

        var alone = new World(null, null);
        Unit killer = MakeUnit(soldier, 0);
        Unit victim = MakeUnit(creepType, 12);
        Unit soloHero = MakeHero(heroType, 0, 1, alone);
        alone.Units.Add(killer);
        alone.Units.Add(victim);
        alone.Units.Add(soloHero);
        alone.DealDamage(World.MakeAbilityDamage(killer, victim, 100000f, AttackType.Spells, DamageType.Universal));
        int bounty = HeroRules.KillXpFor(victim);
        Verify("单个英雄拿全量经验", bounty, soloHero.Xp, 0f);

        var pair = new World(null, null);
        Unit killer2 = MakeUnit(soldier, 0);
        Unit victim2 = MakeUnit(creepType, 12);
        Unit heroA = MakeHero(heroType, 0, 1, pair);
        Unit heroB = MakeHero(heroType, 0, 1, pair);
        pair.Units.Add(killer2);
        pair.Units.Add(victim2);
        pair.Units.Add(heroA);
        pair.Units.Add(heroB);
        pair.DealDamage(World.MakeAbilityDamage(killer2, victim2, 100000f, AttackType.Spells, DamageType.Universal));
        int shared = Mathf.FloorToInt((float)bounty / 2f + 0.5f);
        Verify("两个英雄各拿一半", shared, heroA.Xp, 0f);
        Verify("两个英雄拿到的一样多", heroA.Xp, heroB.Xp, 0f);

        var summon = new World(null, null);
        Unit killer3 = MakeUnit(soldier, 0);
        Unit summoned = MakeUnit(creepType, 12);
        summoned.SummonRemaining = 30f;
        Unit heroC = MakeHero(heroType, 0, 1, summon);
        summon.Units.Add(killer3);
        summon.Units.Add(summoned);
        summon.Units.Add(heroC);
        summon.DealDamage(World.MakeAbilityDamage(killer3, summoned, 100000f, AttackType.Spells, DamageType.Universal));
        Verify("召唤物经验按 SummonedKillFactor 折算", (int)(bounty * MiscRules.SummonedKillFactor), heroC.Xp, 0f);
    }

    /// <summary>迫击炮 Farea=25 / Harea=150 / Qarea=250：全伤、半伤、四分之一、圈外不受伤。</summary>
    private static void CheckSplash()
    {
        UnitType mortar = UnitCatalog.Get("hmtm");
        UnitType footman = UnitCatalog.Get("hfoo");
        if (mortar == null || footman == null) return;

        var world = new World(null, null);
        Unit attacker = MakeUnit(mortar, 0);
        Unit center = MakeUnit(footman, 1);
        Unit near = MakeUnit(footman, 1);
        Unit mid = MakeUnit(footman, 1);
        Unit far = MakeUnit(footman, 1);
        near.Position = new Vector3(100f, 0f, 0f);
        mid.Position = new Vector3(200f, 0f, 0f);
        far.Position = new Vector3(400f, 0f, 0f);

        world.Units.Add(attacker);
        world.Units.Add(center);
        world.Units.Add(near);
        world.Units.Add(mid);
        world.Units.Add(far);

        DamageInfo primary = world.MakeAttackDamage(attacker, center, 100f);
        world.DealDamage(primary);
        world.ApplySplash(primary);

        float centerLoss = center.MaxHp - center.Hp;
        float nearLoss = near.MaxHp - near.Hp;
        float midLoss = mid.MaxHp - mid.Hp;
        float farLoss = far.MaxHp - far.Hp;

        GD.Print($"[自检] 溅射 主目标{centerLoss:0.0} 100距离{nearLoss:0.0} 200距离{midLoss:0.0} 400距离{farLoss:0.0}"
            + $"（Farea={mortar.SplashFull:0} Harea={mortar.SplashHalf:0} Qarea={mortar.SplashQuarter:0}）");
        Verify("溅射中圈系数按 Hfact", mortar.Weapon.SplashHalfRatio, centerLoss <= 0f ? 0f : nearLoss / centerLoss, 0.02f);
        Verify("溅射外圈系数按 Qfact", mortar.Weapon.SplashQuarterRatio, centerLoss <= 0f ? 0f : midLoss / centerLoss, 0.02f);
        Verify("超出 Qarea 不受伤", 0f, farLoss, 0.01f);

        Unit ally = MakeUnit(footman, 0);
        ally.Position = new Vector3(100f, 0f, 0f);
        world.Units.Add(ally);
        UnitType gyro = UnitCatalog.Get("hgyr");
        Unit flyer = gyro != null ? MakeUnit(gyro, 1) : null;
        if (flyer != null)
        {
            flyer.Position = new Vector3(100f, 0f, 0f);
            world.Units.Add(flyer);
        }

        float allyBefore = ally.Hp;
        float flyerBefore = flyer?.Hp ?? 0f;
        world.ApplySplash(world.MakeAttackDamage(attacker, center, 100f));
        Verify("溅射掩码没写关系 → 误伤友军", 1f, ally.Hp < allyBefore ? 1f : 0f, 0f);
        if (flyer != null)
        {
            Verify("溅射掩码不含空中 → 飞机不吃", 0f, flyer.Hp < flyerBefore ? 1f : 0f, 0f);
        }

        UnitType marked = UnitCatalog.Get("hwat");
        if (marked?.Weapon == null) return;
        var markedWorld = new World(null, null);
        Unit caster = MakeUnit(marked, 0);
        Unit target = MakeUnit(footman, 1);
        Unit friendly = MakeUnit(footman, 0);
        friendly.Position = new Vector3(60f, 0f, 0f);
        markedWorld.Units.Add(caster);
        markedWorld.Units.Add(target);
        markedWorld.Units.Add(friendly);
        float friendlyBefore = friendly.Hp;
        markedWorld.ApplySplash(markedWorld.MakeAttackDamage(caster, target, 100f));
        Verify("显式 enemy 掩码不误伤友军", 0f, friendly.Hp < friendlyBefore ? 1f : 0f, 0f);
    }

    private static void CheckRegeneration()
    {
        UnitType footman = UnitCatalog.Get("hfoo");
        UnitType blacksmith = UnitCatalog.Get("hbla");
        if (footman == null || blacksmith == null) return;

        var world = new World(null, null);
        Unit unit = MakeUnit(footman, 0);
        unit.Hp = 100f;
        Unit noRegen = MakeUnit(blacksmith, 0);
        noRegen.Hp = 100f;
        noRegen.IsBuilding = true;
        world.Units.Add(unit);
        world.Units.Add(noRegen);

        int ticks = 400;
        for (int tick = 0; tick < ticks; tick++) world.Advance();

        float seconds = ticks * World.Tick;
        float healed = unit.Hp - 100f;
        Verify("步兵按 regenHP 回血", footman.RegenHp * seconds, healed, 0.5f);
        Verify("regenType=none 不回血", 0f, noRegen.Hp - 100f, 0.01f);
        GD.Print($"[自检] 恢复 步兵 regenHP={footman.RegenHp:0.00}/s，{seconds:0}s 回 {healed:0.00}"
            + $"；铁匠铺 regenType={blacksmith.RegenType} 不回血");
    }

    private static void CheckRally()
    {
        UnitType hall = UnitCatalog.Get("htow");
        UnitType footman = UnitCatalog.Get("hfoo");
        UnitType peasant = UnitCatalog.Get("hpea");
        UnitType mine = UnitCatalog.Get("ngol");
        if (hall == null || footman == null || peasant == null || mine == null) return;

        var world = new World(null, null);
        Unit building = MakeUnit(hall, 0);
        building.IsBuilding = true;
        world.Units.Add(building);

        world.SetRally(building, new Vector3(500f, 0f, 500f));
        Unit fresh = MakeUnit(footman, 0);
        world.Units.Add(fresh);
        world.SendToRally(fresh, building);
        Verify("集结点：新单位走向地面点", 1f, fresh.MoveTarget.HasValue ? 1f : 0f, 0f);

        Unit goldMine = MakeUnit(mine, 12);
        goldMine.Gold = 500;
        world.Units.Add(goldMine);
        world.SetRally(building, goldMine);
        Unit worker = MakeUnit(peasant, 0);
        world.Units.Add(worker);
        world.SendToRally(worker, building);
        Verify("集结点：农民转去采金", 1f, ReferenceEquals(worker.GoldMine, goldMine) ? 1f : 0f, 0f);

        Unit soldier = MakeUnit(footman, 0);
        world.Units.Add(soldier);
        world.SendToRally(soldier, building);
        Verify("集结点：非工人不会被派去采金", 1f, soldier.GoldMine == null ? 1f : 0f, 0f);
    }

    /// <summary>双武器：英雄第一套打不到空中、第二套能；迫击炮砸建筑走第二套。</summary>
    private static void CheckSecondWeapon()
    {
        UnitType hero = UnitCatalog.Get("Hpal");
        UnitType gyro = UnitCatalog.Get("hgyr");
        UnitType mortar = UnitCatalog.Get("hmtm");
        UnitType blacksmith = UnitCatalog.Get("hbla");
        if (hero?.Weapon2 == null || gyro == null || mortar?.Weapon2 == null || blacksmith == null) return;

        Verify("英雄第一套打不到空中", 0f, hero.Weapon.CanTarget(gyro.Kind) ? 1f : 0f, 0f);
        Verify("英雄第二套打得到空中", 1f, hero.Weapon2.CanTarget(gyro.Kind) ? 1f : 0f, 0f);
        Verify("对空自动选第二套", 1f, ReferenceEquals(hero.WeaponFor(gyro.Kind), hero.Weapon2) ? 1f : 0f, 0f);
        Verify("第二套射程 500", 500f, hero.Weapon2.Range, 0.01f);

        Verify("迫击炮第一套不含建筑", 0f, mortar.Weapon.CanTarget(blacksmith.Kind) ? 1f : 0f, 0f);
        Verify("迫击炮砸建筑选第二套", 1f, ReferenceEquals(mortar.WeaponFor(blacksmith.Kind), mortar.Weapon2) ? 1f : 0f, 0f);
        Verify("迫击炮第二套射程 1150", 1150f, mortar.Weapon2.Range, 0.01f);
        GD.Print($"[自检] 双武器 圣骑士 主{hero.Weapon.Range:0}/副{hero.Weapon2.Range:0}"
            + $" 迫击炮 主{mortar.Weapon.Range:0}/副{mortar.Weapon2.Range:0}");
    }

    /// <summary>尸体按 deathType：3/2 会衰减，建筑不留尸。</summary>
    private static void CheckCorpse()
    {
        UnitType footman = UnitCatalog.Get("hfoo");
        UnitType hall = UnitCatalog.Get("htow");
        if (footman == null || hall == null) return;

        var world = new World(null, null);
        Unit soldier = MakeUnit(footman, 0);
        Unit building = MakeUnit(hall, 0);
        building.IsBuilding = true;
        world.Units.Add(soldier);
        world.Units.Add(building);

        soldier.EnterDeath();
        building.EnterDeath();
        Verify("单位阵亡留尸", 1f, soldier.IsCorpse ? 1f : 0f, 0f);
        Verify("建筑阵亡不留尸", 0f, building.IsCorpse ? 1f : 0f, 0f);
        Verify("会衰减的尸体有倒计时", 1f,
            soldier.CorpseRemaining > 0f && soldier.CorpseRemaining <= Unit.CorpseSeconds ? 1f : 0f, 0f);
        GD.Print($"[自检] 尸体 步兵 deathType={footman.DeathType} 衰减={footman.CorpseDecays}"
            + $" 倒计时={soldier.CorpseRemaining:0}s；建筑 deathType={hall.DeathType} 不留尸");
    }

    /// <summary>物品：英雄走近自动拾取、非英雄不捡、地上清单同步。</summary>
    private static void CheckItems()
    {
        ItemType crown = ItemCatalog.Get("ckng");
        UnitType paladinType = UnitCatalog.Get("Hpal");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        if (crown == null || paladinType == null || footmanType == null) return;

        var world = new World(null, null);
        Unit paladin = MakeUnit(paladinType, 0);
        paladin.IsHero = true;
        world.Units.Add(paladin);

        var first = new Item { Type = crown, Position = new Vector3(50f, 0f, 0f) };
        world.DropItem(first, first.Position);
        world.TickItemPickup();

        Verify("英雄走近自动拾取", 1f, paladin.ItemCount == 1 ? 1f : 0f, 0f);
        Verify("物品离地", 0f, first.OnGround ? 1f : 0f, 0f);
        Verify("地上清单清空", 0f, world.GroundItems.Count, 0f);

        paladin.Position = new Vector3(10000f, 0f, 0f);
        Unit soldier = MakeUnit(footmanType, 0);
        world.Units.Add(soldier);
        var second = new Item { Type = crown, Position = new Vector3(30f, 0f, 0f) };
        world.DropItem(second, second.Position);
        world.TickItemPickup();

        Verify("非英雄不会拾取", 0f, soldier.ItemCount, 0f);
        Verify("物品仍留在地上", 1f, second.OnGround ? 1f : 0f, 0f);
        GD.Print($"[自检] 物品 {crown.Name} Lv{crown.Level} 可掉落={crown.Droppable}，"
            + $"英雄栏位 {paladin.ItemCount}/{Unit.InventorySize}");
    }

    /// <summary>野怪死亡按掉落集掉物品，英雄随后能捡走。</summary>
    private static void CheckLoot()
    {
        UnitType creep = UnitCatalog.Get("nftr");
        UnitType footman = UnitCatalog.Get("hfoo");
        UnitType hero = UnitCatalog.Get("Hpal");
        if (creep == null || footman == null || hero == null) return;

        var world = new World(null, null);
        Unit monster = MakeUnit(creep, 12);
        Unit killer = MakeUnit(footman, 0);
        world.Units.Add(monster);
        world.Units.Add(killer);

        var set = new UnitItemDropSet();
        set.Items.Add(new UnitItemDrop("phea", 100));
        monster.DropSets.Add(set);

        int spawned = 0;
        world.ItemSpawned += item => spawned++;
        world.DealDamage(World.MakeAbilityDamage(killer, monster, 100000f, AttackType.Spells, DamageType.Universal));

        Verify("野怪死亡触发掉落", 1f, spawned, 0f);
        Verify("掉落进入地面清单", 1f, world.GroundItems.Count, 0f);

        Unit paladin = MakeUnit(hero, 0);
        paladin.IsHero = true;
        if (world.GroundItems.Count > 0) paladin.Position = world.GroundItems[0].Position;
        world.Units.Add(paladin);
        world.TickItemPickup();
        Verify("英雄捡起掉落的物品", 1f, paladin.ItemCount, 0f);

        GD.Print($"[自检] 掉落 {creep.Name} → {(spawned > 0 ? "有" : "无")}，"
            + $"拾取后坐标地面清单 {world.GroundItems.Count}");
    }
}
