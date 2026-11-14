using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static class AbilityArt
{
    private static readonly string[] FuncTables =
    {
        "UnitsHumanAbilityFunc", "UnitsOrcAbilityFunc", "UnitsUndeadAbilityFunc",
        "UnitsNightElfAbilityFunc", "UnitsNeutralAbilityFunc", "UnitsCampaignAbilityFunc",
        "UnitsCommonAbilityFunc", "UnitsItemAbilityFunc",
    };

    private static readonly char[] ArtSeparators = { ',', ';' };

    private static readonly IReadOnlyList<string> Origin =
        Array.AsReadOnly(new[] { "origin" });

    private static Dictionary<string, Dictionary<string, string>> _rows;

    private static Dictionary<string, Dictionary<string, string>> Rows
    {
        get
        {
            if (_rows != null) return _rows;
            var merged = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string table in FuncTables)
            {
                Variant json = GeneratedTexts.Get(table);
                if (json.VariantType != Variant.Type.Dictionary) continue;
                Godot.Collections.Dictionary root = json.AsGodotDictionary();
                if (!root.ContainsKey("keys")) continue;
                Godot.Collections.Dictionary keys = root["keys"].AsGodotDictionary();
                foreach (Variant keyVariant in keys.Keys)
                {
                    string key = keyVariant.AsString();
                    int dot = key.IndexOf('.');
                    if (dot <= 0 || dot == key.Length - 1) continue;
                    string id = key[..dot];
                    string artField = key[(dot + 1)..];
                    if (!merged.TryGetValue(id, out Dictionary<string, string> row))
                    {
                        row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        merged[id] = row;
                    }
                    if (row.ContainsKey(artField)) continue;
                    row[artField] = keys[keyVariant].AsString();
                }
            }
            _rows = merged;
            GD.Print($"[ART] 技能美术表 {_rows.Count} 条");
            return _rows;
        }
    }

    public static IReadOnlyList<string> Arts(IRuntimeObjectData data, string rawcode, int level,
        string artField, string rawField)
        => Split(Text(data, rawcode, level, artField, rawField));

    public static IReadOnlyCollection<string> Ids => Rows.Keys;

    public static IReadOnlyList<string> Attachments(IRuntimeObjectData data, string rawcode, int level,
        string artField, string rawField)
    {
        int count = Math.Max(1, Count(data, rawcode, level, artField));
        List<string> found = null;
        for (int index = 0; index < count; index++)
        {
            string name = index == 0 ? artField : artField + index;
            string rawName = index == 0 ? rawField : rawField + index;
            string value = Text(data, rawcode, level, name, rawName);
            if (Missing(value)) continue;
            (found ??= new List<string>(count)).Add(value.Trim());
        }
        return found ?? Origin;
    }

    public static string FirstArt(IRuntimeObjectData data, string rawcode, int level,
        string artField, string rawField)
    {
        IReadOnlyList<string> arts = Arts(data, rawcode, level, artField, rawField);
        return arts.Count > 0 ? arts[0] : "";
    }

    public static string FirstAttachment(IRuntimeObjectData data, string rawcode, int level,
        string artField, string rawField)
    {
        IReadOnlyList<string> attachments = Attachments(data, rawcode, level, artField, rawField);
        return attachments.Count > 0 ? attachments[0] : "origin";
    }

    private static string Text(IRuntimeObjectData data, string rawcode, int level,
        string artField, string rawField)
    {
        if (data != null)
        {
            int index = Math.Max(1, level);
            string value = data.GetString(artField, index);
            if (Missing(value) && !string.IsNullOrEmpty(rawField)) value = data.GetString(rawField, index);
            if (!Missing(value)) return value;
        }
        return Table(rawcode, artField, level);
    }

    private static int Count(IRuntimeObjectData data, string rawcode, int level, string artField)
    {
        string value = Text(data, rawcode, level, artField + "count", "");
        return int.TryParse(value, out int parsed) ? parsed : 1;
    }

    private static string Table(string rawcode, string artField, int level)
    {
        if (string.IsNullOrEmpty(rawcode) || string.IsNullOrEmpty(artField)) return "";
        if (!Rows.TryGetValue(rawcode, out Dictionary<string, string> row)) return "";
        if (row.TryGetValue(artField, out string value) && !Missing(value)) return value;
        string levelled = artField + Math.Max(1, level);
        if (row.TryGetValue(levelled, out value) && !Missing(value)) return value;
        return "";
    }

    private static IReadOnlyList<string> Split(string value)
    {
        if (Missing(value)) return Array.Empty<string>();
        List<string> arts = null;
        foreach (string part in value.Split(ArtSeparators))
        {
            string art = part.Trim();
            if (art.Length == 0 || art == "_") continue;
            arts ??= new List<string>(2);
            bool duplicate = false;
            foreach (string existing in arts)
            {
                if (!string.Equals(existing, art, StringComparison.OrdinalIgnoreCase)) continue;
                duplicate = true;
                break;
            }
            if (!duplicate) arts.Add(art);
        }
        return arts ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    private static bool Missing(string value) => string.IsNullOrWhiteSpace(value) || value.Trim() == "_";
}
