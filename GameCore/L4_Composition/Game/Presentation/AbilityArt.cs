using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 技能/Buff 的美术字段拆分。字段值本身来自物编合并层（War3ObjectData），
/// 这里只负责「按字段名取值 → 按分隔符拆成多条美术」。
/// </summary>
public static class AbilityArt
{
    private static readonly char[] ArtSeparators = { ',', ';' };

    private static readonly IReadOnlyList<string> Origin =
        Array.AsReadOnly(new[] { "origin" });

    public static IReadOnlyCollection<string> Ids => War3ObjectData.TextAbilityIds;

    public static IReadOnlyList<string> Arts(IRuntimeObjectData data, int level,
        string artField, string rawField)
        => Split(Text(data, level, artField, rawField));

    public static IReadOnlyList<string> Attachments(IRuntimeObjectData data, int level,
        string artField, string rawField)
    {
        int count = Math.Max(1, Count(data, level, artField, rawField));
        List<string> found = null;
        for (int index = 0; index < count; index++)
        {
            string name = index == 0 ? artField : artField + index;
            string rawName = index == 0 ? rawField : rawField + index;
            string value = Text(data, level, name, rawName);
            if (Missing(value)) continue;
            (found ??= new List<string>(count)).Add(value.Trim());
        }
        return found ?? Origin;
    }

    public static string FirstArt(IRuntimeObjectData data, int level,
        string artField, string rawField)
    {
        IReadOnlyList<string> arts = Arts(data, level, artField, rawField);
        return arts.Count > 0 ? arts[0] : "";
    }

    public static string FirstAttachment(IRuntimeObjectData data, int level,
        string artField, string rawField)
    {
        IReadOnlyList<string> attachments = Attachments(data, level, artField, rawField);
        return attachments.Count > 0 ? attachments[0] : "origin";
    }

    private static string Text(IRuntimeObjectData data, int level,
        string artField, string rawField)
    {
        if (data == null) return "";
        int index = Math.Max(1, level);
        string value = data.GetString(artField, index, 0, "");
        if (Missing(value) && !string.IsNullOrEmpty(rawField)) value = data.GetString(rawField, index, 0, "");
        return Missing(value) ? "" : value;
    }

    private static int Count(IRuntimeObjectData data, int level, string artField, string rawField)
    {
        string value = Text(data, level, artField + "count", rawField.Length == 0 ? "" : rawField + "count");
        return int.TryParse(value, out int parsed) ? parsed : 1;
    }

    private static IReadOnlyList<string> Split(string value)
    {
        if (Missing(value)) return Array.Empty<string>();
        List<string> arts = null;
        foreach (string part in value.Split(ArtSeparators))
        {
            string art = part.Trim();
            if (art.Length == 0 || art == "_") continue;
            arts ??= new List<string>(2);
            bool duplicate = false;
            foreach (string existing in arts)
            {
                if (!string.Equals(existing, art, StringComparison.OrdinalIgnoreCase)) continue;
                duplicate = true;
                break;
            }
            if (!duplicate) arts.Add(art);
        }
        return arts ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    private static bool Missing(string value) => string.IsNullOrWhiteSpace(value) || value.Trim() == "_";
}
