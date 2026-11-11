using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 装备属性加成。对标客户端 Src/Items/ItemBonus.cs：
/// 物品本身不含数值，效果写在 ItemData.abilList 指向的能力里，
/// 这里按能力的基类码把它们翻译成一批可累加的字段。
///
/// 码的含义由实际物品反查确认（Extract 的 ItemData + AbilityData 交叉验证）：
/// <list type="bullet">
/// <item>AIab —— 英雄三围（DataA=力量 DataB=敏捷 DataC=智力）：ckng 5/5/5、rst1 0/0/3</item>
/// <item>AIat —— 攻击力（DataA）：ratc +12、stwa +5</item>
/// <item>AIde —— 护甲（DataA）：rde1 +2</item>
/// <item>AIas —— 攻击速度（DataA，比例）：gcel 0.15</item>
/// <item>AIms —— 移动速度（DataA，绝对值）：ms 类鞋子</item>
/// </list>
/// </summary>
public readonly struct ItemBonus
{
    public readonly int Strength;
    public readonly int Agility;
    public readonly int Intelligence;
    public readonly float Damage;
    public readonly float Armor;
    public readonly float AttackSpeedPct;
    public readonly float MoveSpeed;

    public ItemBonus(int strength, int agility, int intelligence,
        float damage, float armor, float attackSpeedPct, float moveSpeed)
    {
        Strength = strength;
        Agility = agility;
        Intelligence = intelligence;
        Damage = damage;
        Armor = armor;
        AttackSpeedPct = attackSpeedPct;
        MoveSpeed = moveSpeed;
    }

    public static readonly ItemBonus Zero = new(0, 0, 0, 0f, 0f, 0f, 0f);

    public static ItemBonus operator +(ItemBonus a, ItemBonus b)
    {
        return new ItemBonus(
            a.Strength + b.Strength,
            a.Agility + b.Agility,
            a.Intelligence + b.Intelligence,
            a.Damage + b.Damage,
            a.Armor + b.Armor,
            a.AttackSpeedPct + b.AttackSpeedPct,
            a.MoveSpeed + b.MoveSpeed);
    }

    public bool IsZero => Strength == 0 && Agility == 0 && Intelligence == 0
        && Damage == 0f && Armor == 0f && AttackSpeedPct == 0f && MoveSpeed == 0f;

    /// <summary>把一件物品的 abilList 逐条翻译成加成。</summary>
    public static ItemBonus ForItem(ItemType item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.AbilList)) return Zero;

        ItemBonus total = Zero;
        foreach (string id in item.AbilList.Split(',', ';', StringSplitOptions.TrimEntries))
        {
            if (id.Length < 4) continue;
            AbilityType ability = AbilityCatalog.Get(id[..4]);
            if (ability == null) continue;
            total += ForAbility(ability);
        }
        return total;
    }

    public static ItemBonus ForAbility(AbilityType ability)
    {
        if (ability == null) return Zero;

        switch (ability.Code?.ToUpperInvariant())
        {
            case "AIAB":
                return new ItemBonus(
                    (int)MathF.Round(ability.DataAt(1, 'A')),
                    (int)MathF.Round(ability.DataAt(1, 'B')),
                    (int)MathF.Round(ability.DataAt(1, 'C')),
                    0f, 0f, 0f, 0f);
            case "AIAT":
                return new ItemBonus(0, 0, 0, ability.DataAt(1, 'A'), 0f, 0f, 0f);
            case "AIDE":
                return new ItemBonus(0, 0, 0, 0f, ability.DataAt(1, 'A'), 0f, 0f);
            case "AIAS":
                return new ItemBonus(0, 0, 0, 0f, 0f, ability.DataAt(1, 'A'), 0f);
            case "AIMS":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, ability.DataAt(1, 'A'));
            default:
                return Zero;
        }
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Strength != 0) parts.Add($"力量+{Strength}");
        if (Agility != 0) parts.Add($"敏捷+{Agility}");
        if (Intelligence != 0) parts.Add($"智力+{Intelligence}");
        if (Damage != 0f) parts.Add($"攻击+{Damage:0.##}");
        if (Armor != 0f) parts.Add($"护甲+{Armor:0.##}");
        if (AttackSpeedPct != 0f) parts.Add($"攻速+{AttackSpeedPct * 100f:0.#}%");
        if (MoveSpeed != 0f) parts.Add($"移速+{MoveSpeed:0}");
        return parts.Count == 0 ? "-" : string.Join(" ", parts);
    }
}
