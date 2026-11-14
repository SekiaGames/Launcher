using Godot;
using System;
using System.Globalization;
using OpenWC3.Game;

namespace OpenWC3;

/// <summary>
/// UI\MiscData.txt 的 *Text 样式：颜色、速度、寿命、淡出点，字号取 UI\MiscUI.txt 的 *TextHeight。
/// Height 是归一化帧缓冲高度（不是 FDF 的 0.6 单位），渲染时再乘帧缓冲高度换算成像素。
/// </summary>
public sealed class War3FloatingTextStyle
{
    public Color Color { get; private set; } = new(1f, 0f, 0f);
    public float Alpha { get; private set; } = 1f;
    public Vector2 Velocity { get; private set; }
    public float Lifetime { get; private set; }
    public float FadeStart { get; private set; }
    public float Height { get; private set; } = .024f;

    internal War3FloatingTextStyle(float velocityY, float lifetime, float fadeStart, Color? color = null,
        float alpha = 1f, float height = .024f)
    {
        Velocity = new Vector2(0f, velocityY);
        Lifetime = lifetime;
        FadeStart = fadeStart;
        Color = color ?? new Color(1f, 0f, 0f);
        Alpha = alpha;
        Height = height;
    }

    /// <summary>
    /// 按客户端的前缀约定读表：Misc.{前缀}Color / Velocity / Lifetime / FadeStart 在 UI\MiscData，
    /// Misc.{前缀}Height 在 UI\MiscUI。缺键保留经典 TFT 默认值。
    /// </summary>
    internal static War3FloatingTextStyle Load(string prefix, float velocityY, float lifetime, float fadeStart,
        Color defaultColor, float defaultAlpha = 1f, float defaultHeight = .024f)
    {
        var style = new War3FloatingTextStyle(velocityY, lifetime, fadeStart, defaultColor,
            defaultAlpha, defaultHeight);
        if (GeneratedTexts.TryGet("UIMiscData", "Misc." + prefix + "Color", out string color))
            style.Read("Color", color);
        if (GeneratedTexts.TryGet("UIMiscData", "Misc." + prefix + "Velocity", out string velocity))
            style.Read("Velocity", velocity);
        if (GeneratedTexts.TryGet("UIMiscData", "Misc." + prefix + "Lifetime", out string life))
            style.Read("Lifetime", life);
        if (GeneratedTexts.TryGet("UIMiscData", "Misc." + prefix + "FadeStart", out string fade))
            style.Read("FadeStart", fade);
        if (GeneratedTexts.TryGet("UIMiscUI", "Misc." + prefix + "Height", out string height))
            style.Read("Height", height);
        return style;
    }

    internal void Read(string property, string value)
    {
        string[] parts = value.Split(',');
        var numbers = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])
                || !float.IsFinite(numbers[i])) return;

        switch (property)
        {
            case "Color" when numbers.Length == 4:
                Alpha = Math.Clamp(numbers[0] / 255f, 0f, 1f);
                Color = new Color(Math.Clamp(numbers[1] / 255f, 0f, 1f),
                    Math.Clamp(numbers[2] / 255f, 0f, 1f), Math.Clamp(numbers[3] / 255f, 0f, 1f));
                break;
            // 飘字按 velocity.x/y * age 换算屏幕偏移；第三个分量渲染侧不用。
            case "Velocity" when numbers.Length >= 2:
                Velocity = new Vector2(numbers[0], numbers[1]);
                break;
            case "Lifetime" when numbers.Length == 1: Lifetime = Math.Max(0f, numbers[0]); break;
            case "FadeStart" when numbers.Length == 1: FadeStart = Math.Max(0f, numbers[0]); break;
            case "Height" when numbers.Length == 1: Height = Math.Max(0f, numbers[0]); break;
        }
    }
}
