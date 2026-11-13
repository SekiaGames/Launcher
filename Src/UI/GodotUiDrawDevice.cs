using Godot;
using System;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed class GodotUiDrawDevice : IUiRenderDevice
{
    readonly CanvasItem _target;
    readonly Font _font;
    ITexture _white;

    float _scaleX = 1f;
    float _scaleY = 1f;

    public GodotUiDrawDevice(CanvasItem target, Font font)
    {
        _target = target;
        _font = font ?? ThemeDB.FallbackFont;
    }

    public void Begin(float viewportWidth, float viewportHeight, float scaleX, float scaleY)
    {
        _scaleX = scaleX <= 0f ? 1f : scaleX;
        _scaleY = scaleY <= 0f ? 1f : scaleY;
    }

    public void End() { }

    public float SquareWidthRatio => _scaleY / _scaleX;

    public double AnimationTimeSeconds => Time.GetTicksMsec() / 1000.0;

    public ITexture WhiteTexture => _white ??= new GodotUiTexture(MakeWhite());

    public ITexture RadialCircleTexture => null;

    static Texture2D MakeWhite()
    {
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        image.SetPixel(0, 0, Colors.White);
        return ImageTexture.CreateFromImage(image);
    }

    public void DrawTexturedQuad(UiRect rect, ITexture texture, System.Numerics.Vector4 color, UiRect uv = default)
    {
        if (texture is not GodotUiTexture godot || godot.Source == null || rect.Width <= 0 || rect.Height <= 0) return;
        Rect2 source = SourceRect(godot.Source, uv);
        _target.DrawTextureRectRegion(godot.Source, ToPixels(rect), source, ToColor(color));
    }

    public void DrawTexturedQuadAdd(UiRect rect, ITexture texture, System.Numerics.Vector4 color, UiRect uv = default)
        => DrawTexturedQuad(rect, texture, color, uv);

    public void DrawTexturedQuadTiled(UiRect rect, ITexture texture, System.Numerics.Vector4 color, UiRect uv, bool transpose = false)
        => DrawTexturedQuad(rect, texture, color, uv);

    public void DrawCooldownSweep(UiRect rect, float sweep, float innerRatio, System.Numerics.Vector4 color) { }

    public bool DrawCooldownCompletion(UiRect rect, float elapsedSeconds) => false;

    public void DrawText(UiRect rect, string text, float fontSize, System.Numerics.Vector4 color, TextAlignment align,
        System.Numerics.Vector4? shadowColor = null, System.Numerics.Vector2 shadowOffset = default,
        TextVerticalAlignment verticalAlign = TextVerticalAlignment.Top)
    {
        if (string.IsNullOrEmpty(text)) return;
        float size = fontSize * _scaleY;
        Rect2 area = ToPixels(rect);
        var flags = HorizontalAlignment.Left;
        if (align == TextAlignment.Center) flags = HorizontalAlignment.Center;
        else if (align == TextAlignment.Right) flags = HorizontalAlignment.Right;

        Vector2 position = new(area.Position.X, area.Position.Y);
        if (verticalAlign == TextVerticalAlignment.Middle)
            position.Y += Mathf.Max(0f, (area.Size.Y - size) * 0.5f);

        if (shadowColor.HasValue)
        {
            _target.DrawString(_font, position + new Vector2(shadowOffset.X, shadowOffset.Y) * _scaleY, text,
                flags, area.Size.X, (int)size, ToColor(shadowColor.Value));
        }
        _target.DrawString(_font, position, text, flags, area.Size.X, (int)size, ToColor(color));
    }

    public float MeasureTextWidth(string text, float fontSize)
        => string.IsNullOrEmpty(text) ? 0f : _font.GetStringSize(text, HorizontalAlignment.Left, -1, (int)fontSize).X;

    public void PushScissor(UiRect rect) { }

    public void PopScissor() { }

    Rect2 ToPixels(UiRect rect) => new(rect.X * _scaleX, rect.Y * _scaleY, rect.Width * _scaleX, rect.Height * _scaleY);

    static Rect2 SourceRect(Texture2D texture, UiRect uv)
        => uv.Width > 0 && uv.Height > 0
            ? new Rect2(uv.X * texture.GetWidth(), uv.Y * texture.GetHeight(), uv.Width * texture.GetWidth(), uv.Height * texture.GetHeight())
            : new Rect2(0, 0, texture.GetWidth(), texture.GetHeight());

    static Color ToColor(System.Numerics.Vector4 value) => new(value.X, value.Y, value.Z, value.W);

    public void Dispose() { }
}
