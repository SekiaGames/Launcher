using Godot;
using System;

namespace Launcher;

public static class CombatProbe
{
    private static int _failures;

    public static int Run()
    {
        _failures = 0;
        GD.Print("==== 战斗结算管线自检 ====");

        War3GameplayConfig.LogMatrix();

        CheckRatio("穿刺 vs 加固", AttackType.Pierce, DefenseType.Fort, 0.35f);
        CheckRatio("攻城 vs 加固", AttackType.Siege, DefenseType.Fort, 1.50f);
        CheckRatio("普通 vs 加固", AttackType.Normal, DefenseType.Fort, 0.50f);
        CheckRatio("魔法 vs 加固", AttackType.Magic, DefenseType.Fort, 0.35f);
        CheckRatio("混乱 vs 神圣", AttackType.Chaos, DefenseType.Divine, 1.00f);
        CheckRatio("所有普通 vs 神圣", AttackType.Hero, DefenseType.Divine, 0.05f);

        CheckArmor("0 护甲", 0f, 1f);
        CheckArmor("5 护甲", 5f, 1f / 1.3f);
        CheckArmor("10 护甲", 10f, 1f / 1.6f);
        CheckArmor("-5 护甲", -5f, 2f - Mathf.Pow(0.94f, 5f));

        CheckFloor("保底抬到 min(raw,1)", 14f, AttackType.Normal, DefenseType.Divine, 20f, 1f);
        CheckFloor("原始伤害不足 1 时按原始值保底", 0.4f, AttackType.Normal, DefenseType.Divine, 20f, 0.4f);

        GD.Print("==== 单位战斗档案 ====");
        foreach (string id in new[] { "hfoo", "hrif", "earc", "hmtm", "ogru", "uaco", "nftr", "hgyr", "hbla", "Hpal", "Hamg" })
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null)
            {
                GD.Print($"[档] {id,-5} 表里没有");
                continue;
            }

