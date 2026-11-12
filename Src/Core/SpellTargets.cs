using System;

namespace OpenWC3;

/// <summary>
/// 技能的目标位。对标客户端 CTargetType：AbilityData.targsN 用的是这套位，
/// 既含单位类别（地面/空中/建筑）也含关系（敌/友/自己/亡灵）。
/// 武器掩码用 <see cref="TargetKind"/>，技能掩码用这个，两者语义不同不能混用。
/// </summary>
[Flags]
public enum SpellTargets
{
    None = 0,

    // —— 单位类别 ——
    Ground = 1 << 0,
    Air = 1 << 1,
    Structure = 1 << 2,
    Debris = 1 << 3,
    Item = 1 << 4,
    Ward = 1 << 5,
    Tree = 1 << 6,
    Wall = 1 << 7,

    // —— 关系 ——
    Enemy = 1 << 8,
    Neutral = 1 << 9,
    Friend = 1 << 10,
    Self = 1 << 11,
    NotSelf = 1 << 12,
    Player = 1 << 13,
    Ally = 1 << 14,

    // —— 状态 ——
    Dead = 1 << 15,
    Organic = 1 << 16,
    Vulnerable = 1 << 17,
    Invulnerable = 1 << 18,
    NonAncient = 1 << 19,
    NonHero = 1 << 20,
    Hero = 1 << 21,
    NonMagicImmune = 1 << 22,
    MagicImmune = 1 << 23,
    Mine = 1 << 24,
    NonStructure = 1 << 25,
}

public static class SpellTargetCode
{
    private static readonly (string Token, SpellTargets Flag)[] Table =
    {
        ("ground", SpellTargets.Ground),
        ("air", SpellTargets.Air),
        ("structure", SpellTargets.Structure),
        ("debris", SpellTargets.Debris),
        ("item", SpellTargets.Item),
        ("ward", SpellTargets.Ward),
        ("tree", SpellTargets.Tree),
        ("wall", SpellTargets.Wall),
        ("enemy", SpellTargets.Enemy),
        ("neutral", SpellTargets.Neutral),
        ("friend", SpellTargets.Friend),
        ("ally", SpellTargets.Ally),
        ("self", SpellTargets.Self),
        ("notself", SpellTargets.NotSelf),
        ("player", SpellTargets.Player),
        ("dead", SpellTargets.Dead),
        ("organic", SpellTargets.Organic),
        ("vuln", SpellTargets.Vulnerable),
        ("invu", SpellTargets.Invulnerable),
        ("nonancient", SpellTargets.NonAncient),
        ("nonhero", SpellTargets.NonHero),
        ("hero", SpellTargets.Hero),
        ("nonmagicimmune", SpellTargets.NonMagicImmune),
        ("nonstructure", SpellTargets.NonStructure),
        ("mine", SpellTargets.Mine),
    };

    public static SpellTargets Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "_") return SpellTargets.None;

        SpellTargets flags = SpellTargets.None;
        foreach (string part in raw.Split(',', StringSplitOptions.TrimEntries))
        {
            string token = part.ToLowerInvariant();
            if (token.Length == 0) continue;
            foreach ((string key, SpellTargets flag) in Table)
            {
                if (token == key)
                {
                    flags |= flag;
                    break;
                }
            }
        }
        return flags;
    }

    /// <summary>把技能掩码里的单位类别翻成武器掩码，用于复用 CanStrike 式的类别判定。</summary>
    public static TargetKind ToTargetKind(SpellTargets targets)
    {
        TargetKind kind = TargetKind.None;
        if ((targets & SpellTargets.Ground) != 0) kind |= TargetKind.Ground;
        if ((targets & SpellTargets.Air) != 0) kind |= TargetKind.Air;
        if ((targets & SpellTargets.Structure) != 0) kind |= TargetKind.Structure;
        if ((targets & SpellTargets.Ward) != 0) kind |= TargetKind.Ward;
        if ((targets & SpellTargets.Item) != 0) kind |= TargetKind.Item;
        if ((targets & SpellTargets.Tree) != 0) kind |= TargetKind.Tree;
        if ((targets & SpellTargets.Wall) != 0) kind |= TargetKind.Wall;
        if ((targets & SpellTargets.Debris) != 0) kind |= TargetKind.Debris;
        return kind;
    }
}
