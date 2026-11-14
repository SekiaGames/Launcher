using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.RHI;
using OpenWC3.UI;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace OpenWC3.Game.UI;

public sealed class GodotUiRenderDevice : IUiRenderDevice
{
    private readonly CanvasItem _target;
    private readonly List<UiRect> _clips = new();
    private ITexture _white;
    private ITexture _radialCircle;
    private Font _font;
    private Func<string, float, float> _lineSizeResolver;

    private float _scaleX = 1f;
    private float _scaleY = 1f;
    private float _offsetX;
    private CanvasItem _additiveTarget;
    private Rid _additiveItem;
    private bool _additiveReady;
    private Texture2D _cooldownTexture;
    private GodotUiTexture _completionUi;
    private GodotUiTexture _autoCastUi;

    public CanvasItem AdditiveTarget
    {
        get => _additiveTarget;
        set
        {
            _additiveTarget = value;
            _additiveReady = false;
        }
    }

    public GodotUiRenderDevice(CanvasItem target, Font font = null)
    {
        _target = target;
        _font = font ?? ThemeDB.FallbackFont;
    }

    public Font Font
    {
        get => _font;
        set => _font = value ?? ThemeDB.FallbackFont;
    }

    public Func<string, float, float> LineSizeResolver
    {
        get => _lineSizeResolver;
        set => _lineSizeResolver = value;
    }

    public void Begin(float viewportWidth, float viewportHeight, float scaleX, float scaleY)
        => Begin(viewportWidth, viewportHeight, scaleX, scaleY, 0f);

    public void Begin(float viewportWidth, float viewportHeight, float scaleX, float scaleY, float pixelOffsetX)
    {
        _scaleX = scaleX <= 0f ? 1f : scaleX;
        _scaleY = scaleY <= 0f ? 1f : scaleY;
        _offsetX = pixelOffsetX;
        _clips.Clear();
        _additiveReady = _additiveTarget != null && GodotObject.IsInstanceValid(_additiveTarget);
        if (!_additiveReady) return;
        _additiveItem = _additiveTarget.GetCanvasItem();
        RenderingServer.CanvasItemClear(_additiveItem);
    }

    public void End() => _clips.Clear();

    public float SquareWidthRatio => _scaleY / _scaleX;

    public double AnimationTimeSeconds => Time.GetTicksMsec() / 1000.0;

    public int TextMeasurementRevision => 0;

    public ITexture WhiteTexture => _white ??= new GodotUiTexture(MakeWhite());

    public ITexture RadialCircleTexture => _radialCircle ??= new GodotUiTexture(MakeRadialCircle());

