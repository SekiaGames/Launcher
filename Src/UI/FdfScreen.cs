using Godot;
using System.Collections.Generic;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class FdfScreen : Control
{
    public const float DesignWidth = 1024f;
    public const float DesignHeight = 768f;

    const string GlobalStrings = @"UI\FrameDef\GlobalStrings.fdf";

    UiRoot _root;
    GodotUiDrawDevice _device;
    FdfUiFactory _factory;

    public int FrameCount => _root?.Children.Count ?? 0;

    public void Load(string fdfPath, string templateName)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _factory = new FdfUiFactory(new GodotUiTextureResolver(), FdfSource.Read);
        _factory.LoadTemplate(GlobalStrings);
        _factory.LoadTemplate(fdfPath);
        _root = new UiRoot();
        UiFrame frame = _factory.Create(templateName, _root);

        _device = new GodotUiDrawDevice(this, ThemeDB.FallbackFont);
        QueueRedraw();
    }

    public string BackgroundPrefix { get; set; } = @"UI\Glues\Loading\Load-Generic\Generic-Loading-";

    void DrawLoadingBackground(IUiRenderDevice device)
    {
        var resolver = new GodotUiTextureResolver();
        ITexture tl = resolver.Resolve(BackgroundPrefix + "TopLeft.png");
        ITexture tr = resolver.Resolve(BackgroundPrefix + "TopRight.png");
        ITexture bl = resolver.Resolve(BackgroundPrefix + "BotLeft.png");
        ITexture br = resolver.Resolve(BackgroundPrefix + "BotRight.png");
        if (tl == null || tr == null || bl == null || br == null) return;

        float halfWidth = DesignWidth * .5f;
        float halfHeight = DesignHeight * .5f;
        var white = new System.Numerics.Vector4(1f, 1f, 1f, 1f);
        device.DrawTexturedQuad(new UiRect(0f, 0f, halfWidth, halfHeight), tl, white);
        device.DrawTexturedQuad(new UiRect(halfWidth, 0f, halfWidth, halfHeight), tr, white);
        device.DrawTexturedQuad(new UiRect(0f, halfHeight, halfWidth, halfHeight), bl, white);
        device.DrawTexturedQuad(new UiRect(halfWidth, halfHeight, halfWidth, halfHeight), br, white);
    }

    public override void _Draw()
    {
        if (_root == null || _device == null) return;
        Vector2 viewport = GetViewportRect().Size;
        float scaleX = viewport.X / DesignWidth;
        float scaleY = viewport.Y / DesignHeight;
        _device.Begin(viewport.X, viewport.Y, scaleX, scaleY);
        _root.UpdateViewport(DesignWidth, DesignHeight);
        DrawLoadingBackground(_device);
        _root.Render(_device);
        _device.End();
    }
}
