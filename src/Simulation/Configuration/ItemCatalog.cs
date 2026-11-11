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
}

public static class ItemCatalog
{
    private const string StringsPath = "res://extract/Texts/MeleeV0UnitsItemStrings.json";

    private static readonly Dictionary<string, ItemType> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> Rows = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _strings;
    private static bool _indexed;

    private static void EnsureIndex()
    {
        if (_indexed) return;
        _indexed = true;

        SlkTable table = War3Tables.ItemData();
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

        SlkTable table = War3Tables.ItemData();
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
            Usable = table.Get(row, "usable") == "1",
            PowerUp = table.Get(row, "powerup") == "1",
            PickRandom = table.Get(row, "pickRandom") == "1",
            AbilList = (table.Get(row, "abilList") ?? "").Trim(),
        };

        string localized = String(id, "Name");
        if (!string.IsNullOrWhiteSpace(localized)) type.Name = localized;
        type.Tip = String(id, "Tip") ?? "";

        Cache[id] = type;
        return type;
    }

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
