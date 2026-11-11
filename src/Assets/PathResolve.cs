using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public static class PathResolve
{
    private static readonly Dictionary<string, Dictionary<string, string>> DirCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> SceneCache = new(StringComparer.OrdinalIgnoreCase);

    public static string ModelScene(string war3Path)
    {
        if (string.IsNullOrWhiteSpace(war3Path)) return null;
        if (SceneCache.TryGetValue(war3Path, out string cached)) return cached;

        string result = ResourceFile(war3Path, ".tscn") ?? ResourceFile(war3Path, ".glb");
        SceneCache[war3Path] = result;
        return result;
    }

    public static string IconPng(string war3Path)
    {
        if (string.IsNullOrWhiteSpace(war3Path)) return null;

        string trimmed = war3Path.Replace('\\', '/').Trim('/');
        int dot = trimmed.LastIndexOf('.');
        int slash = trimmed.LastIndexOf('/');
        if (dot > slash) trimmed = trimmed[..dot];
        return ResourceFile(trimmed, ".png");
    }

    public static string ResourceFile(string relative, string extension)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;

        string[] segments = relative.Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        string directory = "res://extract";
        for (int i = 0; i < segments.Length - 1; i++)
        {
            string match = MatchDir(directory, segments[i]);
            if (match == null) return null;
            directory += "/" + match;
        }

        string file = segments[^1];
        string actual = MatchFile(directory, file + extension);
        return actual == null ? null : directory + "/" + actual;
    }

    private static Dictionary<string, string> Listing(string directory)
    {
        if (DirCache.TryGetValue(directory, out Dictionary<string, string> cached)) return cached;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DirAccess dir = DirAccess.Open(directory);
        if (dir != null)
        {
            foreach (string name in dir.GetDirectories())
            {
                map["dir:" + name] = name;
            }
            foreach (string name in dir.GetFiles())
            {
                string logical = LogicalName(name);
                map["file:" + logical] = logical;
            }
        }
        DirCache[directory] = map;
        return map;
    }

    private static string LogicalName(string name)
    {
        if (name.EndsWith(".remap", StringComparison.OrdinalIgnoreCase)) return name[..^6];
        if (name.EndsWith(".import", StringComparison.OrdinalIgnoreCase)) return name[..^7];
        return name;
    }

    private static string MatchDir(string directory, string segment)
    {
        return Listing(directory).TryGetValue("dir:" + segment, out string actual) ? actual : null;
    }

    private static string MatchFile(string directory, string name)
    {
        return Listing(directory).TryGetValue("file:" + name, out string actual) ? actual : null;
    }
}