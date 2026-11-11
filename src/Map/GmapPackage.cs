using Godot;
using System;

namespace Launcher;

public sealed class GmapPackage
{
    public const string ManifestPath = "res://gmap/manifest.json";

    public string Path { get; private set; }
    public string Name { get; private set; }
    public char Tileset { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public Godot.Collections.Dictionary Manifest { get; private set; }

    public static GmapPackage Mount(string absolutePath)
    {
        if (!Assets.MountPack(absolutePath))
        {
            GD.PushError($"[GMAP] 挂载失败: {absolutePath}");
            return null;
        }

        Variant json = Assets.ReadJson(ManifestPath);
        if (json.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError("[GMAP] 包里没有 gmap/manifest.json");
            return null;
        }

        Godot.Collections.Dictionary manifest = json.AsGodotDictionary();
        var package = new GmapPackage
        {
            Path = absolutePath,
            Manifest = manifest,
            Name = manifest.TryGetValue("name", out Variant name) ? name.AsString() : "unknown",
            Width = manifest.TryGetValue("width", out Variant w) ? w.AsInt32() : 0,
            Height = manifest.TryGetValue("height", out Variant h) ? h.AsInt32() : 0,
            OffsetX = manifest.TryGetValue("offset_x", out Variant ox) ? ox.AsSingle() : 0f,
            OffsetY = manifest.TryGetValue("offset_y", out Variant oy) ? oy.AsSingle() : 0f,
        };

        string tileset = manifest.TryGetValue("tileset", out Variant t) ? t.AsString() : "";
        package.Tileset = string.IsNullOrEmpty(tileset) ? 'L' : tileset[0];

        GD.Print($"[GMAP] {package.Name} {package.Width}x{package.Height} 地形集 {package.Tileset} 文件 {package.Path}");
        return package;
    }

    public byte[] ReadMapFile(string name)
    {
        return Assets.ReadBytes("res://gmap/map/" + name);
    }

    public bool HasMapFile(string name)
    {
        return FileAccess.FileExists("res://gmap/map/" + name);
    }
}