            GD.Print($"[档] {id,-5} {type.Name,-8} 攻击{type.AttackType,-6} 护甲{type.DefenseType,-8}({type.Armor:0.0})"
                + $" 伤害{type.MinDamage:0}-{type.MaxDamage:0} 间隔{type.AttackCooldown:0.00}s"
                + $" 碰撞{type.CollisionRadius:0} 自身{CombatCodes.Describe(type.Kind)} 可打{CombatCodes.Describe(type.AllTargets)}"
                + (type.Flying ? " 飞行" : ""));
        }

        GD.Print("==== 单次命中拆解（原始 100） ====");
        foreach ((string attacker, string victim) in new[]
        {
            ("hfoo", "hfoo"), ("hrif", "hfoo"), ("hmtm", "hbla"),
            ("earc", "hrif"), ("Hpal", "hfoo"), ("ogru", "earc"),
        })
        {
            ReportHit(attacker, victim, 100f);
        }

        GD.Print("==== 目标掩码 ====");
        ReportMask("hfoo", "hgyr");
        ReportMask("earc", "hgyr");
        ReportMask("hmtm", "hbla");

        GD.Print(_failures == 0 ? "[自检] 全部通过" : $"[自检] {_failures} 项不符");
        return _failures == 0 ? 0 : 1;
    }

    public static int RunStates()
    {
        _failures = 0;
        GD.Print("==== 状态修饰自检 ====");

        var states = new State();
        Verify("初始可动可打", 1f, states.CanMove && states.CanAttack ? 1f : 0f, 0f);

        states.Add(StateMod.Stun, 0.5f);
        Verify("眩晕后不可移动", 0f, states.CanMove ? 1f : 0f, 0f);
        Verify("眩晕后不可攻击", 0f, states.CanAttack ? 1f : 0f, 0f);
        Verify("眩晕后不可施法", 0f, states.CanCast ? 1f : 0f, 0f);

        states.Add(StateMod.Stun, 0.5f);
        Verify("两次眩晕计数叠加", 2f, states.Count(StateMod.Stun), 0f);
        states.Remove(StateMod.Stun);
        Verify("去掉一层仍在眩晕", 1f, states.Has(StateMod.Stun) ? 1f : 0f, 0f);

        for (int tick = 0; tick < 11; tick++) states.Tick(0.05f);
        Verify("0.55s 后眩晕自动到期", 0f, states.Has(StateMod.Stun) ? 1f : 0f, 0f);
        Verify("到期后可移动", 1f, states.CanMove ? 1f : 0f, 0f);

        states.Add(StateMod.Snare, 2f);
        Verify("定身禁移动", 0f, states.CanMove ? 1f : 0f, 0f);
        Verify("定身仍可攻击", 1f, states.CanAttack ? 1f : 0f, 0f);
        states.Clear();

        states.Add(StateMod.Disarm, 3f);
        Verify("缴械禁普攻", 0f, states.CanAttack ? 1f : 0f, 0f);
        states.Clear();

        states.Add(StateMod.Ethereal, 3f);
        Verify("虚无禁普攻", 0f, states.CanAttack ? 1f : 0f, 0f);
        Verify("虚无法术加成 1.66", 1.66f, War3GameplayConfig.EtherealBonus(AttackType.Spells), 0.001f);
        Verify("虚无普攻无加成", 1f, War3GameplayConfig.EtherealBonus(AttackType.Normal), 0.001f);

        CheckImmunity();
        CheckDuel();
        CheckBounty();
        CheckSplash();
        CheckRegeneration();
        CheckRally();
        CheckSecondWeapon();

        GD.Print(_failures == 0 ? "[自检] 状态全部通过" : $"[自检] 状态 {_failures} 项不符");
        return _failures == 0 ? 0 : 1;
    }

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

    private static Unit MakeUnit(UnitType type, int player)
    {
        return new Unit
        {
            Type = type,
            Player = player,
            Position = Vector3.Zero,
            Hp = type.Hp,
            MaxHp = type.Hp,
            IsBuilding = type.IsBuilding,
        };
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
        Verify("溅射半伤档", 0.5f, centerLoss <= 0f ? 0f : nearLoss / centerLoss, 0.02f);
        Verify("溅射四分之一档", 0.25f, centerLoss <= 0f ? 0f : midLoss / centerLoss, 0.02f);
        Verify("超出 Qarea 不受伤", 0f, farLoss, 0.01f);
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

    private static void ReportHit(string attackerId, string victimId, float raw)
    {
        UnitType attacker = UnitCatalog.Get(attackerId);
        UnitType victim = UnitCatalog.Get(victimId);
        if (attacker == null || victim == null) return;

        float armor = victim.Armor;
        GD.Print($"[命中] {attackerId} → {victimId}  {War3GameplayConfig.Describe(raw, attacker.AttackType, victim.DefenseType, DamageType.Normal, armor, false, false)}");
    }

    private static void ReportMask(string attackerId, string victimId)
    {
        UnitType attacker = UnitCatalog.Get(attackerId);
        UnitType victim = UnitCatalog.Get(victimId);
        if (attacker == null || victim == null) return;

        bool can = attacker.CanTarget(victim.Kind);
        string second = attacker.Weapon2 != null ? " +第二武器" : "";
        GD.Print($"[掩码] {attackerId}(可打 {CombatCodes.Describe(attacker.AllTargets)}{second}) → {victimId}(属于 {CombatCodes.Describe(victim.Kind)}) = {(can ? "能打" : "打不到")}");
    }

    private static void CheckRatio(string label, AttackType attack, DefenseType defense, float expected)
    {
        float actual = War3GameplayConfig.ClassRatio(attack, defense);
        Verify(label, expected, actual, 0.001f);
    }

    private static void CheckArmor(string label, float armor, float expected)
    {
        Verify(label, expected, War3GameplayConfig.ArmorFactor(armor), 0.0001f);
    }

    private static void CheckFloor(string label, float raw, AttackType attack, DefenseType defense, float armor, float expected)
    {
        float actual = War3GameplayConfig.Resolve(raw, attack, defense, DamageType.Normal, armor, false, false);
        Verify(label, expected, actual, 0.001f);
    }

    private static void Verify(string label, float expected, float actual, float tolerance)
    {
        bool ok = Mathf.Abs(expected - actual) <= tolerance;
        if (!ok) _failures++;
        GD.Print($"[校验] {(ok ? "OK  " : "FAIL")} {label} 期望 {expected:0.0000} 实际 {actual:0.0000}");
    }
}
