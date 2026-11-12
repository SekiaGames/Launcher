using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenWC3.Game;

/// <summary>
/// [FogOfWar] 配置，来源 UI\MiscData.txt。四元组按原版是 ARGB（首分量 alpha）。
/// 优先级：extract.pck 内的 UI/MiscData.txt → 解包目录 → 内置默认（与原版 ini 一致）。
/// </summary>
public static class FogRules
{
    public static Color BlackMaskedTerrain { get; private set; } = new(0f, 0f, 0f, 1f);
    public static Color FoggedTerrain { get; private set; } = new(16f / 255f, 16f / 255f, 32f / 255f, 170f / 255f);
    public static Color DarkMaskedTerrain { get; private set; } = new(0f, 0f, 0f, 230f / 255f);
    public static string Source { get; private set; } = "内置默认";

    private static bool _loaded;

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        string text = ReadMiscData(out string source);
        if (string.IsNullOrEmpty(text)) return;

        Source = source;
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("FoggedTerrain", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseArgb(trimmed, out Color color)) FoggedTerrain = color;
            }
            else if (trimmed.StartsWith("BlackMaskedTerrain", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseArgb(trimmed, out Color color)) BlackMaskedTerrain = color;
            }
            else if (trimmed.StartsWith("DarkMaskedTerrain", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseArgb(trimmed, out Color color)) DarkMaskedTerrain = color;
            }
        }

        GD.Print($"[FOG] 配置来源 {Source}：黑区={BlackMaskedTerrain} 雾区={FoggedTerrain}");
    }

    private static string ReadMiscData(out string source)
    {
        const string packed = "res://extract/UI/MiscData.txt";
        if (FileAccess.FileExists(packed))
        {
            source = "extract.pck:UI/MiscData.txt";
            return FileAccess.GetFileAsString(packed);
        }

        string unpacked = Assets.WorkspaceDir + "/test/war3-unpack/UI/MiscData.txt";
        if (System.IO.File.Exists(unpacked))
        {
            source = "解包目录:UI/MiscData.txt";
            return System.IO.File.ReadAllText(unpacked);
        }

        source = "内置默认";
        return null;
    }

    private static bool TryParseArgb(string line, out Color color)
    {
        color = default;
        int space = line.IndexOf(' ');
        if (space < 0) return false;

        string[] parts = line[(space + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return false;

        var values = new float[4];
        for (int i = 0; i < 4; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
        }

        // ARGB（首分量 alpha）
        color = new Color(values[1] / 255f, values[2] / 255f, values[3] / 255f, values[0] / 255f);
        return true;
    }
}
