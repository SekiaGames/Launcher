using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class UnitFuncRow
{
    public string[] Trains = Array.Empty<string>();
    public string[] Builds = Array.Empty<string>();
    public string[] Researches = Array.Empty<string>();
    public string[] UpgradeTo = Array.Empty<string>();
    public string[] Requires = Array.Empty<string>();
    public string[] SellItems = Array.Empty<string>();
    public string[] MakeItems = Array.Empty<string>();
    /// <summary>UnitFunc 的 Sellunits：中立建筑卖的兵（酒馆 ntav = Nalc,Ntin,…；雇佣兵营地 nmer = nftb,nfsp,…）。</summary>
    public string[] SellUnits = Array.Empty<string>();
    /// <summary>只取 Requires 本体（不含 Requires1..12），买兵前置按它判，多档要求是「或」不是「与」。</summary>
    public string[] RequiresPrimary = Array.Empty<string>();
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
    public float ExtraDamageBase;
    public float ExtraDamageMod;
    public float ArmorBase;
    public float ArmorMod;
    public float HpPercentBase;
    public float HpPercentMod;
    public float FlatHpBase;
    public float FlatHpMod;
    public float MaxManaBase;
    public float MaxManaMod;
    public float ManaRegenBase;
    public float ManaRegenMod;
    public float HpRegenBase;
    public float HpRegenMod;
    public float RangeBase;
    public float RangeMod;
    public float AttackSpeedBase;
    public float AttackSpeedMod;
    public float MoveSpeedBase;
    public float MoveSpeedMod;
    public float LumberYieldBase;
    public float LumberYieldMod;
    public float SpikedBase;
    public float SpikedMod;
    public float TargetCountBase;
    public float TargetCountMod;
    public float SummonLifeBase;
    public float SummonLifeMod;
    public float SummonLevelBase;
    public float SummonLevelMod;
    public string IconPath;
    public string[] Requires = Array.Empty<string>();
    public string[] LevelNames = Array.Empty<string>();

    public int GoldFor(int level) => GoldBase + GoldMod * Mathf.Max(0, level - 1);
    public int LumberFor(int level) => LumberBase + LumberMod * Mathf.Max(0, level - 1);
    public float SecondsFor(int level) => Mathf.Max(1f, TimeBase + TimeMod * Mathf.Max(0, level - 1));

    public float DamageAt(int level) => LevelValue(DamageBase, DamageMod, level);
    public float ExtraDamageAt(int level) => LevelValue(ExtraDamageBase, ExtraDamageMod, level);
    public float ArmorAt(int level) => LevelValue(ArmorBase, ArmorMod, level);
    public float HpPercentAt(int level) => LevelValue(HpPercentBase, HpPercentMod, level);
    public float FlatHpAt(int level) => LevelValue(FlatHpBase, FlatHpMod, level);
    public float MaxManaAt(int level) => LevelValue(MaxManaBase, MaxManaMod, level);
    public float ManaRegenAt(int level) => LevelValue(ManaRegenBase, ManaRegenMod, level);
    public float HpRegenAt(int level) => LevelValue(HpRegenBase, HpRegenMod, level);
    public float RangeAt(int level) => LevelValue(RangeBase, RangeMod, level);
    public float AttackSpeedAt(int level) => LevelValue(AttackSpeedBase, AttackSpeedMod, level);
    public float MoveSpeedAt(int level) => LevelValue(MoveSpeedBase, MoveSpeedMod, level);
    public float LumberYieldAt(int level) => LevelValue(LumberYieldBase, LumberYieldMod, level);
    public float SpikedAt(int level) => LevelValue(SpikedBase, SpikedMod, level);
    public float TargetCountAt(int level) => LevelValue(TargetCountBase, TargetCountMod, level);
    public float SummonLifeAt(int level) => LevelValue(SummonLifeBase, SummonLifeMod, level);
    public float SummonLevelAt(int level) => LevelValue(SummonLevelBase, SummonLevelMod, level);

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

    // 客户端 War3ObjectData.Load 只读基础 Units\*UnitFunc.txt 这六族；
    // Melee_V0 是 RoC 精简档（没有酒馆 ntav、四族商店与 TFT 单位），不能当技术树数据源。
    private static readonly string[] FuncTables =
    {
        "UnitsHumanUnitFunc", "UnitsOrcUnitFunc", "UnitsUndeadUnitFunc",
        "UnitsNightElfUnitFunc", "UnitsNeutralUnitFunc", "UnitsCampaignUnitFunc",
    };

    private static readonly string[] StringTables =
    {
        "UnitsHumanUnitStrings", "UnitsOrcUnitStrings", "UnitsUndeadUnitStrings",
        "UnitsNightElfUnitStrings", "UnitsNeutralUnitStrings", "UnitsCampaignUnitStrings",
    };

    private static readonly string[] UpgradeStringTables =
    {
        "UnitsHumanUpgradeStrings", "UnitsOrcUpgradeStrings", "UnitsUndeadUpgradeStrings",
        "UnitsNightElfUpgradeStrings", "UnitsNeutralUpgradeStrings", "UnitsCampaignUpgradeStrings",
    };

    private static readonly string[] UpgradeFuncTables =
    {
        "UnitsHumanUpgradeFunc", "UnitsOrcUpgradeFunc", "UnitsUndeadUpgradeFunc",
        "UnitsNightElfUpgradeFunc", "UnitsNeutralUpgradeFunc", "UnitsCampaignUpgradeFunc",
    };

    /// <summary>ItemFunc.txt 里的依赖别名段（TALT=任一祭坛、TWN2=二级主基…）。</summary>
    private const string ItemFuncTable = "UnitsItemFunc";

    private static Dictionary<string, Dictionary<string, string>> _unitFields;
    private static Dictionary<string, Dictionary<string, string>> _unitText;
    private static Dictionary<string, Dictionary<string, string>> _upgradeText;
    private static Dictionary<string, Dictionary<string, string>> _upgradeFields;
    private static Dictionary<string, Dictionary<string, string>> _itemFields;
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
        _itemFields = LoadTables(new[] { ItemFuncTable });
        LoadUpgradeData();

        GD.Print($"[TECH] 单位功能 {_unitFields.Count} 条，本单位中文名 {_unitText.Count} 条，"
            + $"依赖别名 {CountDependencyGroups()} 项，科技 {Upgrades.Count} 项");
    }

    private static int CountDependencyGroups()
    {
        int count = 0;
        foreach (Dictionary<string, string> row in _itemFields.Values)
        {
            if (!string.IsNullOrWhiteSpace(Get(row, "DependencyOr"))) count++;
        }
        return count;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadTables(string[] tables)
    {
        var merged = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string table in tables)
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
        SlkTable table = War3SlkReader.Get("UnitsUpgradeData");
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
                        upgrade.DamageBase = baseValue;
                        upgrade.DamageMod = modValue;
                        break;
                    case "ratx":
                        upgrade.ExtraDamageBase = baseValue;
                        upgrade.ExtraDamageMod = modValue;
                        break;
                    case "ratc":
                        upgrade.TargetCountBase = baseValue;
                        upgrade.TargetCountMod = modValue;
                        break;
                    case "rarm":
                        upgrade.ArmorBase = baseValue;
                        upgrade.ArmorMod = modValue;
                        break;
                    case "rhpo":
                        upgrade.HpPercentBase = baseValue;
                        upgrade.HpPercentMod = modValue;
                        break;
                    case "rhpx":
                        upgrade.FlatHpBase = baseValue;
                        upgrade.FlatHpMod = modValue;
                        break;
                    case "rmnx":
                        upgrade.MaxManaBase = baseValue;
                        upgrade.MaxManaMod = modValue;
                        break;
                    case "rmnr":
                        upgrade.ManaRegenBase = baseValue;
                        upgrade.ManaRegenMod = modValue;
                        break;
                    case "rhpr":
                        upgrade.HpRegenBase = baseValue;
                        upgrade.HpRegenMod = modValue;
                        break;
                    case "ratr":
                        upgrade.RangeBase = baseValue;
                        upgrade.RangeMod = modValue;
                        break;
                    case "rats":
                        upgrade.AttackSpeedBase = baseValue;
                        upgrade.AttackSpeedMod = modValue;
                        break;
                    case "rmvx":
                        upgrade.MoveSpeedBase = baseValue;
                        upgrade.MoveSpeedMod = modValue;
                        break;
                    case "rlum":
                        upgrade.LumberYieldBase = baseValue;
                        upgrade.LumberYieldMod = modValue;
                        break;
                    case "rspi":
                        upgrade.SpikedBase = baseValue;
                        upgrade.SpikedMod = modValue;
                        break;
                    case "rrai":
                        upgrade.SummonLifeBase = baseValue;
                        upgrade.SummonLifeMod = modValue;
                        break;
                    case "rlev":
                        upgrade.SummonLevelBase = baseValue;
                        upgrade.SummonLevelMod = modValue;
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
            RequiresPrimary = SplitList(Get(fields, "Requires")),
            SellItems = SplitList(Get(fields, "Sellitems")),
            MakeItems = SplitList(Get(fields, "Makeitems")),
            SellUnits = SplitList(Get(fields, "Sellunits")),
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

    /// <summary>
    /// 依赖别名的或列表：ItemFunc.txt 里 TALT = halt,oalt,eate,uaod（任一祭坛），
    /// TWN2/TWN3 = 二/三级主基。买中立英雄的 Requires 用的是这些别名，不是建筑 id。
    /// </summary>
    public static string[] DependencyOr(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id) || _itemFields == null) return Array.Empty<string>();
        if (!_itemFields.TryGetValue(id, out Dictionary<string, string> fields)) return Array.Empty<string>();
        return SplitList(Get(fields, "DependencyOr"));
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
