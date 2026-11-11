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

    private static float Value(string key, float fallback)
    {
        Variant json = Assets.ReadJson(Table);
        if (json.VariantType != Variant.Type.Dictionary) return fallback;

        Godot.Collections.Dictionary root = json.AsGodotDictionary();
        if (!root.ContainsKey("keys")) return fallback;

        Godot.Collections.Dictionary keys = root["keys"].AsGodotDictionary();
        if (!keys.ContainsKey(key)) return fallback;

        string text = keys[key].AsString();
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    public static void Log()
    {
        GD.Print($"[MISC] 守卫 警戒={GuardDistance} 最大追击={MaxGuardDistance} 回营等待={GuardReturnTime}s 呼叫同伴={CreepCallForHelp}");
    }
}
