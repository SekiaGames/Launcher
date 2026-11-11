using System;

namespace Launcher;

/// <summary>
/// 消耗品的即时效果。对标客户端物品使用路径：物品本身没有数值，
/// 效果写在 `ItemData.abilList` 指向的能力里，这里按基类码翻译成「回血 / 回蓝」。
///
/// 码的含义用实际物品反查确认：
/// <list type="bullet">
/// <item>AIhe —— 治疗药水（phea DataA=250）</item>
/// <item>AIma —— 法力药水（pman DataA=150）</item>
/// <item>AIrg —— 生命/法力双回（rej1 100/25、pclr 0/200）</item>
/// <item>AImr —— 回蓝（sman 100）</item>
/// </list>
/// </summary>
public readonly struct ItemConsumable
{
    public readonly float Heal;
    public readonly float Mana;

    public ItemConsumable(float heal, float mana)
    {
        Heal = heal;
        Mana = mana;
    }

    public static readonly ItemConsumable Empty = new(0f, 0f);

    public bool IsEmpty => Heal <= 0f && Mana <= 0f;

    public static ItemConsumable Of(ItemType item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.AbilList)) return Empty;

        ItemConsumable total = Empty;
        foreach (string id in item.AbilList.Split(',', ';', StringSplitOptions.TrimEntries))
        {
            if (id.Length < 4) continue;
            AbilityType ability = AbilityCatalog.Get(id[..4]);
            if (ability == null) continue;
            total += Of(ability);
        }
        return total;
    }

    public static ItemConsumable Of(AbilityType ability)
    {
        float a = ability.DataAt(1, 'A');
        float b = ability.DataAt(1, 'B');

        return ability.Code?.ToUpperInvariant() switch
        {
            "AIHE" => new ItemConsumable(a, 0f),
            "AIMA" => new ItemConsumable(0f, a),
            "AIMR" => new ItemConsumable(0f, a),
            "AIRG" => new ItemConsumable(a, b),
            _ => Empty,
        };
    }

    public static ItemConsumable operator +(ItemConsumable left, ItemConsumable right)
        => new(left.Heal + right.Heal, left.Mana + right.Mana);

    public override string ToString()
    {
        if (IsEmpty) return "-";
        if (Heal > 0f && Mana > 0f) return $"生命+{Heal:0} 法力+{Mana:0}";
        return Heal > 0f ? $"生命+{Heal:0}" : $"法力+{Mana:0}";
    }
}
