using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace Launcher;

/// <summary>
/// 物品类型。数据来自 Units\ItemData.slk + Units\*ItemStrings（名字）。
/// 对标客户端 Simulation/Configuration/ItenBonusReader 与 Src/Items。
/// 物品的属性加成来自 ItemData.abilList 指向的能力，等技能系统接上再消费。
/// </summary>
public sealed class ItemType
{
    public string Id;
    public string Name;
    public string Tip;
    /// <summary>物编 Ubertip：真正的说明文字（Tip 只是商店按钮那句「购买XXX(A)」）。</summary>
    public string Ubertip;
    public string ModelPath;
    public float Scale = 1f;
    public int Level = 1;
    public string Class = "";
    public int GoldCost;
    public int LumberCost;
    public bool Droppable;
    public bool Usable;
    public bool PowerUp;
    public bool PickRandom;
    public string AbilList = "";
    public int Uses;
    public bool Perishable;
    /// <summary>ItemData.stockMax / stockRegen / stockStart：商店库存上限、补一件的秒数、开局延迟。</summary>
    public int StockMax;
    public float StockRegen = 120f;
    public float StockStart;
    /// <summary>图标。物编的 Art 列没导出，按 abilList 的基类码给原版图标名。</summary>
    public string IconArt = @"ReplaceableTextures\CommandButtons\BTNGoldRing";

    public ItemBonus Bonus => ItemBonus.ForItem(this);
    public ItemConsumable Consumable => ItemConsumable.Of(this);
    public string BonusText => Bonus.IsZero ? Consumable.ToString() : Bonus.ToString();
}

public static class ItemCatalog
{
    // 与单位技术树同源：客户端读基础 Units\ItemStrings，Melee_V0 档只有 RoC 那批物品名。
    private const string StringsPath = "res://extract/Texts/UnitsItemStrings.json";

    private static readonly Dictionary<string, ItemType> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> Rows = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _strings;
    private static bool _indexed;

    private static void EnsureIndex()
    {
        if (_indexed) return;
        _indexed = true;

        SlkTable table = War3SlkReader.ItemData();
        foreach (int row in table.DataRows)
        {
            string id = table.Get(row, "itemID");
            if (string.IsNullOrWhiteSpace(id)) continue;
            Rows[id.Trim()] = row;
        }
        GD.Print($"[ITEM] 物品索引 {Rows.Count} 条");
    }

    public static bool Exists(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        EnsureIndex();
        return Rows.ContainsKey(id);
    }

    public static ItemType Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out ItemType cached)) return cached;
        EnsureIndex();
        if (!Rows.TryGetValue(id, out int row)) return null;

        SlkTable table = War3SlkReader.ItemData();
        string file = table.Get(row, "file") ?? "";
        var type = new ItemType
        {
            Id = id,
            Name = id,
            ModelPath = PathResolve.ModelScene(file),
            Scale = Number(table.Get(row, "scale"), 1f),
            Level = Mathf.Max(1, table.GetInt(row, "Level", 1)),
            Class = (table.Get(row, "class") ?? "").Trim(),
            GoldCost = table.GetInt(row, "goldcost"),
            LumberCost = table.GetInt(row, "lumbercost"),
            Droppable = table.Get(row, "droppable") == "1",
            Uses = Mathf.Max(0, table.GetInt(row, "uses", 0)),
            StockMax = Mathf.Max(0, table.GetInt(row, "stockMax", 0)),
            StockRegen = Number(table.Get(row, "stockRegen"), 120f),
            StockStart = Number(table.Get(row, "stockStart"), 0f),
            Perishable = table.Get(row, "perishable") == "1",
            Usable = table.Get(row, "usable") == "1",
            PowerUp = table.Get(row, "powerup") == "1",
            PickRandom = table.Get(row, "pickRandom") == "1",
            AbilList = (table.Get(row, "abilList") ?? "").Trim(),
        };

        string localized = String(id, "Name");
        if (!string.IsNullOrWhiteSpace(localized)) type.Name = localized;
        type.Tip = String(id, "Tip") ?? "";
        type.Ubertip = String(id, "Ubertip") ?? "";
        type.IconArt = IconFor(type);

        Cache[id] = type;
        return type;
    }

    /// <summary>物编 Art 列没导出，按 abilList 的基类码挑原版图标（都对得上 extract 里实际存在的 png）。</summary>
    private static string IconFor(ItemType item)
    {
        if (string.IsNullOrWhiteSpace(item.AbilList)) return DefaultIcon;

        foreach (string id in item.AbilList.Split(',', ';', StringSplitOptions.TrimEntries))
        {
            if (id.Length < 4) continue;
            AbilityType ability = AbilityCatalog.Get(id[..4]);
            string icon = ability?.Code?.ToUpperInvariant() switch
            {
                "AIHE" => @"ReplaceableTextures\CommandButtons\BTNPotionGreen",
                "AIMA" or "AIMR" => @"ReplaceableTextures\CommandButtons\BTNPotionBlue",
                "AIRG" => @"ReplaceableTextures\CommandButtons\BTNMinorRejuvPotion",
                "AIAB" => @"ReplaceableTextures\CommandButtons\BTNPeriapt",
                "AIAT" => @"ReplaceableTextures\CommandButtons\BTNClawsOfAttack",
                "AIDE" => @"ReplaceableTextures\CommandButtons\BTNRingGreen",
                "AIAS" => @"ReplaceableTextures\CommandButtons\BTNGauntletsOfOgrePower",
                _ => null,
            };
            if (icon != null) return icon;
        }
        return DefaultIcon;
    }

    private const string DefaultIcon = @"ReplaceableTextures\CommandButtons\BTNGoldRing";

    /// <summary>按等级挑一个可掉落物品（掉落表接上之前的替身，只用于调试）。</summary>
    public static ItemType RandomByLevel(int level)
    {
        EnsureIndex();
        var candidates = new List<string>();
        foreach (KeyValuePair<string, int> pair in Rows)
        {
            ItemType type = Get(pair.Key);
            if (type == null || !type.Droppable) continue;
            if (Mathf.Abs(type.Level - level) > 1) continue;
            candidates.Add(pair.Key);
        }
        if (candidates.Count == 0) return null;
        return Get(candidates[(int)(GD.Randi() % candidates.Count)]);
    }

    private static string String(string id, string field)
    {
        _strings ??= LoadStrings();
        return _strings.TryGetValue(id + "." + field, out string value) ? value : null;
    }

    private static Dictionary<string, string> LoadStrings()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Variant json = Assets.ReadJson(StringsPath);
        if (json.VariantType != Variant.Type.Dictionary) return result;

        Godot.Collections.Dictionary root = json.AsGodotDictionary();
        if (!root.ContainsKey("keys")) return result;

        foreach (KeyValuePair<Variant, Variant> pair in root["keys"].AsGodotDictionary())
        {
            result[pair.Key.AsString()] = pair.Value.AsString();
        }
        return result;
    }

    private static float Number(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        return float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
