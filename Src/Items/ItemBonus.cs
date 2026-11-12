using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 装备属性加成。物品本身不含数值，效果写在 ItemData.abilList 指向的能力里，
/// 这里按能力的基类码把它们翻译成一批可累加的字段。
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
    public readonly float LifestealPct;
    public readonly float ManaRegen;
    public readonly float EvasionPct;
    public readonly float Sight;
    public readonly float OrbDamage;
    public readonly float OrbSecondary;
    public readonly OrbEffect OrbEffect;
    public readonly float OrbSeconds;
    public readonly float MaxHp;
    public readonly float MaxMana;
    public readonly float MagicReductionPct;

    public ItemBonus(int strength, int agility, int intelligence,
        float damage, float armor, float attackSpeedPct, float moveSpeed, float lifestealPct,
        float manaRegen = 0f, float evasionPct = 0f, float sight = 0f,
        float orbDamage = 0f, float orbSecondary = 0f, OrbEffect orbEffect = OrbEffect.None, float orbSeconds = 0f,
        float maxHp = 0f, float maxMana = 0f, float magicReductionPct = 0f)
    {
        Strength = strength;
        Agility = agility;
        Intelligence = intelligence;
        Damage = damage;
        Armor = armor;
        AttackSpeedPct = attackSpeedPct;
        MoveSpeed = moveSpeed;
        LifestealPct = lifestealPct;
        ManaRegen = manaRegen;
        EvasionPct = evasionPct;
        Sight = sight;
        OrbDamage = orbDamage;
        OrbSecondary = orbSecondary;
        OrbEffect = orbEffect;
        OrbSeconds = orbSeconds;
        MaxHp = maxHp;
        MaxMana = maxMana;
        MagicReductionPct = magicReductionPct;
    }

    public static readonly ItemBonus Zero = new(0, 0, 0, 0f, 0f, 0f, 0f, 0f);

    public static ItemBonus operator +(ItemBonus a, ItemBonus b)
    {
        return new ItemBonus(
            a.Strength + b.Strength,
            a.Agility + b.Agility,
            a.Intelligence + b.Intelligence,
            a.Damage + b.Damage,
            a.Armor + b.Armor,
            a.AttackSpeedPct + b.AttackSpeedPct,
            a.MoveSpeed + b.MoveSpeed,
            a.LifestealPct + b.LifestealPct,
            a.ManaRegen + b.ManaRegen,
            a.EvasionPct + b.EvasionPct,
            a.Sight + b.Sight,
            a.OrbDamage + b.OrbDamage,
            Math.Max(a.OrbSecondary, b.OrbSecondary),
            a.OrbEffect != OrbEffect.None ? a.OrbEffect : b.OrbEffect,
            Math.Max(a.OrbSeconds, b.OrbSeconds),
            a.MaxHp + b.MaxHp,
            a.MaxMana + b.MaxMana,
            a.MagicReductionPct + b.MagicReductionPct);
    }

    public bool IsZero => Strength == 0 && Agility == 0 && Intelligence == 0
        && Damage == 0f && Armor == 0f && AttackSpeedPct == 0f && MoveSpeed == 0f && LifestealPct == 0f
        && ManaRegen == 0f && EvasionPct == 0f && Sight == 0f
        && OrbDamage == 0f && OrbSecondary == 0f && OrbEffect == OrbEffect.None && OrbSeconds == 0f
        && MaxHp == 0f && MaxMana == 0f && MagicReductionPct == 0f;

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

        float dataA = ability.DataAt(1, 'A');
        float dataB = ability.DataAt(1, 'B');
        float dataC = ability.DataAt(1, 'C');
        float duration = ability.DurationAt(1) > 0f ? ability.DurationAt(1) : 0f;

        switch ((ability.Id ?? "").ToUpperInvariant())
        {
            case "AILL":
            case "AILX":
                return Orb(OrbEffect.Lightning, dataA, dataB, 5f);
            case "AISB":
                return Orb(OrbEffect.Spells, dataA, dataB, 5f);
            case "AISZ":
            case "AIPB":
                return Orb(OrbEffect.Venom, dataA, 0f, duration > 0f ? duration : 5f);
            case "APO2":
                return Orb(OrbEffect.Venom, dataA, 0f, duration > 0f ? duration : 6f);
            case "AIFB":
            case "AIFW":
                return Orb(OrbEffect.Fire, dataA, 0f, 2f);
            case "AIGD":
                return Orb(OrbEffect.Corruption, dataA, 0f, 5f);
            case "AIZB":
                return Orb(OrbEffect.Freezing, dataA, 0f, 5f);
            case "AIOB":
            case "AIFT":
                return Orb(OrbEffect.Frost, dataA, 0f, duration > 0f ? duration : 3f);
            case "AIEV":
            case "AEEV":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, 0f, evasionPct: dataA);
            case "AIRN":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, 0f, manaRegen: dataA);
            case "AISI":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, 0f, sight: dataA);
            case "AISX":
                return new ItemBonus(0, 0, 0, 0f, 0f, dataA, 0f, 0f);
        }

        switch (ability.Code?.ToUpperInvariant())
        {
            case "AIAB":
                // 客户端 ItemBonusReader.cs:66：力=DataC、敏=DataA、智=DataB（原实现按 A/B/C 顺序，三围串位）。
                return new ItemBonus(
                    (int)MathF.Round(dataC),
                    (int)MathF.Round(dataA),
                    (int)MathF.Round(dataB),
                    0f, 0f, 0f, 0f, 0f);
            case "AIAT":
                return new ItemBonus(0, 0, 0, dataA, 0f, 0f, 0f, 0f);
            case "AIDE":
                return new ItemBonus(0, 0, 0, 0f, dataA, 0f, 0f, 0f);
            case "AIAS":
                return new ItemBonus(0, 0, 0, 0f, 0f, dataA, 0f, 0f);
            case "AIMS":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, dataA, 0f);
            case "AIVA":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, dataA);
            case "AIRM":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, 0f, manaRegen: dataA);
            case "AIMM":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, 0f, maxMana: dataA);
            case "AIMI":
            case "AIML":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, 0f, maxHp: dataA);
            case "AIAM":
                return new ItemBonus(0, (int)MathF.Round(dataA), 0, 0f, 0f, 0f, 0f, 0f);
            case "AIIM":
                return new ItemBonus(0, 0, (int)MathF.Round(dataB), 0f, 0f, 0f, 0f, 0f);
            case "AISM":
                return new ItemBonus((int)MathF.Round(dataC), 0, 0, 0f, 0f, 0f, 0f, 0f);
            case "AIXM":
                return new ItemBonus((int)MathF.Round(dataA), (int)MathF.Round(dataB), (int)MathF.Round(dataC),
                    0f, 0f, 0f, 0f, 0f);
            case "AICB":
                return Orb(OrbEffect.Corruption, dataA, 0f, 5f);
            case "AILB":
                return Orb(OrbEffect.Lightning, dataA, 0f, 5f);
            case "AISR":
                return new ItemBonus(0, 0, 0, 0f, 0f, 0f, 0f, 0f, magicReductionPct: dataB > 0f ? dataB : 0.33f);
            default:
                return Zero;
        }
    }

    private static ItemBonus Orb(OrbEffect effect, float damage, float secondary, float seconds)
        => new(0, 0, 0, 0f, 0f, 0f, 0f, 0f,
            orbDamage: damage, orbSecondary: secondary, orbEffect: effect, orbSeconds: seconds);

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
        if (LifestealPct != 0f) parts.Add($"吸血+{LifestealPct * 100f:0.#}%");
        if (ManaRegen != 0f) parts.Add($"回蓝+{ManaRegen:0.##}/s");
        if (EvasionPct != 0f) parts.Add($"闪避+{EvasionPct * 100f:0.#}%");
        if (Sight != 0f) parts.Add($"视野+{Sight:0}");
        if (OrbEffect != OrbEffect.None) parts.Add($"法球:{OrbEffect}(+{OrbDamage:0})");
        if (MaxHp != 0f) parts.Add($"生命上限+{MaxHp:0}");
        if (MaxMana != 0f) parts.Add($"法力上限+{MaxMana:0}");
        if (MagicReductionPct != 0f) parts.Add($"魔法减伤+{MagicReductionPct * 100f:0}%");
        return parts.Count == 0 ? "-" : string.Join(" ", parts);
    }
}