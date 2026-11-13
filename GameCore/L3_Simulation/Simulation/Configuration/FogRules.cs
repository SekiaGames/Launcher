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

        if (!ReadMiscData("FoggedTerrain", out string fogged)) return;
        Source = "生成表 UIMiscData";
        if (TryParseArgb("FoggedTerrain=" + fogged, out Color fog)) FoggedTerrain = fog;
        if (ReadMiscData("BlackMaskedTerrain", out string black)
            && TryParseArgb("BlackMaskedTerrain=" + black, out Color dark)) BlackMaskedTerrain = dark;
        if (ReadMiscData("DarkMaskedTerrain", out string masked)
            && TryParseArgb("DarkMaskedTerrain=" + masked, out Color dim)) DarkMaskedTerrain = dim;

        GD.Print($"[FOG] 配置来源 {Source}：黑区={BlackMaskedTerrain} 雾区={FoggedTerrain}");
    }

    private static bool ReadMiscData(string key, out string value) =>
        GeneratedTexts.TryGet("UIMiscData", key, out value);

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
