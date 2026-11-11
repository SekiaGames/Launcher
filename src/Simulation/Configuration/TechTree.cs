using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WorldEdit.Core.Formats.Slk;

namespace Launcher;

public sealed class UnitFuncRow
{
    public string[] Trains = Array.Empty<string>();
    public string[] Builds = Array.Empty<string>();
    public string[] Researches = Array.Empty<string>();
    public string[] UpgradeTo = Array.Empty<string>();
    public string[] Requires = Array.Empty<string>();
    /// <summary>UnitFunc 的 Sellitems：商店卖的物品 id 列表（地精商店 ngme 就是 phea,pman,pinv,…）。</summary>
    public string[] SellItems = Array.Empty<string>();
    public string MissileArt;
    public float MissileSpeed;
    public float MissileArc;
}

public sealed class UpgradeType
{
    public string Id;
    public string Name;
    public string Description;
    public int MaxLevel = 1;
    public int GoldBase;
    public int GoldMod;
    public int LumberBase;
    public int LumberMod;
    public float TimeBase = 30f;
    public float TimeMod;
    public float DamageBase;
    public float DamageMod;
    public float ArmorBase;
    public float ArmorMod;
    public float HpPercentBase;
    public float HpPercentMod;
    public string IconPath;
    public string[] Requires = Array.Empty<string>();
    public string[] LevelNames = Array.Empty<string>();

    public int GoldFor(int level) => GoldBase + GoldMod * Mathf.Max(0, level - 1);
    public int LumberFor(int level) => LumberBase + LumberMod * Mathf.Max(0, level - 1);
    public float SecondsFor(int level) => Mathf.Max(1f, TimeBase + TimeMod * Mathf.Max(0, level - 1));

    public float DamageAt(int level) => LevelValue(DamageBase, DamageMod, level);
    public float ArmorAt(int level) => LevelValue(ArmorBase, ArmorMod, level);
    public float HpPercentAt(int level) => LevelValue(HpPercentBase, HpPercentMod, level);

    private float LevelValue(float baseValue, float modValue, int level)
    {
        if (level <= 0) return 0f;
        return baseValue + modValue * (level - 1);
    }

    public string NameFor(int level)
    {
        if (LevelNames.Length == 0) return Name ?? Id;
        return LevelNames[Mathf.Clamp(level - 1, 0, LevelNames.Length - 1)];
    }
}

public static class TechTree
{
    private const string TextRoot = "res://extract/Texts/";

    private static readonly string[] FuncTables =
    {
        "MeleeV0UnitsHumanUnitFunc", "MeleeV0UnitsOrcUnitFunc", "MeleeV0UnitsUndeadUnitFunc",
        "MeleeV0UnitsNightElfUnitFunc", "MeleeV0UnitsNeutralUnitFunc", "MeleeV0UnitsCampaignUnitFunc",
    };

    private static readonly string[] StringTables =
    {
        "MeleeV0UnitsHumanUnitStrings", "MeleeV0UnitsOrcUnitStrings", "MeleeV0UnitsUndeadUnitStrings",
        "MeleeV0UnitsNightElfUnitStrings", "MeleeV0UnitsNeutralUnitStrings", "MeleeV0UnitsCampaignUnitStrings",
    };

    private static readonly string[] UpgradeStringTables =
    {
        "MeleeV0UnitsHumanUpgradeStrings", "MeleeV0UnitsOrcUpgradeStrings", "MeleeV0UnitsUndeadUpgradeStrings",
        "MeleeV0UnitsNightElfUpgradeStrings", "MeleeV0UnitsNeutralUpgradeStrings", "MeleeV0UnitsCampaignUpgradeStrings",
    };

    private static readonly string[] UpgradeFuncTables =
    {
        "MeleeV0UnitsHumanUpgradeFunc", "MeleeV0UnitsOrcUpgradeFunc", "MeleeV0UnitsUndeadUpgradeFunc",
        "MeleeV0UnitsNightElfUpgradeFunc", "MeleeV0UnitsNeutralUpgradeFunc", "MeleeV0UnitsCampaignUpgradeFunc",
    };

