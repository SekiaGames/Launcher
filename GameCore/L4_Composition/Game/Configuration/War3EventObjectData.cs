using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public enum War3ModelEventKind
{
    Unknown,
    Sound,
    Spawn,
    Splat,
    UberSplat,
}

public readonly struct War3ModelEventData
{
    public War3ModelEventKind Kind { get; init; }
    public string Id { get; init; }
    public string Resource { get; init; }
    public float Scale { get; init; }
    public float LifeSpan { get; init; }
    public float BirthTime { get; init; }
    public float PauseTime { get; init; }
    public float DecayTime { get; init; }
    public Color StartColor { get; init; }
    public Color MiddleColor { get; init; }
    public Color EndColor { get; init; }
    public int BlendMode { get; init; }

    public bool IsGroundSplat => Kind is War3ModelEventKind.Splat or War3ModelEventKind.UberSplat;
}

public static class War3EventObjectData
{
    private static readonly Dictionary<string, War3ModelEventData> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static War3ModelEventData Resolve(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return default;
        }

        string normalized = eventName.Trim();
        if (Cache.TryGetValue(normalized, out War3ModelEventData cached))
        {
            return cached;
        }

        War3ModelEventData definition = ResolveDefinition(normalized);
        Cache[normalized] = definition;
        return definition;
    }

    private static War3ModelEventData ResolveDefinition(string normalized)
    {
        if (normalized.Length < 7)
        {
            return default;
        }

        string prefix = normalized[..3].ToUpperInvariant();
        string id = normalized[^4..].ToUpperInvariant();
        return prefix switch
        {
            "SPL" => ResolveSplat(War3SlkReader.SplatData(), War3ModelEventKind.Splat, id),
            "UBR" => ResolveSplat(War3SlkReader.UberSplatData(), War3ModelEventKind.UberSplat, id),
            _ => default,
        };
    }

    private static War3ModelEventData ResolveSplat(SlkTable table, War3ModelEventKind kind, string id)
    {
        int row = table.FindRow("Name", id);
        if (row < 0)
        {
            return default;
        }

        string directory = table.Get(row, "Dir") ?? "";
        string file = table.Get(row, "file") ?? "";
        string resource = "";
        if (!string.IsNullOrWhiteSpace(file))
        {
            resource = string.IsNullOrWhiteSpace(directory)
                ? file
                : directory.Replace('\\', '/').TrimEnd('/') + "/" + file;
            if (!System.IO.Path.HasExtension(resource))
            {
                resource += ".blp";
            }
        }

        float life = table.GetFloat(row, "Lifespan",
            table.GetFloat(row, "BirthTime", 0f)
            + table.GetFloat(row, "PauseTime", 0f)
            + table.GetFloat(row, "Decay", 0f));

        return new War3ModelEventData
        {
            Kind = kind,
            Id = id,
            Resource = resource,
            Scale = table.GetFloat(row, "Scale", 1f),
            LifeSpan = life,
            BirthTime = Math.Max(0f, table.GetFloat(row, "BirthTime", 0f)),
            PauseTime = Math.Max(0f, table.GetFloat(row, "PauseTime", 0f)),
            DecayTime = Math.Max(0f, table.GetFloat(row, "Decay", 0f)),
            StartColor = ColorOf(table, row, "Start"),
            MiddleColor = ColorOf(table, row, "Middle"),
            EndColor = ColorOf(table, row, "End"),
            BlendMode = table.GetInt(row, "BlendMode", 0),
        };
    }

    private static Color ColorOf(SlkTable table, int row, string phase)
    {
        return new Color(
            Mathf.Clamp(table.GetFloat(row, phase + "R", 255f), 0f, 255f) / 255f,
            Mathf.Clamp(table.GetFloat(row, phase + "G", 255f), 0f, 255f) / 255f,
            Mathf.Clamp(table.GetFloat(row, phase + "B", 255f), 0f, 255f) / 255f,
            Mathf.Clamp(table.GetFloat(row, phase + "A", 255f), 0f, 255f) / 255f);
    }
}
