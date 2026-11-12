using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class TerrainTextureLoader
{

    public const string BlightId = "@blight";

    private static string _blightPath = "TerrainArt/Blight/Ashen_Blight.blp";

    public static void SetBlightTileset(char tileset)
    {
        string fromTable = BlightPathFromTable(tileset);
        if (!string.IsNullOrEmpty(fromTable)) _blightPath = fromTable;
    }

    /// <summary>腐地贴图：`UI\WorldEditData` 的 [TileSets] 每行第二段就是该地形集的 blight 贴图。</summary>
    private static string BlightPathFromTable(char tileset)
    {
        Variant json = Assets.ReadJson("res://extract/Texts/UIWorldEditData.json");
        if (json.VariantType != Variant.Type.Dictionary) return null;

        Godot.Collections.Dictionary root = json.AsGodotDictionary();
        if (!root.ContainsKey("keys")) return null;

        Godot.Collections.Dictionary keys = root["keys"].AsGodotDictionary();
        string key = "TileSets." + char.ToUpperInvariant(tileset);
        if (!keys.ContainsKey(key)) return null;

        string[] parts = keys[key].AsString().Split(',');
        if (parts.Length < 2) return null;

        string path = parts[1].Trim();
        if (path.Length == 0) return null;
        if (!path.EndsWith(".blp", StringComparison.OrdinalIgnoreCase)) path += ".blp";
        return path;
    }

    /// <summary>当前地形集的腐地贴图（war3 路径）。</summary>
    public static string BlightPath => _blightPath;

    private static SlkTable _slk;
    private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

    private static SlkTable Table => _slk ??= War3SlkReader.Terrain();

    public static string PathFor(string tileId)
    {
        int row = Table.FindRow("tileID", tileId);
        if (row < 0)
        {
            return null;
        }
        string dir = Table.Get(row, "dir");
        string file = Table.Get(row, "file");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
        {
            return null;
        }
        return dir.Replace('\\', '/') + "/" + file + ".blp";
    }

    public static string NameFor(string tileId)
    {
        int row = Table.FindRow("tileID", tileId);
        return row < 0 ? tileId : Table.Get(row, "comment") ?? tileId;
    }

    public Image Get(string tileId)
    {
        if (_images.TryGetValue(tileId, out Image cached))
        {
            return cached;
        }

        string relative = tileId == BlightId ? _blightPath : PathFor(tileId);
        if (relative == null || !War3Assets.Exists(relative))
        {
            GD.PushWarning($"[TEX] 找不到瓦片贴图: {tileId} -> {relative}");
            return null;
        }

        Image image = War3Assets.LoadImage(relative);
        _images[tileId] = image;
        _paths[tileId] = relative;
        return image;
    }

    public string PathOf(string tileId)
    {
        Get(tileId);
        return _paths.GetValueOrDefault(tileId);
    }
}