    private static Dictionary<string, Dictionary<string, string>> _unitFields;
    private static Dictionary<string, Dictionary<string, string>> _unitText;
    private static Dictionary<string, Dictionary<string, string>> _upgradeText;
    private static Dictionary<string, Dictionary<string, string>> _upgradeFields;
    private static readonly Dictionary<string, UnitFuncRow> Rows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, UpgradeType> Upgrades = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        _unitFields = LoadTables(FuncTables);
        _unitText = LoadTables(StringTables);
        _upgradeText = LoadTables(UpgradeStringTables);
        _upgradeFields = LoadTables(UpgradeFuncTables);
        LoadUpgradeData();

        GD.Print($"[TECH] 单位功能 {_unitFields.Count} 条，本单位中文名 {_unitText.Count} 条，科技 {Upgrades.Count} 项");
    }

    private static Dictionary<string, Dictionary<string, string>> LoadTables(string[] tables)
    {
        var merged = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string table in tables)
        {
            Variant json = Assets.ReadJson(TextRoot + table + ".json");
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
                string field = key[(dot + 1)..];
                if (!merged.TryGetValue(id, out Dictionary<string, string> row))
                {
                    row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    merged[id] = row;
                }
                row[field] = keys[keyVariant].AsString();
            }
        }
        return merged;
    }

    private static void LoadUpgradeData()
    {
        SlkTable table = War3Tables.Get("UnitsUpgradeData");
        if (table == null || table.Columns.Count == 0) return;

        foreach (int row in table.DataRows)
        {
            string id = table.Get(row, "upgradeid");
            if (string.IsNullOrWhiteSpace(id)) continue;
            id = id.Trim();

            var upgrade = new UpgradeType
            {
                Id = id,
                MaxLevel = Mathf.Max(1, table.GetInt(row, "maxlevel")),
                GoldBase = table.GetInt(row, "goldbase"),
                GoldMod = table.GetInt(row, "goldmod"),
                LumberBase = table.GetInt(row, "lumberbase"),
                LumberMod = table.GetInt(row, "lumbermod"),
                TimeBase = Number(table.Get(row, "timebase"), 30f),
                TimeMod = Number(table.Get(row, "timemod"), 0f),
            };

            foreach (int slot in new[] { 1, 2, 3, 4 })
            {
                string effect = table.Get(row, $"effect{slot}");
                if (string.IsNullOrWhiteSpace(effect) || effect.Trim() == "_") continue;

                float baseValue = Number(table.Get(row, $"base{slot}"), -1f);
                float modValue = Number(table.Get(row, $"mod{slot}"), -1f);
                if (baseValue < 0f) baseValue = 1f;
                if (modValue < 0f) modValue = upgrade.MaxLevel > 1 ? baseValue : 0f;

                switch (effect.Trim().ToLowerInvariant())
                {
                    case "ratd":
                    case "ratc":
                        upgrade.DamageBase = baseValue;
                        upgrade.DamageMod = modValue;
                        break;
                    case "rarm":
                        upgrade.ArmorBase = baseValue;
                        upgrade.ArmorMod = modValue;
                        break;
                    case "rhpo":
                        upgrade.HpPercentBase = baseValue;
                        upgrade.HpPercentMod = modValue;
                        break;
                }
            }

            if (_upgradeText != null && _upgradeText.TryGetValue(id, out Dictionary<string, string> text))
            {
                string names = Get(text, "Name");
                upgrade.Name = Clean(names).Split(',')[0].Trim();
                upgrade.LevelNames = Clean(names).Split(',', StringSplitOptions.TrimEntries);
                upgrade.Description = StripColor(Get(text, "Ubertip"));
            }
            if (string.IsNullOrEmpty(upgrade.Name)) upgrade.Name = id;

            if (_upgradeFields != null && _upgradeFields.TryGetValue(id, out Dictionary<string, string> fields))
            {
                string art = Get(fields, "Art").Split(',')[0].Trim();
                upgrade.IconPath = PathResolve.IconPng(art);
                upgrade.Requires = SplitList(Get(fields, "Requires"), fields, "Requires");
            }

            Upgrades[id] = upgrade;
        }
    }

    public static UnitFuncRow Row(string id)
    {
        EnsureLoaded();
        if (id == null) return null;
        if (Rows.TryGetValue(id, out UnitFuncRow cached)) return cached;

        if (_unitFields == null || !_unitFields.TryGetValue(id, out Dictionary<string, string> fields))
        {
            Rows[id] = null;
            return null;
        }

        var row = new UnitFuncRow
        {
            Trains = SplitList(Get(fields, "Trains")),
            Builds = SplitList(Get(fields, "Builds")),
            Researches = SplitList(Get(fields, "Researches")),
            UpgradeTo = SplitList(Get(fields, "Upgrade")),
            Requires = SplitList(Get(fields, "Requires"), fields, "Requires"),
            SellItems = SplitList(Get(fields, "Sellitems")),
            MissileArt = Get(fields, "Missileart"),
            MissileSpeed = Number(Get(fields, "Missilespeed"), 0f),
            MissileArc = Number(Get(fields, "Missilearc"), 0f),
        };
        Rows[id] = row;
        return row;
    }

    public static string UnitName(string id)
    {
        EnsureLoaded();
        if (_unitText != null && _unitText.TryGetValue(id, out Dictionary<string, string> text))
        {
            string name = StripColor(Get(text, "Name"));
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
        }
        return null;
    }

    public static string UnitTip(string id)
    {
        EnsureLoaded();
        if (_unitText == null || !_unitText.TryGetValue(id, out Dictionary<string, string> text)) return "";
        return StripColor(Get(text, "Ubertip"));
    }

    public static string UnitIconArt(string id)
    {
        EnsureLoaded();
        if (_unitFields == null || !_unitFields.TryGetValue(id, out Dictionary<string, string> fields)) return null;
        return Get(fields, "Art");
    }

    public static string UnitRequiresText(string id)
    {
        EnsureLoaded();
        if (_unitFields == null || !_unitFields.TryGetValue(id, out Dictionary<string, string> fields)) return "";
        return Get(fields, "Requires");
    }

    public static UpgradeType Upgrade(string id)
    {
        EnsureLoaded();
        return id != null && Upgrades.TryGetValue(id, out UpgradeType upgrade) ? upgrade : null;
    }

    public static IEnumerable<UpgradeType> AllUpgrades => Upgrades.Values;

    public static Texture2D Icon(string artPath)
    {
        if (string.IsNullOrWhiteSpace(artPath)) return null;
        if (IconCache.TryGetValue(artPath, out Texture2D cached)) return cached;

        string res = PathResolve.IconPng(artPath);
        Texture2D texture = Assets.LoadTexture(res);
        IconCache[artPath] = texture;
        return texture;
    }

    private static string Get(Dictionary<string, string> row, string field)
    {
        return row != null && row.TryGetValue(field, out string value) ? value : "";
    }

    private static string[] SplitList(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();

        var list = new List<string>();
        foreach (string part in csv.Split(',', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(part) || part == "_") continue;
            list.Add(part);
        }
        return list.ToArray();
    }

    private static string[] SplitList(string csv, Dictionary<string, string> row, string prefix)
    {
        var list = new List<string>(SplitList(csv));
        for (int slot = 1; slot <= 12; slot++)
        {
            string extra = Get(row, prefix + slot);
            if (string.IsNullOrWhiteSpace(extra)) continue;
            string trimmed = extra.Trim();
            if (!list.Contains(trimmed)) list.Add(trimmed);
        }
        return list.ToArray();
    }

    public static string StripColor(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '|' || i + 1 >= text.Length)
            {
                builder.Append(text[i]);
                continue;
            }

            char next = text[i + 1];
            if (next is 'c' or 'C' && i + 9 < text.Length)
            {
                i += 9;
                continue;
            }
            if (next is 'r' or 'R' or 'n' or 'N')
            {
                i += 1;
                if (next is 'n' or 'N') builder.Append('\n');
                continue;
            }
            builder.Append(text[i]);
        }
        return builder.ToString();
    }

    private static string Clean(string csv)
    {
        return StripColor(csv ?? "").Replace("\"", "");
    }

    private static float Number(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "-") return fallback;
        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }
}
