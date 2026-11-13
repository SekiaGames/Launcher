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
        => DrawTexturedQuad(rect, texture, color, uv);

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
        points[0] = new Vector2(_offsetX + centerX * _scaleX, centerY * _scaleY);
        for (int i = 0; i <= segments; i++)
        {
            float angle = -Mathf.Pi * 0.5f + Mathf.Tau * amount * (i / (float)segments);
            var point = new NVector2(centerX + Mathf.Cos(angle) * radius, centerY + Mathf.Sin(angle) * radius);
            points[i + 1] = new Vector2(_offsetX + point.X * _scaleX, point.Y * _scaleY);
        }
        _target.DrawColoredPolygon(points, ToColor(color));
    }

    public bool DrawCooldownCompletion(UiRect rect, float elapsedSeconds) => false;

    public bool DrawMinimapCamp(UiRect rect, bool tough, float elapsedSeconds, NVector4 color) => false;

    public bool DrawAutoCast(UiRect rect, object button, float elapsedSeconds) => false;

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

        int pixelSize = Mathf.Max(1, Mathf.RoundToInt(LineSize(text, lineHeight) * _scaleY));
        Rect2 area = ToPixels(rect);
        float textWidth = _font.GetStringSize(text, HorizontalAlignment.Left, -1, pixelSize).X;
        float textHeight = _font.GetHeight(pixelSize);

        float x = area.Position.X;
        if (align == TextAlignment.Center) x += (area.Size.X - textWidth) * 0.5f;
        else if (align == TextAlignment.Right) x += area.Size.X - textWidth;

        float y = area.Position.Y;
        if (verticalAlign == TextVerticalAlignment.Middle) y += (area.Size.Y - textHeight) * 0.5f;
        else if (verticalAlign == TextVerticalAlignment.Bottom) y += area.Size.Y - textHeight;

        var position = new Vector2(x, y);
        if (shadowColor.HasValue)
        {
            _target.DrawString(_font, position + new Vector2(shadowOffset.X, shadowOffset.Y) * _scaleY,
                text, HorizontalAlignment.Left, -1, pixelSize, ToColor(shadowColor.Value));
        }
        _target.DrawString(_font, position, text, HorizontalAlignment.Left, -1, pixelSize, ToColor(color));
    }

    public float MeasureTextWidth(string text, float fontSize)
        => MeasureTextLineWidth(text, fontSize);

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
