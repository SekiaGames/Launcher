using Godot;
using System;
using OpenWC3.Game.UI.Generated;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class FdfLoadingScreen : Control
{
    private const float DesignWidth = War3UiViewport.BaseWidth;
    private const float DesignHeight = War3UiViewport.BaseHeight;
    private const float BarWidth = 512f, BarHeight = 64f, BarBottomMargin = 52f;

    private readonly CanvasLayer _layer;
    private UiRoot _root;
    private LoadingScreenBinding _binding;
    private GodotUiRenderDevice _device;
    private float _remaining;
    private float _progress;
    private bool _fadeOut;

    public FdfLoadingScreen(Node parent, MapSource map, float seconds = 2.2f)
    {
        _remaining = Mathf.Max(0.4f, seconds);

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        War3UiSkin skin = War3UiSkin.Load();
        GodotWar3FontFace font = GodotWar3FontFace.Load(skin);
        var resolver = new GodotUiTextureResolver { Skin = skin };
        var factory = new FdfUiFactory(resolver, FdfSource.Read);

        _root = new UiRoot();
        _binding = LoadingScreenBinding.Load(factory, _root);

        string title = Text(map?.Info?.LoadingScreenTitle) ?? Text(map?.Name) ?? "";
        string subtitle = Text(map?.Info?.LoadingScreenSubtitle) ?? "";
        string body = Text(map?.Info?.LoadingScreenText) ?? "";
        if (_binding != null)
        {
            if (_binding.LoadingTitleText != null) _binding.LoadingTitleText.Text = title;
            if (_binding.LoadingSubtitleText != null) _binding.LoadingSubtitleText.Text = subtitle;
            if (_binding.LoadingText != null) _binding.LoadingText.Text = body;
            if (_binding.LoadingBarText != null) _binding.LoadingBarText.Text = "正在载入";
            if (_binding.LoadingMeleeMapName != null) _binding.LoadingMeleeMapName.Text = title;
            if (_binding.LoadingMeleeGameTypeValue != null) _binding.LoadingMeleeGameTypeValue.Text = "1v1";
        }

        _root.UpdateViewport(DesignWidth, DesignHeight);

        _device = new GodotUiRenderDevice(this, font.Font)
        {
            LineSizeResolver = font.PixelSizeForLine,
        };

        _layer = new CanvasLayer { Name = "FdfLoadingScreen", Layer = 50 };
        parent.AddChild(_layer);
        _layer.AddChild(this);
    }

    private static string Text(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : War3Strings.Resolve(value);

    public void SetProgress(float progress) => _progress = Mathf.Clamp(progress, 0f, 1f);

    public override void _Draw()
    {
        if (_root == null || _device == null) return;

        Vector2 viewport = GetViewportRect().Size;
        float scaleX = viewport.X / DesignWidth;
        float scaleY = viewport.Y / DesignHeight;

        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        DrawBackdrop(_device);

        _root.UpdateViewport(DesignWidth, DesignHeight);
        _root.Render(_device);

        DrawProgress(_device);
        _device.End();
    }

    private void DrawBackdrop(IUiRenderDevice device)
    {
        var black = new System.Numerics.Vector4(0f, 0f, 0f, 0.96f);
        device.DrawTexturedQuad(new UiRect(0, 0, DesignWidth, DesignHeight),
            device.WhiteTexture, black);
    }

    private void DrawProgress(IUiRenderDevice device)
    {
        float x = (DesignWidth - BarWidth) * 0.5f;
        float y = DesignHeight - BarBottomMargin - BarHeight;
        var frame = new UiRect(x, y, BarWidth, BarHeight);

        device.DrawTexturedQuad(frame, device.WhiteTexture, new System.Numerics.Vector4(0f, 0f, 0f, 0.85f));
        device.DrawTexturedQuad(new UiRect(x + 2, y + 2, BarWidth - 4, BarHeight - 4),
            device.WhiteTexture, new System.Numerics.Vector4(0.32f, 0.26f, 0.12f, 1f));

        float filled = (BarWidth - 8) * _progress;
        if (filled > 0.5f)
        {
            device.DrawTexturedQuad(new UiRect(x + 4, y + 4, filled, BarHeight - 8),
                device.WhiteTexture, new System.Numerics.Vector4(0.93f, 0.76f, 0.28f, 1f));
        }
    }

    public void FrameSeconds(float delta)
    {
        if (_remaining <= 0f) return;
        _remaining -= delta;
        _progress = Mathf.Clamp(1f - _remaining / 2.2f, 0f, 1f);

        if (_remaining > 0.5f)
        {
            QueueRedraw();
            return;
        }
        _fadeOut = true;
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_remaining / 0.5f, 0f, 1f));
        QueueRedraw();
        if (_remaining > 0f) return;

        _layer?.QueueFree();
        QueueFree();
    }
}
