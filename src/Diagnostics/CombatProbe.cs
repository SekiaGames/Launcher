using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
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
        CheckCorpse();
        CheckItems();
        CheckLoot();
        CheckSkills();
        CheckUndeadSkills();
        CheckRacialSkills();
        CheckMorphSkills();
        CheckAiCombat();
        CheckClientSkills();
        CheckMeleeUltimates();
        CheckBloodMageAndUltimates();
        CheckIllusionSkills();
        CheckTavernSkills();
        CheckFormation();

        GD.Print(_failures == 0 ? "[自检] 状态全部通过" : $"[自检] 状态 {_failures} 项不符");
        return _failures == 0 ? 0 : 1;
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

