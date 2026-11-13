using Godot;
using System.Collections.Generic;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class FdfScreen : Control
{
    public const float DesignWidth = 1024f;
    public const float DesignHeight = 768f;

    UiRoot _root;
    GodotUiDrawDevice _device;
    FdfUiFactory _factory;

    public int FrameCount => _root?.Children.Count ?? 0;

    public void Load(string fdfPath, string templateName)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _factory = new FdfUiFactory(new GodotUiTextureResolver(), FdfSource.Read);
        _factory.LoadTemplate(fdfPath);
        _root = new UiRoot();
        UiFrame frame = _factory.Create(templateName, _root);

        _device = new GodotUiDrawDevice(this, ThemeDB.FallbackFont);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_root == null || _device == null) return;
        float scaleX = Size.X / DesignWidth;
        float scaleY = Size.Y / DesignHeight;
        _device.Begin(Size.X, Size.Y, scaleX, scaleY);
        _root.UpdateViewport(DesignWidth, DesignHeight);
        _root.Render(_device);
        _device.End();
    }
}
