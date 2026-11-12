using System;

namespace Launcher;

/// <summary>
/// 消耗品的即时效果。对标客户端物品使用路径：物品本身没有数值，
/// 效果写在 `ItemData.abilList` 指向的能力里，这里按**实际基类码**翻译成「回血 / 回蓝」。
///
/// 码的含义用实际物品反查确认（MeleeV0 ItemData 里 phea/pman 挂的就是 AIh1/AIm1）：
/// <list type="item">
/// <item>AIh1/AIh2/AIh3/AIhx —— 治疗药水（phea DataA=250、pghe 500）</item>
/// <item>AIm1/AIm2 —— 法力药水（pman 150、pgma 300）</item>
/// <item>AIre —— 恢复药水（pres 500/200）</item>
/// <item>AIp1..AIp4 —— 重生药水（100/25 … 400/125）</item>
/// <item>AIsl —— 生命恢复卷轴（sreg 225）</item>
/// <item>AIrl —— 医疗剂（hslv 400）</item>
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
            "AIH1" or "AIH2" or "AIH3" or "AIHX" or "AIHE" => new ItemConsumable(a, 0f),
            "AIM1" or "AIM2" or "AIMA" or "AIMR" => new ItemConsumable(0f, a),
            "AIRE" or "AIRG" or "AIP1" or "AIP2" or "AIP3" or "AIP4" => new ItemConsumable(a, b),
            "AISL" or "AIRL" => new ItemConsumable(a, 0f),
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
