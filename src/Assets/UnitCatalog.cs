using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace Launcher;

public sealed class UnitType
{
    public string Id;
    public string Name;
    public string Tip;
    public string IconPath;
    public string ModelPath;
    public float ModelScale = 1f;
    public float Hp = 100f;
    public float Speed = 270f;
    public bool IsBuilding;
    public bool CanAttack;
    public float AttackRange = 100f;
    public float AttackCooldown = 1.5f;
    public float MinDamage = 5f;
    public float MaxDamage = 10f;
    public float AcquireRange = 500f;
    public float Armor;
    public int GoldCost;
    public int LumberCost;
    public float TrainSeconds = 15f;
    public float BuildSeconds = 30f;
    public int FoodUsed;
    public int FoodMade;

    public string[] Trains = Array.Empty<string>();
    public string[] Builds = Array.Empty<string>();
    public string[] Researches = Array.Empty<string>();
    public string[] UpgradeTo = Array.Empty<string>();
    public string[] Requires = Array.Empty<string>();
    public string[] UpgradesApplied = Array.Empty<string>();

    public bool IsWorkerType => Builds.Length > 0;
}

public static class UnitCatalog
{
    private static readonly Dictionary<string, UnitType> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> UiRows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> BalanceRows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> WeaponRows = new(StringComparer.OrdinalIgnoreCase);
    private static bool _indexed;

    private static void EnsureIndex()
    {
        if (_indexed) return;
        _indexed = true;
        Index(War3Tables.UnitUI(), "unitUIID", UiRows);
        Index(War3Tables.UnitBalance(), "unitBalanceID", BalanceRows);
        Index(War3Tables.UnitWeapons(), "unitWeapID", WeaponRows);
        GD.Print($"[UNIT] 索引 unitUI={UiRows.Count} balance={BalanceRows.Count} weapons={WeaponRows.Count}");
    }

    private static void Index(SlkTable table, string column, Dictionary<string, int> target)
    {
        foreach (int row in table.DataRows)
        {
            string id = table.Get(row, column);
            if (string.IsNullOrWhiteSpace(id)) continue;
            target[id.Trim()] = row;
        }
    }

    public static UnitType Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out UnitType cached)) return cached;
        EnsureIndex();

        UnitType type = Build(id);
        Cache[id] = type;
        return type;
    }

    private static UnitType Build(string id)
    {
        if (!UiRows.TryGetValue(id, out int uiRow)) return null;

        SlkTable ui = War3Tables.UnitUI();
        string file = ui.Get(uiRow, "file");
        if (string.IsNullOrWhiteSpace(file)) return null;

        var type = new UnitType
        {
            Id = id,
            ModelPath = PathResolve.ModelScene(file),
            ModelScale = Number(ui.Get(uiRow, "modelScale"), 1f),
            Name = file.Replace('\\', '/').Split('/')[^1],
        };

        if (BalanceRows.TryGetValue(id, out int balanceRow))
        {
            SlkTable balance = War3Tables.UnitBalance();
            type.Hp = Number(balance.Get(balanceRow, "HP"), 100f);
            type.Speed = Number(balance.Get(balanceRow, "spd"), 270f);
            type.Armor = Number(balance.Get(balanceRow, "def"), 0f);
            type.IsBuilding = balance.Get(balanceRow, "isbldg") == "1";
            type.GoldCost = balance.GetInt(balanceRow, "goldcost");
            type.LumberCost = balance.GetInt(balanceRow, "lumbercost");
            type.TrainSeconds = Number(balance.Get(balanceRow, "reptm"), 15f);
            type.BuildSeconds = Number(balance.Get(balanceRow, "bldtm"), 30f);
            type.FoodUsed = Mathf.Max(0, balance.GetInt(balanceRow, "fused"));
            type.FoodMade = Mathf.Max(0, balance.GetInt(balanceRow, "fmade"));
            type.UpgradesApplied = SplitList(balance.Get(balanceRow, "upgrades"));
            string comment = balance.Get(balanceRow, "comment(s)");
            if (!string.IsNullOrWhiteSpace(comment)) type.Name = comment.Trim();
        }

        if (WeaponRows.TryGetValue(id, out int weaponRow))
        {
            SlkTable weapons = War3Tables.UnitWeapons();
            string kind = weapons.Get(weaponRow, "weapTp1");
            type.CanAttack = !string.IsNullOrWhiteSpace(kind)
                && !kind.Equals("none", StringComparison.OrdinalIgnoreCase);
            type.AttackRange = Number(weapons.Get(weaponRow, "rangeN1"), 90f);
            type.AttackCooldown = Number(weapons.Get(weaponRow, "cool1"), 1.5f);
            type.MinDamage = Number(weapons.Get(weaponRow, "mindmg1"), 5f);
            type.MaxDamage = Number(weapons.Get(weaponRow, "maxdmg1"), 10f);
            type.AcquireRange = Number(weapons.Get(weaponRow, "acquire"), 500f);
        }

        UnitFuncRow row = TechTree.Row(id);
        if (row != null)
        {
            type.Trains = row.Trains;
            type.Builds = row.Builds;
            type.Researches = row.Researches;
            type.UpgradeTo = row.UpgradeTo;
            type.Requires = row.Requires;
        }

        string localized = TechTree.UnitName(id);
        if (!string.IsNullOrWhiteSpace(localized)) type.Name = localized;
        type.Tip = TechTree.UnitTip(id);
        type.IconPath = PathResolve.IconPng(TechTree.UnitIconArt(id));

        return type;
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

    private static float Number(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        return float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
