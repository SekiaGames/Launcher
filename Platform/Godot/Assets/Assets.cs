using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3;

public static class Assets
{
    public static string ProjectDir { get; private set; }
    public static string WorkspaceDir { get; private set; }
    public static bool ExtractMounted { get; private set; }
    public static string ExtractPck { get; private set; }

    static readonly Dictionary<string, Variant> JsonCache = new();

    public static void Init()
    {
        ProjectDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/');
        WorkspaceDir = ProjectDir + "/..";
        ExtractPck = WorkspaceDir + "/extract.pck";
        ExtractMounted = System.IO.File.Exists(ExtractPck) && ProjectSettings.LoadResourcePack(ExtractPck);
        GD.Print($"[ASSET] 工程 {ProjectDir}");
        GD.Print($"[ASSET] extract.pck {(ExtractMounted ? "已挂载" : "未找到")}: {ExtractPck}");
    }

    public static bool MountPack(string absolutePath)
    {
        bool mounted = !string.IsNullOrEmpty(absolutePath)
            && System.IO.File.Exists(absolutePath)
            && ProjectSettings.LoadResourcePack(absolutePath);
        if (mounted) JsonCache.Clear();
        return mounted;
    }

    public static string Res(string relative)
    {
        return "res://extract/" + relative.Replace("\\", "/").TrimStart('/');
    }

    public static bool Exists(string relative)
    {
        return ResourceLoader.Exists(Res(relative));
    }

    public static string ReadText(string resPath)
    {
        if (!FileAccess.FileExists(resPath)) return null;
        using FileAccess file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    public static byte[] ReadBytes(string resPath)
    {
        if (!FileAccess.FileExists(resPath)) return null;
        return FileAccess.GetFileAsBytes(resPath);
    }

    public static Variant ReadJson(string resPath)
    {
        if (JsonCache.TryGetValue(resPath, out Variant cached)) return cached;
        string text = ReadText(resPath);
        if (string.IsNullOrEmpty(text)) return default;
        Variant parsed = Json.ParseString(text);
        JsonCache[resPath] = parsed;
        return parsed;
    }

    public static PackedScene LoadScene(string resPath)
    {
        return string.IsNullOrEmpty(resPath) ? null : ResourceLoader.Load<PackedScene>(resPath);
    }

    public static Texture2D LoadTexture(string resPath)
    {
        return string.IsNullOrEmpty(resPath) ? null : ResourceLoader.Load<Texture2D>(resPath);
    }

    public static string[] ListGmaps()
    {
        var found = new List<string>();
        DirAccess dir = DirAccess.Open(WorkspaceDir);
        if (dir == null) return found.ToArray();

        foreach (string name in dir.GetFiles())
        {
            if (name.EndsWith(".gmap", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(name);
            }
        }
        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found.ToArray();
    }
}