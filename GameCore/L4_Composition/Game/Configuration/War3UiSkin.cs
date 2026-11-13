using System;
using System.Collections.Generic;
using Godot;
using Vector4 = System.Numerics.Vector4;

namespace OpenWC3.Game;

public sealed class War3UiSkin
{
    private const int Edition = 1;

    private readonly Dictionary<string, string> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _editionLookups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _rocLookups = new(StringComparer.OrdinalIgnoreCase);
    private Vector4[] _teamColors = Array.Empty<Vector4>();

    public Vector4? TeamColor(int index) => _teamColors.Length == 0
        ? null
        : _teamColors[Math.Abs(index % _teamColors.Length)];

    public string Get(string key, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        return Lookup(key, fallback, _editionLookups, "_V" + Edition);
    }

    public string GetRoc(string key, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        return Lookup(key, fallback, _rocLookups, "_V0");
    }

    private string Lookup(string key, string fallback, Dictionary<string, string> cache, string suffix)
    {
        if (cache.TryGetValue(key, out string cached)) return cached ?? fallback;
        string value = null;
        foreach (string candidate in new[] { key, key + suffix })
        {
            if (_fields.TryGetValue(candidate, out string field) && !string.IsNullOrWhiteSpace(field))
            {
                value = field;
                break;
            }
        }
        if (cache.Count >= 512) cache.Clear();
        cache[key] = value;
        return value ?? fallback;
    }

    public static War3UiSkin Load(string race = "Human", string mapSkinText = null)
    {
        var result = new War3UiSkin();
        Dictionary<string, Dictionary<string, string>> sections = ReadGeneratedSections();
        Merge(result._fields, Section(sections, "Default"));
        Merge(result._fields, Section(sections, race));
        Merge(result._fields, Section(sections, "CustomSkin"));

        Dictionary<string, Dictionary<string, string>> map = Parse(mapSkinText);
        Merge(result._fields, Section(map, "CustomSkin"));
        Merge(result._fields, Section(map, race));

        int count = int.TryParse(result.Get("TeamColors", "16"), out int configured)
            ? Math.Clamp(configured, 1, 32)
            : 16;
        var colors = new Vector4[count];
        string prefix = result.Get("TeamColor", @"ReplaceableTextures\TeamColor\TeamColor");
        for (int i = 0; i < count; i++)
        {
            Color? pixel = ReadTeamColor(prefix + $"{i:00}.png");
            if (pixel == null) return result;
            Color color = pixel.Value;
            colors[i] = new Vector4(color.R, color.G, color.B, color.A);
        }
        result._teamColors = colors;
        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> ReadGeneratedSections()
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Godot.Collections.Dictionary keys = GeneratedTexts.Get("UIwar3skins")["keys"].AsGodotDictionary();
        foreach (KeyValuePair<Variant, Variant> pair in keys)
        {
            string name = pair.Key.AsString();
            int dot = name.IndexOf('.');
            if (dot <= 0) continue;
            string section = name[..dot];
            string field = name[(dot + 1)..];
            if (!sections.TryGetValue(section, out Dictionary<string, string> fields))
            {
                fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[section] = fields;
            }
            fields[field] = pair.Value.AsString().Trim('"');
        }
        return sections;
    }

    private static Color? ReadTeamColor(string path)
    {
        byte[] bytes = Assets.ReadBytes(Assets.Res(path.Replace('\\', '/')));
        if (bytes == null || bytes.Length == 0) return null;
        var image = new Image();
        if (image.LoadPngFromBuffer(bytes) != Error.Ok) return null;
        return image.GetPixel(0, 0);
    }

    private static Dictionary<string, Dictionary<string, string>> Parse(string text)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> current = null;
        foreach (string raw in (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                string name = line[1..^1].Trim();
                if (!sections.TryGetValue(name, out current))
                {
                    sections[name] = current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }
            int equals = line.IndexOf('=');
            if (current == null || equals <= 0) continue;
            string value = line[(equals + 1)..].Trim().Trim('"');
            current[line[..equals].Trim()] = value;
        }
        return sections;
    }

    private static Dictionary<string, string> Section(
        Dictionary<string, Dictionary<string, string>> sections, string name) =>
        sections.TryGetValue(name, out Dictionary<string, string> fields) ? fields : null;

    private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        if (source == null) return;
        foreach (KeyValuePair<string, string> field in source) target[field.Key] = field.Value;
    }
}
