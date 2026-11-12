using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

/// <summary>
/// 引擎闪电束参数（`Units\LightningData.slk` → `SplatsLightningData`）：
/// 贴图、宽度、分段长度、噪点、颜色。
/// 1.27 的 AbilityData 没有闪电码列，技能到闪电码的对应关系按客户端各技能类的默认值补。
/// </summary>
public sealed class LightningInfo
{
    public string Id;
    public string TexturePath;
    public float SegmentLength = 100f;
    public float Width = 40f;
    public Color Tint = Colors.White;
    public float NoiseScale = 0.05f;
    public float TextureScale = 0.5f;
    public float Linger = 2f;
}

public static class LightningTable
{
    private const string TableName = "SplatsLightningData";

    private static readonly Dictionary<string, LightningInfo> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> AbilityBolt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AOcl"] = "CLPB", ["ACcl"] = "CLPB", ["AIcl"] = "CLPB",
        ["ANfl"] = "FORK", ["ACfl"] = "FORK",
        ["AEmb"] = "MBUR",
        ["AOhw"] = "HWPB",
        ["Alit"] = "CHIM",
        ["ANfd"] = "AFOD",
    };

    private static bool _indexed;
    private static readonly Dictionary<string, int> Rows = new(StringComparer.OrdinalIgnoreCase);

    public static LightningInfo Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out LightningInfo cached)) return cached;

        EnsureIndex();
        if (!Rows.TryGetValue(id, out int row)) return null;

        SlkTable table = War3SlkReader.Get(TableName);
        string file = table.Get(row, "file");
        string dir = table.Get(row, "Dir");
        string texture = PathResolve.IconPng((dir ?? "").Trim('\\', '/') + "/" + file);

        var info = new LightningInfo
        {
            Id = id,
            TexturePath = texture,
            SegmentLength = Number(table.Get(row, "AvgSegLen"), 100f),
            Width = Number(table.Get(row, "Width"), 40f),
            Tint = new Color(
                Number(table.Get(row, "R"), 255f) / 255f,
                Number(table.Get(row, "G"), 255f) / 255f,
                Number(table.Get(row, "B"), 255f) / 255f,
                Number(table.Get(row, "A"), 255f) / 255f),
            NoiseScale = Number(table.Get(row, "NoiseScale"), 0.05f),
            TextureScale = Number(table.Get(row, "TexCoordScale"), 0.5f),
            Linger = Number(table.Get(row, "Duration"), 2f),
        };
        Cache[id] = info;
        return info;
    }

    /// <summary>技能对应的闪电码：先按别名，再按基类码。</summary>
    public static string BoltFor(Ability ability)
    {
        if (ability?.Type == null) return null;
        if (AbilityBolt.TryGetValue(ability.Type.Id, out string bolt)) return bolt;
        string code = ability.Type.Code;
        if (!string.IsNullOrEmpty(code) && AbilityBolt.TryGetValue(code, out bolt)) return bolt;
        return null;
    }

    private static void EnsureIndex()
    {
        if (_indexed) return;
        _indexed = true;

        SlkTable table = War3SlkReader.Get(TableName);
        foreach (int row in table.DataRows)
        {
            string name = (table.Get(row, "Name") ?? "").Trim();
            if (name.Length == 0) continue;
            Rows[name] = row;
        }
        GD.Print($"[LIGHTNING] 闪电束 {Rows.Count} 种");
    }

    private static float Number(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "-") return fallback;
        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }
}
