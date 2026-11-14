using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenWC3.Game;

internal sealed class AnimationTagCache
{
    private sealed class PerUnit
    {
        internal int RequestVersion = -1;
        internal string[] Properties = Array.Empty<string>();
        internal readonly List<Entry> Entries = new();
    }

    private sealed record Entry(string[] Secondary, string Harvest, bool Cutting,
        IReadOnlyCollection<string> Tags, string Key);

    private readonly ConditionalWeakTable<Unit, PerUnit> _units = new();
    private readonly PerUnit _noUnit = new();

    internal (IReadOnlyCollection<string> Tags, string Key) Get(Unit unit,
        IReadOnlyCollection<string> secondary, string harvest, bool cutting)
    {
        secondary ??= Array.Empty<string>();
        PerUnit cache = unit == null ? _noUnit : _units.GetValue(unit, static _ => new PerUnit());
        IReadOnlyCollection<string> properties = unit?.AnimationProperties;
        int version = unit?.AnimationRequestVersion ?? 0;
        if (cache.RequestVersion != version || !Matches(cache.Properties, properties))
        {
            cache.RequestVersion = version;
            cache.Properties = properties?.Count > 0
                ? new List<string>(properties).ToArray() : Array.Empty<string>();
            cache.Entries.Clear();
        }

        foreach (Entry entry in cache.Entries)
            if (entry.Harvest == harvest && entry.Cutting == cutting
                && Matches(entry.Secondary, secondary))
                return (entry.Tags, entry.Key);

        string[] secondaryCopy = secondary.Count > 0
            ? new List<string>(secondary).ToArray() : Array.Empty<string>();
        IReadOnlyCollection<string> tags = secondaryCopy;
        if (cache.Properties.Length > 0 || !string.IsNullOrEmpty(harvest) || cutting)
        {
            var merged = new HashSet<string>(secondaryCopy, StringComparer.OrdinalIgnoreCase);
            foreach (string property in cache.Properties)
                foreach (string tag in UnitFactory.SplitAnimationProperties(property)) merged.Add(tag);
            if (!string.IsNullOrEmpty(harvest)) merged.Add(harvest);
            if (cutting) merged.Add("Work");
            tags = merged.ToArray();
        }

        string key = tags.Count == 0 ? "" : string.Join(',', tags).ToUpperInvariant();
        if (cache.Entries.Count >= 16) cache.Entries.Clear();
        cache.Entries.Add(new Entry(secondaryCopy, harvest, cutting, tags, key));
        return (tags, key);
    }

    private static bool Matches(string[] snapshot, IReadOnlyCollection<string> current)
    {
        if (snapshot.Length != (current?.Count ?? 0)) return false;
        if (snapshot.Length == 0) return true;
        int index = 0;
        foreach (string value in current)
            if (snapshot[index++] != value) return false;
        return true;
    }
}