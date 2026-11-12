using System;
using System.Collections.Generic;
using System.Text;

namespace OpenWC3;

/// <summary>
/// war3map.wts 的 TRIGSTR 解析。客户端的玩家名、地图名、提示文本都走这张表，
/// 直接显示 TRIGSTR_010 是不对的。
/// </summary>
public static class War3Strings
{
    private const string Prefix = "TRIGSTR_";

    private static readonly Dictionary<int, string> Values = new();
    private static bool _loaded;

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        byte[] raw = Assets.ReadBytes("res://gmap/map/war3map.wts");
        if (raw == null || raw.Length == 0) return;

        string text = Encoding.UTF8.GetString(raw);
        if (text.Contains('\uFFFD')) text = Encoding.Latin1.GetString(raw);
        text = text.TrimStart('\uFEFF');   // wts 常带 UTF-8 BOM，会影响首条 STRING

        Parse(text);
    }

    public static string Resolve(string value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        if (!value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return value;
        if (!int.TryParse(value.AsSpan(Prefix.Length), out int id)) return value;

        Load();
        return Values.TryGetValue(id, out string resolved) ? resolved : value;
    }

    private static void Parse(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int index = 0;
        while (index < lines.Length)
        {
            string line = lines[index].Trim();
            if (!line.StartsWith("STRING", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            string number = line[6..].Trim();
            int comment = number.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) number = number[..comment].Trim();
            if (!int.TryParse(number, out int id))
            {
                index++;
                continue;
            }

            index++;
            while (index < lines.Length && lines[index].Trim() != "{") index++;
            index++;

            var builder = new StringBuilder();
            while (index < lines.Length && lines[index].Trim() != "}")
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(lines[index]);
                index++;
            }
            index++;

            Values[id] = builder.ToString().Trim();
        }
    }
}