    private static Texture2D MakeWhite()
    {
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        image.SetPixel(0, 0, Colors.White);
        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D MakeRadialCircle()
    {
        const int size = 64;
        const float half = size * 0.5f;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float distance = MathF.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp(1f - distance, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    public void DrawTexturedQuad(UiRect rect, ITexture texture, NVector4 color, UiRect uv = default)
    {
        if (texture is not GodotUiTexture godot || godot.Source == null) return;
        if (!Clip(ref rect, ref uv)) return;
        _target.DrawTextureRectRegion(godot.Source, ToPixels(rect), SourceRect(godot.Source, uv), ToColor(color));
    }

    public void DrawTexturedQuadAdd(UiRect rect, ITexture texture, NVector4 color, UiRect uv = default)
    {
        if (texture is not GodotUiTexture godot || godot.Source == null) return;
        if (!Clip(ref rect, ref uv)) return;
        if (!_additiveReady)
        {
            _target.DrawTextureRectRegion(godot.Source, ToPixels(rect),
                SourceRect(godot.Source, uv), ToColor(color));
            return;
        }
        RenderingServer.CanvasItemAddTextureRectRegion(_additiveItem, ToPixels(rect),
            godot.Source.GetRid(), SourceRect(godot.Source, uv), ToColor(color));
    }

    public void DrawTexturedQuadTiled(UiRect rect, ITexture texture, NVector4 color, UiRect uv, bool transpose = false)
    {
        if (texture is not GodotUiTexture godot || godot.Source == null) return;
        if (!Clip(ref rect, ref uv)) return;

        float textureWidth = godot.Source.GetWidth() * Mathf.Max(uv.Width, 0.0001f);
        float textureHeight = godot.Source.GetHeight() * Mathf.Max(uv.Height, 0.0001f);
        float tileWidth = transpose ? textureHeight : textureWidth;
        float tileHeight = transpose ? textureWidth : textureHeight;
        if (tileWidth <= 0f || tileHeight <= 0f) return;
        if (rect.Width <= 0f || rect.Height <= 0f) return;

        Rect2 source = SourceRect(godot.Source, uv);
        float x = rect.X;
        while (x < rect.X + rect.Width - 0.01f)
        {
            float w = Mathf.Min(tileWidth, rect.X + rect.Width - x);
            float y = rect.Y;
            while (y < rect.Y + rect.Height - 0.01f)
            {
                float h = Mathf.Min(tileHeight, rect.Y + rect.Height - y);
                Rect2 part = new(source.Position, new Vector2(
                    source.Size.X * (w / tileWidth), source.Size.Y * (h / tileHeight)));
                _target.DrawTextureRectRegion(godot.Source,
                    ToPixels(new UiRect(x, y, w, h)), part, ToColor(color));
                y += tileHeight;
            }
            x += tileWidth;
        }
    }

    public void DrawCooldownSweep(UiRect rect, float sweep, float innerRatio, NVector4 color)
    {
        float amount = Mathf.Clamp(sweep, 0f, 1f);
        if (amount <= 0f || rect.Width <= 0f || rect.Height <= 0f) return;
        UiRect uv = default;
        if (!Clip(ref rect, ref uv)) return;

        float centerX = rect.X + rect.Width * 0.5f;
        float centerY = rect.Y + rect.Height * 0.5f;
        float radius = Mathf.Max(rect.Width, rect.Height) * 0.5f * 1.02f;
        int segments = Mathf.Max(3, Mathf.CeilToInt(48 * amount));

        var points = new Vector2[segments + 2];
        var colors = new Color[points.Length];
        Texture2D texture = CooldownMaskTexture;
        Vector2[] uvs = texture == null ? null : new Vector2[points.Length];
        Color shade = texture == null
            ? ToColor(color)
            : new Color(color.X, color.Y, color.Z, Mathf.Clamp(color.W / CooldownMaskAlpha, 0f, 1f));

        points[0] = new Vector2(_offsetX + centerX * _scaleX, centerY * _scaleY);
        colors[0] = shade;
        if (uvs != null) uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i <= segments; i++)
        {
            float angle = -Mathf.Pi * 0.5f + Mathf.Tau * amount * (i / (float)segments);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            var point = new NVector2(centerX + cos * radius, centerY + sin * radius);
            points[i + 1] = new Vector2(_offsetX + point.X * _scaleX, point.Y * _scaleY);
            colors[i + 1] = shade;
            if (uvs != null) uvs[i + 1] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);
        }

        if (uvs != null) _target.DrawPolygon(points, colors, uvs, texture);
        else _target.DrawColoredPolygon(points, ToColor(color));
    }

    public bool DrawCooldownCompletion(UiRect rect, float elapsedSeconds)
    {
        if (elapsedSeconds < 0f || elapsedSeconds >= CooldownCompletionSeconds) return false;
        ITexture texture = CompletionUiTexture;
        if (texture == null) return false;
        float scale = CompletionScale(elapsedSeconds / CooldownCompletionSeconds);
        float size = Mathf.Max(rect.Width, rect.Height) * scale;
        float centerX = rect.X + rect.Width * 0.5f;
        float centerY = rect.Y + rect.Height * 0.5f;
        DrawTexturedQuadAdd(
            new UiRect(centerX - size * 0.5f, centerY - size * 0.5f, size, size),
            texture, new NVector4(0.407843f, 0.603922f, 1f, 1f));
        return true;
    }

    public bool DrawMinimapCamp(UiRect rect, bool tough, float elapsedSeconds, NVector4 color) => false;

    public bool DrawAutoCast(UiRect rect, object button, float elapsedSeconds)
    {
        if (button == null || rect.Width <= 0f || rect.Height <= 0f) return false;
        ITexture texture = AutoCastUiTexture;
        if (texture == null) return false;

        float phase = Mathf.PosMod(elapsedSeconds / AutoCastCycleSeconds, 1f);
        float inset = Mathf.Max(1f, Mathf.Min(rect.Width, rect.Height) * 0.07f);
        float size = Mathf.Min(rect.Width, rect.Height) * 0.34f;
        for (int index = 0; index < 4; index++)
        {
            float u = Mathf.PosMod(phase + index * 0.25f, 1f);
            Vector2 point = PerimeterPoint(rect, inset, u);
            float pulse = 0.6f + 0.4f * Mathf.Sin(u * Mathf.Tau * 2f);
            DrawTexturedQuadAdd(
                new UiRect(point.X - size * 0.5f, point.Y - size * 0.5f, size, size),
                texture, new NVector4(1f, 1f, 1f, pulse));
        }
        return true;
    }

    private static Vector2 PerimeterPoint(UiRect rect, float inset, float u)
    {
        float left = rect.X + inset;
        float top = rect.Y + inset;
        float right = rect.X + rect.Width - inset;
        float bottom = rect.Y + rect.Height - inset;
        float width = Mathf.Max(0f, right - left);
        float height = Mathf.Max(0f, bottom - top);
        float perimeter = 2f * (width + height);
        if (perimeter <= 0f) return new Vector2(left, top);

        float distance = Mathf.PosMod(u, 1f) * perimeter;
        if (distance < height) return new Vector2(right, top + distance);
        distance -= height;
        if (distance < width) return new Vector2(right - distance, bottom);
        distance -= width;
        if (distance < height) return new Vector2(left, bottom - distance);
        distance -= height;
        return new Vector2(left + Mathf.Min(distance, width), top);
    }

    private const float CooldownMaskAlpha = 0.6f;
    private const float CooldownCompletionSeconds = 1f;
    private const float AutoCastCycleSeconds = 1.667f;

    private static readonly float[] CompletionKeyTimes =
    {
        0f, 0.08325f, 0.124875f, 0.1665f, 0.208125f, 0.24975f, 0.291375f, 0.333f,
        0.374625f, 0.41625f, 0.4995f, 0.58275f, 0.624375f, 0.666f, 0.70775f, 0.7495f,
        0.79125f, 0.812125f, 0.833f, 0.87475f, 0.9165f, 0.95825f, 1f,
    };

    private static readonly float[] CompletionKeyScales =
    {
        1f, 1.358869f, 1.510828f, 1.639706f, 1.741929f, 1.813925f, 1.852119f, 1.85294f,
        1.846955f, 1.798941f, 1.625156f, 1.428251f, 1.351336f, 1.30489f, 1.370598f, 1.477934f,
        1.573778f, 1.60079f, 1.60501f, 1.583908f, 1.492432f, 1.344665f, 1.15469f,
    };

    private static float CompletionScale(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        for (int i = 1; i < CompletionKeyTimes.Length; i++)
        {
            if (t > CompletionKeyTimes[i]) continue;
            float span = Mathf.Max(0.0001f, CompletionKeyTimes[i] - CompletionKeyTimes[i - 1]);
            float ratio = (t - CompletionKeyTimes[i - 1]) / span;
            return Mathf.Lerp(CompletionKeyScales[i - 1], CompletionKeyScales[i], ratio);
        }
        return CompletionKeyScales[^1];
    }

    private Texture2D CooldownMaskTexture =>
        _cooldownTexture ??= LoadUiTexture(@"UI\Feedback\Cooldown\cooldown.blp");

    private ITexture CompletionUiTexture =>
        _completionUi ??= CreateUiTexture(@"UI\Feedback\Cooldown\star4.blp");

    private ITexture AutoCastUiTexture =>
        _autoCastUi ??= CreateUiTexture(@"UI\Feedback\Autocast\GlowStar.blp");

    private static GodotUiTexture CreateUiTexture(string path)
    {
        Texture2D texture = LoadUiTexture(path);
        return texture == null ? null : new GodotUiTexture(texture);
    }

    private static Texture2D LoadUiTexture(string path)
    {
        string converted = War3Assets.ConvertedPath(path);
        return converted == null ? null : Assets.LoadTexture(converted);
    }

    public void DrawText(UiRect rect, string text, float fontSize, NVector4 color, TextAlignment align,
        NVector4? shadowColor = null, NVector2 shadowOffset = default,
        TextVerticalAlignment verticalAlign = TextVerticalAlignment.Top)
        => DrawTextLine(rect, text, fontSize, color, align, shadowColor, shadowOffset, verticalAlign);

    public void DrawTextLine(UiRect rect, string text, float lineHeight, NVector4 color, TextAlignment align,
        NVector4? shadowColor = null, NVector2 shadowOffset = default,
        TextVerticalAlignment verticalAlign = TextVerticalAlignment.Top)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!Visible(rect)) return;

