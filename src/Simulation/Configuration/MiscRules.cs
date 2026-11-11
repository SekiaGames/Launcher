using Godot;
using System;
using System.Globalization;

namespace Launcher;

public static class MiscRules
{
    private const string Table = "res://extract/Texts/MeleeV0UnitsMiscGame.json";

    public static readonly float GuardDistance = Value("Misc.GuardDistance", 600f);
    public static readonly float MaxGuardDistance = Value("Misc.MaxGuardDistance", 1000f);
    public static readonly float GuardReturnTime = Value("Misc.GuardReturnTime", 5f);
    public static readonly float CreepCallForHelp = Value("Misc.CreepCallForHelp", 600f);

    public static string Text(string key)
    {
        Variant json = Assets.ReadJson(Table);
        if (json.VariantType != Variant.Type.Dictionary) return null;

        Godot.Collections.Dictionary root = json.AsGodotDictionary();
        if (!root.ContainsKey("keys")) return null;

        Godot.Collections.Dictionary keys = root["keys"].AsGodotDictionary();
        if (!keys.ContainsKey(key)) return null;

        return keys[key].AsString();
    }

    public static float Value(string key, float fallback)
    {
        string text = Text(key);
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    public static float[] Values(string key, int expectedLength)
    {
        string text = Text(key);
        var result = new float[expectedLength];
        Array.Fill(result, 1f);
        if (string.IsNullOrWhiteSpace(text)) return result;

        string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
        for (int index = 0; index < expectedLength && index < parts.Length; index++)
        {
            if (float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                result[index] = value;
            }
        }
        return result;
    }

    public static void Log()
    {
        GD.Print($"[MISC] 守卫 警戒={GuardDistance} 最大追击={MaxGuardDistance} 回营等待={GuardReturnTime}s 呼叫同伴={CreepCallForHelp}");
    }
}
