using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace OpenWC3.Game;

internal sealed class War3MiscData
{
    public float VisibleAlpha { get; private set; }
    public float FogMaskAlpha { get; private set; }
    public float DarkMaskAlpha { get; private set; }
    public float BlackMaskAlpha { get; private set; }

    public Vector4 ColorPlayer { get; private set; }
    public Vector4 ColorAlly { get; private set; }
    public Vector4 ColorEnemy { get; private set; }
    public Vector4 ColorResource { get; private set; }
    public Vector4 ColorItem { get; private set; }
    public Vector4 ColorHero { get; private set; }
    public Vector4 ColorDestructable { get; private set; }
    public Vector4 ColorCreepAllied { get; private set; }
    public Vector4 ColorCreepNormal { get; private set; }
    public Vector4 BlightMask { get; private set; }

    public float MiddleCampThreshold { get; private set; }
    public float ToughCampThreshold { get; private set; }
    public float MiddleCampScale { get; private set; }
    public float CampPulseScale { get; private set; }
    public Vector4 WeakCampColor { get; private set; }
    public Vector4 MiddleCampColor { get; private set; }
    public Vector4 ToughCampColor { get; private set; }

    public War3MiscData()
    {
        VisibleAlpha = 1f;
        FogMaskAlpha = 192f / 255f;
        DarkMaskAlpha = 96f / 255f;
        BlackMaskAlpha = 0f;
        ColorPlayer = new Vector4(1f, 1f, 1f, 1f);
        ColorAlly = new Vector4(0f, 1f, 210f / 255f, 1f);
        ColorEnemy = new Vector4(1f, 0f, 0f, 1f);
        ColorResource = new Vector4(32f / 255f, 128f / 255f, 128f / 255f, 1f);
        ColorItem = new Vector4(240f / 255f, 240f / 255f, 0f, 1f);
        ColorHero = new Vector4(1f, 1f, 128f / 255f, 1f);
        ColorDestructable = new Vector4(0f, 160f / 255f, 128f / 255f, 1f);
        ColorCreepAllied = new Vector4(0f, 0f, 50f / 255f, 1f);
        ColorCreepNormal = ColorCreepAllied;
        BlightMask = new Vector4(63f / 255f, 63f / 255f, 64f / 255f, 0f);
        MiddleCampThreshold = 10f;
        ToughCampThreshold = 20f;
        MiddleCampScale = 1.3f;
        CampPulseScale = 0.5f;
        WeakCampColor = new Vector4(0f, 200f / 255f, 0f, 1f);
        MiddleCampColor = new Vector4(1f, 128f / 255f, 0f, 1f);
        ToughCampColor = new Vector4(220f / 255f, 0f, 0f, 1f);

        VisibleAlpha = ReadFloat("FogColorVisibleAlpha", VisibleAlpha) / 255f;
        FogMaskAlpha = ReadFloat("FogColorFogMaskAlpha", FogMaskAlpha * 255f) / 255f;
        DarkMaskAlpha = ReadFloat("FogColorDarkMaskAlpha", DarkMaskAlpha * 255f) / 255f;
        BlackMaskAlpha = ReadFloat("FogColorBlackMaskAlpha", BlackMaskAlpha) / 255f;
        ColorPlayer = ReadRgba("FogColorPlayer", ColorPlayer);
        ColorAlly = ReadRgba("FogColorAlly", ColorAlly);
        ColorEnemy = ReadRgba("FogColorEnemy", ColorEnemy);
        ColorResource = ReadRgba("FogColorResource", ColorResource);
        ColorItem = ReadRgba("FogColorItem", ColorItem);
        ColorHero = ReadRgba("FogColorHero", ColorHero);
        ColorDestructable = ReadRgba("FogColorDestructable", ColorDestructable);
        ColorCreepAllied = ReadRgba("FogColorCreepAllied", ColorCreepAllied);
        ColorCreepNormal = ReadRgba("FogColorCreepNormal", ColorCreepNormal);
        BlightMask = ReadRgba("FogColorBlightMask", BlightMask);
        MiddleCampThreshold = ReadFloat("MinimapMiddleCampThreshold", MiddleCampThreshold);
        ToughCampThreshold = ReadFloat("MinimapToughCampThreshold", ToughCampThreshold);
        MiddleCampScale = ReadFloat("MinimapMiddleCampScale", MiddleCampScale);
        CampPulseScale = ReadFloat("MinimapCampPulseScale", CampPulseScale);
        WeakCampColor = ReadRgba("MinimapWeakCampColor", WeakCampColor);
        MiddleCampColor = ReadRgba("MinimapMiddleCampColor", MiddleCampColor);
        ToughCampColor = ReadRgba("MinimapToughCampColor", ToughCampColor);
    }

    public void ResolveCamp(float strength, out Vector4 color, out float scale)
    {
        bool tough = strength >= ToughCampThreshold;
        bool middle = !tough && strength >= MiddleCampThreshold;
        color = tough ? ToughCampColor : middle ? MiddleCampColor : WeakCampColor;
        scale = middle ? MiddleCampScale : 1f;
    }

    static float ReadFloat(string key, float fallback)
    {
        if (!GeneratedTexts.TryGet("UIMiscData", key, out string value)) return fallback;
        List<float> parts = ParseNumbers(value);
        return parts.Count > 0 ? parts[0] : fallback;
    }

    static Vector4 ReadRgba(string key, Vector4 fallback)
    {
        if (!GeneratedTexts.TryGet("UIMiscData", key, out string value)) return fallback;
        List<float> parts = ParseNumbers(value);
        if (parts.Count < 3) return fallback;
        return parts.Count >= 4
            ? new Vector4(parts[1] / 255f, parts[2] / 255f, parts[3] / 255f, parts[0] / 255f)
            : new Vector4(parts[0] / 255f, parts[1] / 255f, parts[2] / 255f, 1f);
    }

    static List<float> ParseNumbers(string value)
    {
        var result = new List<float>(4);
        foreach (string part in (value ?? "").Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0
                && float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                result.Add(number);
        }
        return result;
    }
}