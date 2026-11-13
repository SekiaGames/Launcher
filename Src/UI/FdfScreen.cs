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
    GodotUiRenderDevice _device;
    FdfUiFactory _factory;
    GodotUiTextureResolver _resolver;

    public int FrameCount => _root?.Children.Count ?? 0;

    public void Load(string fdfPath, string templateName)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        War3UiSkin skin = War3UiSkin.Load();
        GodotWar3FontFace font = GodotWar3FontFace.Load(skin);
        _resolver = new GodotUiTextureResolver { Skin = skin };
        _factory = new FdfUiFactory(_resolver, FdfSource.Read);
        _factory.LoadTemplate(GlobalStrings);
        _factory.LoadTemplate(fdfPath);
        _root = new UiRoot();
        UiFrame frame = _factory.Create(templateName, _root);

        _device = new GodotUiRenderDevice(this, font.Font)
        {
            LineSizeResolver = font.PixelSizeForLine,
        };
        QueueRedraw();
    }

    public string BackgroundPrefix { get; set; } = @"UI\Glues\Loading\Load-Generic\Generic-Loading-";

    void DrawLoadingBackground(IUiRenderDevice device)
    {
        GodotUiTextureResolver resolver = _resolver ?? new GodotUiTextureResolver();
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
