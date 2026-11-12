using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenWC3.Game;

/// <summary>
/// 物编文本处理：`|cffRRGGBB` 色码、`|n` 换行，以及 `&lt;AEmb,DataA1&gt;` 这类
/// 「技能码 + 字段」占位符（原版 Ubertip/Tip 里到处是它，值来自对应技能的物编数据）。
/// </summary>
public static class War3Text
{
    public static string Plain(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var builder = new StringBuilder(raw.Length);
        for (int index = 0; index < raw.Length; index++)
        {
            char current = raw[index];
            if (current != '|' || index + 1 >= raw.Length)
            {
                builder.Append(current);
                continue;
            }

            char next = raw[index + 1];
            if (next is 'n' or 'N')
            {
                builder.Append('\n');
                index++;
                continue;
            }
            if (next is 'r' or 'R')
            {
                index++;
                continue;
            }
            if (next is 'c' or 'C' && index + 9 < raw.Length)
            {
                index += 9;
                continue;
            }
            builder.Append(current);
        }
        return builder.ToString();
    }

    /// <summary>把 `&lt;AEmb,DataA1&gt;` 换成物编数值；查不到的占位符原样保留，方便发现缺表。</summary>
    public static string Resolve(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf('<') < 0) return raw;

        var builder = new StringBuilder(raw.Length);
        for (int index = 0; index < raw.Length; index++)
        {
            if (raw[index] != '<')
            {
                builder.Append(raw[index]);
                continue;
            }

            int close = raw.IndexOf('>', index + 1);
            if (close < 0)
            {
                builder.Append(raw[index]);
                continue;
            }

            string token = raw[(index + 1)..close];
            string value = Lookup(token);
            if (value == null)
            {
                // 1.27 里确实有挂空的引用（全知药水的 <Alrv,Dur1>），留空并记一次，别把尖括号怼到界面上
                if (Warned.Add(token)) GD.Print($"[TEXT] 占位符 <{token}> 在物编里找不到对应技能，按空处理");
            }
            builder.Append(value ?? "");
            index = close;
        }
        return builder.ToString();
    }

    private static readonly HashSet<string> Warned = new(StringComparer.OrdinalIgnoreCase);

    public static string Describe(string raw) => Plain(Resolve(raw)).Trim();

    private static string Lookup(string token)
    {
        string[] parts = token.Split(',');
        if (parts.Length != 2) return null;

        AbilityType type = AbilityCatalog.Get(parts[0].Trim());
        if (type == null) return null;

        string field = parts[1].Trim();
        int level = 1;
        if (field.Length > 1 && char.IsDigit(field[^1]))
        {
            level = field[^1] - '0';
            field = field[..^1];
        }
        if (level < 1) level = 1;

        float value;
        string name = field.ToLowerInvariant();
        if (name.Length == 5 && name.StartsWith("data") && name[4] >= 'a' && name[4] <= 'i')
        {
            value = type.DataAt(level, char.ToUpperInvariant(name[4]));
        }
        else
        {
            switch (name)
            {
                case "dur": value = type.DurationAt(level); break;
                case "herodur": value = type.HeroDurationAt(level); break;
                case "cost": value = type.CostAt(level); break;
                case "cool": value = type.CooldownAt(level); break;
                case "area": value = type.AreaAt(level); break;
                case "rng": value = type.RangeAt(level); break;
                case "cast": value = type.CastAt(level); break;
                default: return null;
            }
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
