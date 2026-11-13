using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public static class War3SlkReader
{
    private static readonly Dictionary<string, SlkTable> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static SlkTable Get(string name)
    {
        if (Cache.TryGetValue(name, out SlkTable cached)) return cached;

        SlkTable table = Load(name);
        Cache[name] = table;
        return table;
    }

    public static bool Has(string name) => Generated.SlkTables.All.ContainsKey(name);

    private static SlkTable Load(string name)
    {
        string key = name;
        if (!Generated.SlkTables.All.ContainsKey(key))
        {
            string match = null;
            foreach (string candidate in Generated.SlkTables.All.Keys)
            {
                if (!candidate.EndsWith(name, StringComparison.OrdinalIgnoreCase)) continue;
                match = candidate;
                break;
            }
            if (match == null)
            {
                GD.PushError($"[TABLE] 生成的表里找不到 {name}");
                return SlkTable.FromRows(Array.Empty<string>(), Array.Empty<string[]>());
            }
            key = match;
        }

        var source = Generated.SlkTables.All[key];
        var rows = new List<string[]>(source.Rows.Length);
        foreach (string row in source.Rows)
            rows.Add(row.Length == 0 ? Array.Empty<string>() : row.Split('\t'));
        return SlkTable.FromRows(source.Columns, rows.ToArray());
    }

    public static SlkTable UnitUI() => Get("UnitsunitUI");
    public static SlkTable UnitBalance() => Get("UnitsUnitBalance");
    public static SlkTable UnitWeapons() => Get("UnitsUnitWeapons");
    public static SlkTable UnitData() => Get("UnitsUnitData");
    public static SlkTable UnitAbilities() => Get("UnitsUnitAbilities");
    public static SlkTable DestructableData() => Get("UnitsDestructableData");
    public static SlkTable ItemData() => Get("UnitsItemData");
    public static SlkTable Terrain() => Get("TerrainArtTerrain");
    public static SlkTable CliffTypes() => Get("TerrainArtCliffTypes");
    public static SlkTable Water() => Get("TerrainArtWater");
    public static SlkTable Doodads() => Get("DoodadsDoodads");
}
