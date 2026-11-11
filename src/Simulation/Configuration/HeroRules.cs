using Godot;
using System;

namespace Launcher;

/// <summary>
/// 英雄规则：常量全部取自 Units\MiscData 的 [Misc]（MaxHeroLevel / NeedHeroXP / GrantHeroXP / HeroExpRange）。
/// 升级所需经验按 NeedHeroXP*L + 100*L*(L-1)/2 递推，逐级得到 200/500/900/1400/2000/2700/3500/4400/5400，
/// 与原版 10 级英雄的经验表一致。
/// </summary>
public static class HeroRules
{
    public static readonly int MaxLevel = (int)Value("Misc.MaxHeroLevel", 10f);
    public static readonly int NeedHeroXpBase = (int)Value("Misc.NeedHeroXP", 200f);
    public static readonly float ExpRange = Value("Misc.HeroExpRange", 1200f);
    public static readonly float GrantNormalXp = Value("Misc.GrantNormalXP", 25f);
    public static readonly float GrantHeroXpLow = Value("Misc.GrantHeroXPFormulaC", 100f);

    public static readonly float StrHpBonus = Value("Misc.StrHitPointBonus", 25f);
    public static readonly float IntManaBonus = Value("Misc.IntManaBonus", 15f);
    public static readonly float AgiDefenseBonus = Value("Misc.AgiDefenseBonus", 0.30f);
    public static readonly float AgiDefenseBase = Value("Misc.AgiDefenseBase", -2f);
    public static readonly float AgiAttackSpeedBonus = Value("Misc.AgiAttackSpeedBonus", 0.02f);

    /// <summary>击杀英雄按对方等级给的经验（Misc.GrantHeroXP = 100,120,160,220,300）。</summary>
    private static readonly int[] HeroKillXp = ParseList(ValueText("Misc.GrantHeroXP", "100,120,160,220,300"));

    /// <summary>升到下一级所需经验。</summary>
    public static int XpToNext(int level)
    {
        int clamped = Mathf.Max(1, level);
        return NeedHeroXpBase * clamped + 100 * clamped * (clamped - 1) / 2;
    }

    public static int KillXpFor(Unit victim)
    {
        if (victim == null) return (int)GrantNormalXp;
        if (victim.IsHero)
        {
            int level = Mathf.Clamp(victim.Level, 1, HeroKillXp.Length);
            return HeroKillXp[level - 1];
        }
        return (int)GrantNormalXp;
    }

    /// <summary>英雄属性和等级带来的派生值：生命 = 基础 + 力量*StrHitPointBonus，法力 = 智力*IntManaBonus。</summary>
    public static float MaxHpOf(UnitType type, int strength)
    {
        return type != null ? type.Hp + strength * StrHpBonus : 100f;
    }

    public static float MaxManaOf(int intelligence)
    {
        return intelligence * IntManaBonus;
    }

    /// <summary>敏捷带来的护甲：AgiDefenseBase + 敏捷 × AgiDefenseBonus（圣骑士 13 敏 → 1.9）。</summary>
    public static float AgilityArmor(int agility)
    {
        return AgiDefenseBase + agility * AgiDefenseBonus;
    }

    public static int[] HeroKillTable() => HeroKillXp;

    private static float Value(string key, float fallback) => MiscRules.Value(key, fallback);

    private static string ValueText(string key, string fallback) => MiscRules.Text(key) ?? fallback;

    private static int[] ParseList(string text)
    {
        var list = new System.Collections.Generic.List<int>();
        foreach (string part in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out int value)) list.Add(value);
        }
        if (list.Count == 0) list.AddRange(new[] { 100, 120, 160, 220, 300 });
        return list.ToArray();
    }

    public static void Log()
    {
        GD.Print($"[HERO] 等级上限={MaxLevel} 经验表={string.Join("/", XpToNext(1), XpToNext(2), XpToNext(3), XpToNext(4))}..."
            + $" 击杀英雄经验={string.Join(",", HeroKillXp)} 普通单位={GrantNormalXp:0} 经验范围={ExpRange:0}");
    }
}
