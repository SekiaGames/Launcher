using System;
using System.Collections.Generic;
using Godot;
using OpenWC3.UI;

namespace OpenWC3.Game;

internal sealed class GodotWar3FontFace
{
    private static readonly string[] FallbackPaths =
    {
        @"Fonts\FRIZQT__.TTF",
        @"Fonts\BLQ55Web.ttf",
    };

    private static readonly string[] CjkFamilies =
    {
        "Microsoft YaHei",
        "Noto Sans CJK SC",
        "SimSun",
        "PingFang SC",
    };

    private readonly Font _primary;
    private readonly Font _cjk;

    public string ResourcePath { get; }
    public Font Font => _primary;

    private GodotWar3FontFace(Font primary, Font cjk, string resourcePath)
    {
        _primary = primary;
        _cjk = cjk;
        ResourcePath = resourcePath;
        if (primary is FontFile file && cjk != null)
        {
            var fallbacks = new Godot.Collections.Array<Font>();
            foreach (Font fallback in file.Fallbacks) fallbacks.Add(fallback);
            fallbacks.Add(cjk);
            file.Fallbacks = fallbacks;
        }
    }

    public static GodotWar3FontFace Load(War3UiSkin skin, string skinKey = "MasterFont")
    {
        string configured = skin?.Get(skinKey, skin?.Get("MasterFont", @"Fonts\FRIZQT__.TTF") ?? "")
            ?? "";
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.AddRange(FallbackPaths);

        foreach (string path in candidates)
        {
            FontFile file = LoadFromPack(path);
            if (file == null)
            {
                RuntimeDiagnostics.WriteLine($"[ui-font] 跳过 {path}：{Assets.Res(path.Replace('\\', '/'))} 不存在");
                continue;
            }
            RuntimeDiagnostics.WriteLine($"[ui-font] {skinKey} -> {path}");
            return new GodotWar3FontFace(file, LoadCjkFont(), path);
        }

        RuntimeDiagnostics.WriteLine($"[ui-font] {skinKey} 缺失，回退默认字体");
        return new GodotWar3FontFace(ThemeDB.FallbackFont, LoadCjkFont(), "");
    }

    private static FontFile LoadFromPack(string path)
    {
        string resourcePath = Assets.Res(path.Replace('\\', '/'));
        if (ResourceLoader.Exists(resourcePath))
        {
            var imported = ResourceLoader.Load<FontFile>(resourcePath);
            if (imported != null) return imported;
        }
        byte[] bytes = Assets.ReadBytes(resourcePath);
        if (bytes == null || bytes.Length == 0) return null;
        var file = new FontFile();
        file.Data = bytes;
        return file;
    }

    private static Font LoadCjkFont()
    {
        var font = new SystemFont
        {
            FontNames = CjkFamilies,
            AllowSystemFallback = true,
        };
        return font;
    }

    public static bool NeedsCjk(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if (c >= 0x2e80) return true;
        }
        return false;
    }

    public Font For(string text) => NeedsCjk(text) ? _cjk : _primary;

    // FDF 的字号就是 em 尺寸, 与客户端 UiRenderDevice 一致: fontSize * ScaleY 直接做光栅字号.
    // 不按字体的 ascent+descent 回缩, 否则中英文会因为量到的行框不同而大小不一.
    public float PixelSizeForLine(string text, float lineHeight) =>
        lineHeight <= 0f ? 1f : lineHeight;

    public float Measure(string text, float lineHeight)
    {
        Font font = For(text);
        return font.GetStringSize(UiText.Strip(text), HorizontalAlignment.Left, -1,
            Mathf.Max(1, Mathf.RoundToInt(PixelSizeForLine(text, lineHeight)))).X;
    }
}
