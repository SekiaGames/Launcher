using Godot;
using System;

namespace OpenWC3.Game.UI;

public static class FdfSource
{
    public static string Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string relative = Normalize(path);

        if (Generated.FdfTables.All.TryGetValue(relative, out string text)) return text;

        foreach (var entry in Generated.FdfTables.All)
        {
            if (entry.Key.EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        GD.PushError($"[FDF] 生成的界面表里找不到 {path}");
        return null;
    }

    static string Normalize(string path)
    {
        string relative = path.Replace('\\', '/').TrimStart('/');
        if (relative.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[6..];
        }
        int markers = relative.IndexOf("UI/", StringComparison.OrdinalIgnoreCase);
        return markers > 0 ? relative[markers..] : relative;
    }
}
