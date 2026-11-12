using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using WorldEdit.Core.Formats.Slk;

namespace Launcher;

public static class War3SlkReader
{
    private const string Root = "res://extract/Tables/";

    private static readonly Dictionary<string, SlkTable> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static string[] _files;

    private static string[] Files
    {
        get
        {
            if (_files != null) return _files;
            var list = new List<string>();
            foreach (string file in DirAccess.GetFilesAt(Root))
            {
                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) list.Add(file[..^5]);
                else if (file.EndsWith(".json.remap", StringComparison.OrdinalIgnoreCase)) list.Add(file[..^11]);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            _files = list.ToArray();
            return _files;
        }
    }

    private static string ResolveFile(string name)
    {
        foreach (string file in Files)
        {
            if (string.Equals(file, name, StringComparison.OrdinalIgnoreCase)) return file;
        }
        foreach (string file in Files)
        {
            if (file.EndsWith(name, StringComparison.OrdinalIgnoreCase)) return file;
        }
        return null;
    }

    public static SlkTable Get(string name)
    {
        if (Cache.TryGetValue(name, out SlkTable cached)) return cached;

        SlkTable table = Load(name);
        Cache[name] = table;
        return table;
    }

    public static bool Has(string name)
    {
        return ResolveFile(name) != null;
    }

    private static SlkTable Load(string name)
    {
        string file = ResolveFile(name);
        if (file == null)
        {
            GD.PushError($"[TABLE] Tables 里找不到 {name}");
            return SlkTable.FromRows(Array.Empty<string>(), Array.Empty<string[]>());
        }

        string path = Root + file + ".json";
        using FileAccess stream = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (stream == null)
        {
            GD.PushError($"[TABLE] 打不开 {path}");
            return SlkTable.FromRows(Array.Empty<string>(), Array.Empty<string[]>());
        }

        try
        {
            return Parse(file, stream.GetAsText());
        }
        catch (Exception error)
        {
            GD.PushError($"[TABLE] 解析 {file} 失败: {error.Message}");
            return SlkTable.FromRows(Array.Empty<string>(), Array.Empty<string[]>());
        }
    }

    private static SlkTable Parse(string name, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        var columns = new List<string>();
        if (root.TryGetProperty("columns", out JsonElement columnArray))
        {
            foreach (JsonElement column in columnArray.EnumerateArray())
            {
                columns.Add(column.GetString() ?? "");
            }
        }

        var rows = new List<string[]>();
        if (root.TryGetProperty("rows", out JsonElement rowArray))
        {
            foreach (JsonElement row in rowArray.EnumerateArray())
            {
                var cells = new string[row.GetArrayLength()];
                int index = 0;
                foreach (JsonElement cell in row.EnumerateArray())
                {
                    cells[index++] = cell.GetString() ?? "";
                }
                rows.Add(cells);
            }
        }

        if (columns.Count == 0)
        {
            GD.PushWarning($"[TABLE] {name} 没有列定义");
        }
        return SlkTable.FromRows(columns.ToArray(), rows.ToArray());
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