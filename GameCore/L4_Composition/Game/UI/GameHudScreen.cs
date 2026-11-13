using Godot;
using System;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class GameHudScreen : Control
{
    public const float DesignWidth = 1024f;
    public const float DesignHeight = 768f;

    private UiRoot _root;
    private GodotUiRenderDevice _device;
    private GodotWar3FontFace _font;
    private ConsoleSkinFrame _console;
    private TopBarFrame _topBar;

    public int FrameCount => _root?.Children.Count ?? 0;

    public void Load(string race = "Human")
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        War3UiSkin skin = War3UiSkin.Load(race);
        _font = GodotWar3FontFace.Load(skin);
        var resolver = new GodotUiTextureResolver { Skin = skin };
        _device = new GodotUiRenderDevice(this, _font.Font)
        {
            LineSizeResolver = _font.PixelSizeForLine,
        };

        _root = new UiRoot();
        _console = new ConsoleSkinFrame(resolver, skin);
        _topBar = new TopBarFrame(resolver, skin);
        _root.AddChild(_console);
        _root.AddChild(_topBar);

        _topBar.UpdateResources(500, 220, 12, 24);
        _topBar.UpdateTime(6.0);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_root == null || _device == null) return;

        Vector2 viewport = GetViewportRect().Size;
        float scaleX = viewport.X / DesignWidth;
        float scaleY = viewport.Y / DesignHeight;

        _root.UpdateViewport(DesignWidth, DesignHeight);
        _console.Layout(DesignWidth, DesignHeight);
        _topBar.Layout(DesignWidth, DesignHeight);

        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        _root.Render(_device);
        _device.End();
    }
}
