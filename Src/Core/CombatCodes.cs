using System;

namespace OpenWC3;

public static class CombatCodes
{
    public static WeaponSound ParseWeaponSound(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "_" || raw == "-") return WeaponSound.Whoknows;
        return Enum.TryParse(raw.Trim(), true, out WeaponSound parsed)
            ? parsed : WeaponSound.Whoknows;
    }

    public static AttackType ParseAttack(string raw, AttackType fallback = AttackType.Normal)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "normal" => AttackType.Normal,
            "pierce" => AttackType.Pierce,
            "siege" => AttackType.Siege,
            "spells" => AttackType.Spells,
            "chaos" => AttackType.Chaos,
            "magic" => AttackType.Magic,
            "hero" => AttackType.Hero,
            _ => fallback,
        };
    }

    public static DefenseType ParseDefense(string raw, DefenseType fallback = DefenseType.Unarmored)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "small" => DefenseType.Small,
            "medium" => DefenseType.Medium,
            "large" => DefenseType.Large,
            "fort" or "fortified" => DefenseType.Fort,
            "unarmored" or "normal" => DefenseType.Unarmored,
            "hero" => DefenseType.Hero,
            "divine" => DefenseType.Divine,
            "none" => DefenseType.None,
            _ => fallback,
        };
    }

    public static TargetKind ParseTargets(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "_") return TargetKind.None;

        TargetKind kind = TargetKind.None;
        foreach (string part in raw.Split(',', StringSplitOptions.TrimEntries))
        {
            kind |= part.ToLowerInvariant() switch
            {
                "ground" => TargetKind.Ground,
                "air" => TargetKind.Air,
                "structure" => TargetKind.Structure,
                "ward" => TargetKind.Ward,
                "item" => TargetKind.Item,
                "tree" => TargetKind.Tree,
                "wall" => TargetKind.Wall,
                "debris" => TargetKind.Debris,
                _ => TargetKind.None,
            };
        }
        return kind;
    }

    /// <summary>归属关系位：掩码里没写关系就是「全部」（客户端 68c7b0 的默认，友军也会吃溅射）。</summary>
    public static TargetRelation ParseRelations(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "_" || raw == "-") return TargetRelation.All;

        TargetRelation relation = TargetRelation.None;
        foreach (string part in raw.Split(',', StringSplitOptions.TrimEntries))
        {
            relation |= part.ToLowerInvariant() switch
            {
                "self" => TargetRelation.Self,
                "player" => TargetRelation.Player,
                "ally" => TargetRelation.Ally,
                "neutral" => TargetRelation.Neutral,
                "enemy" => TargetRelation.Enemy,
                _ => TargetRelation.None,
            };
        }
        return relation == TargetRelation.None ? TargetRelation.All : relation;
    }

    public static string AttackLabel(AttackType attack) => attack switch
    {
        AttackType.Normal => "普通",
        AttackType.Pierce => "穿刺",
        AttackType.Siege => "攻城",
        AttackType.Spells => "法术",
        AttackType.Chaos => "混乱",
        AttackType.Magic => "魔法",
        AttackType.Hero => "英雄",
        _ => "-",
    };

    public static string DefenseLabel(DefenseType defense) => defense switch
    {
        DefenseType.Small => "轻甲",
        DefenseType.Medium => "中甲",
        DefenseType.Large => "重甲",
        DefenseType.Fort => "加固",
        DefenseType.Unarmored => "无甲",
        DefenseType.Hero => "英雄",
        DefenseType.Divine => "神圣",
        _ => "无",
    };

    public static string Describe(TargetKind kind)
    {
        if (kind == TargetKind.None) return "-";

        Span<char> buffer = stackalloc char[64];
        int length = 0;
        Append(kind, TargetKind.Ground, "地", buffer, ref length);
        Append(kind, TargetKind.Air, "空", buffer, ref length);
        Append(kind, TargetKind.Structure, "建", buffer, ref length);
        Append(kind, TargetKind.Ward, "守", buffer, ref length);
        Append(kind, TargetKind.Item, "物", buffer, ref length);
        Append(kind, TargetKind.Tree, "树", buffer, ref length);
        Append(kind, TargetKind.Wall, "墙", buffer, ref length);
        Append(kind, TargetKind.Debris, "残", buffer, ref length);
        return new string(buffer[..length]);
    }

    private static void Append(TargetKind kind, TargetKind flag, string label, Span<char> buffer, ref int length)
    {
        if ((kind & flag) == 0) return;
        foreach (char c in label)
        {
            if (length < buffer.Length) buffer[length++] = c;
        }
    }
}