        string normalized = text.Replace("\r", "").Replace("|n", "\n").Replace("|N", "\n");
        string[] lines = normalized.Split('\n');
        int pixelSize = Mathf.Max(1, Mathf.RoundToInt(LineSize(text, lineHeight) * _scaleY));
        Rect2 area = ToPixels(rect);
        float ascent = _font.GetAscent(pixelSize);
        float lineAdvance = Mathf.Max(lineHeight, _font.GetHeight(pixelSize) / _scaleY);
        float totalHeight = lineAdvance * lines.Length * _scaleY;

        float startY = area.Position.Y + ascent;
        if (verticalAlign == TextVerticalAlignment.Middle)
            startY += (area.Size.Y - totalHeight) * 0.5f;
        else if (verticalAlign == TextVerticalAlignment.Bottom)
            startY += area.Size.Y - totalHeight;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.Length == 0) continue;
            var runs = SplitColorRuns(line);
            float textWidth = 0f;
            foreach ((string runText, uint argb) in runs)
                if (runText.Length > 0) textWidth += _font.GetStringSize(runText, HorizontalAlignment.Left, -1, pixelSize).X;

            float x = area.Position.X;
            if (align == TextAlignment.Center) x += (area.Size.X - textWidth) * 0.5f;
            else if (align == TextAlignment.Right) x += area.Size.X - textWidth;

            float y = startY + index * lineAdvance * _scaleY;
            foreach ((string runText, uint argb) in runs)
            {
                if (runText.Length == 0) continue;
                NVector4 runColor = argb == 0 ? color : ColorFromArgb(argb, color.W);
                if (shadowColor.HasValue)
                {
                    _target.DrawString(_font, new Vector2(x + shadowOffset.X * _scaleY, y + shadowOffset.Y * _scaleY),
                        runText, HorizontalAlignment.Left, -1, pixelSize, ToColor(shadowColor.Value));
                }
                _target.DrawString(_font, new Vector2(x, y), runText,
                    HorizontalAlignment.Left, -1, pixelSize, ToColor(runColor));
                x += _font.GetStringSize(runText, HorizontalAlignment.Left, -1, pixelSize).X;
            }
        }
    }

    static readonly List<(string Text, uint Argb)> ColorRunBuffer = new(8);

    static List<(string Text, uint Argb)> SplitColorRuns(string line)
    {
        var runs = ColorRunBuffer;
        runs.Clear();
        var text = new System.Text.StringBuilder(line.Length);
        uint color = 0;
        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];
            if (current != '|' || i + 1 >= line.Length)
            {
                text.Append(current);
                continue;
            }
            char next = line[i + 1];
            if ((next is 'c' or 'C') && i + 9 < line.Length && IsHexRun(line, i + 2))
            {
                if (text.Length > 0)
                {
                    runs.Add((text.ToString(), color));
                    text.Clear();
                }
                color = Convert.ToUInt32(line.Substring(i + 2, 8), 16);
                i += 9;
                continue;
            }
            if (next is 'r' or 'R')
            {
                if (text.Length > 0)
                {
                    runs.Add((text.ToString(), color));
                    text.Clear();
                }
                color = 0;
                i += 1;
                continue;
            }
            if (next == '|')
            {
                text.Append('|');
                i += 1;
                continue;
            }
            text.Append(current);
        }
        if (text.Length > 0) runs.Add((text.ToString(), color));
        return runs;
    }

    static bool IsHexRun(string line, int start)
    {
        for (int i = 0; i < 8; i++)
        {
            char c = line[start + i];
            bool hex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!hex) return false;
        }
        return true;
    }

    static NVector4 ColorFromArgb(uint argb, float alphaScale)
    {
        float a = ((argb >> 24) & 0xFF) / 255f;
        float r = ((argb >> 16) & 0xFF) / 255f;
        float g = ((argb >> 8) & 0xFF) / 255f;
        float b = (argb & 0xFF) / 255f;
        return new NVector4(r, g, b, a * alphaScale);
    }

    public float MeasureTextWidth(string text, float fontSize)
        => MeasureTextLineWidth(text, fontSize);

    public float MeasureTextHeight(string text, float fontSize)
    {
        string plain = UiText.Strip(text);
        if (string.IsNullOrEmpty(plain)) return 0f;
        int pixelSize = Mathf.Max(1, Mathf.RoundToInt(LineSize(plain, fontSize) * _scaleY));
        return _font.GetHeight(pixelSize) / _scaleY;
    }

    public int TextPixelSize(string text, float lineHeight) =>
        Mathf.Max(1, Mathf.RoundToInt(LineSize(text, lineHeight) * _scaleY));

    public float MeasureTextLineWidth(string text, float lineHeight)
    {
        string plain = UiText.Strip(text);
        if (string.IsNullOrEmpty(plain)) return 0f;
        int pixelSize = Mathf.Max(1, Mathf.RoundToInt(LineSize(plain, lineHeight) * _scaleY));
        return _font.GetStringSize(plain, HorizontalAlignment.Left, -1, pixelSize).X / _scaleX;
    }

    public void PushScissor(UiRect rect) => _clips.Add(rect);

    public void PopScissor()
    {
        if (_clips.Count > 0) _clips.RemoveAt(_clips.Count - 1);
    }

    private float LineSize(string text, float lineHeight) =>
        _lineSizeResolver?.Invoke(text, lineHeight) ?? lineHeight;

    private bool Visible(UiRect rect)
    {
        if (_clips.Count == 0) return true;
        Rect2 clip = CurrentClip();
        return rect.X < clip.End.X && rect.X + rect.Width > clip.Position.X
            && rect.Y < clip.End.Y && rect.Y + rect.Height > clip.Position.Y;
    }

    private Rect2 CurrentClip()
    {
        UiRect clip = _clips[^1];
        return new Rect2(clip.X, clip.Y, clip.Width, clip.Height);
    }

    private bool Clip(ref UiRect rect, ref UiRect uv)
    {
        if (_clips.Count == 0) return rect.Width > 0f && rect.Height > 0f;

        Rect2 clip = CurrentClip();
        float left = Mathf.Max(rect.X, clip.Position.X);
        float top = Mathf.Max(rect.Y, clip.Position.Y);
        float right = Mathf.Min(rect.X + rect.Width, clip.End.X);
        float bottom = Mathf.Min(rect.Y + rect.Height, clip.End.Y);
        if (right <= left || bottom <= top) return false;

        if (uv.Width > 0f && uv.Height > 0f)
        {
            float u0 = uv.X + uv.Width * (left - rect.X) / rect.Width;
            float v0 = uv.Y + uv.Height * (top - rect.Y) / rect.Height;
            float u1 = uv.X + uv.Width * (right - rect.X) / rect.Width;
            float v1 = uv.Y + uv.Height * (bottom - rect.Y) / rect.Height;
            uv = new UiRect(u0, v0, u1 - u0, v1 - v0);
        }
        rect = new UiRect(left, top, right - left, bottom - top);
        return true;
    }

    private Rect2 ToPixels(UiRect rect) =>
        new(_offsetX + rect.X * _scaleX, rect.Y * _scaleY, rect.Width * _scaleX, rect.Height * _scaleY);

    private static Rect2 SourceRect(Texture2D texture, UiRect uv)
    {
        float width = texture.GetWidth();
        float height = texture.GetHeight();
        return uv.Width > 0 && uv.Height > 0
            ? new Rect2(uv.X * width, uv.Y * height, uv.Width * width, uv.Height * height)
            : new Rect2(0, 0, width, height);
    }

    private static Color ToColor(NVector4 value) => new(value.X, value.Y, value.Z, value.W);

    public void Dispose()
    {
        _white = null;
        _radialCircle = null;
        _clips.Clear();
    }
}
