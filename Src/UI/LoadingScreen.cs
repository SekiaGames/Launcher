using Godot;
using System;

namespace Launcher;

public sealed class LoadingScreen
{
    private readonly CanvasLayer _layer;
    private readonly ColorRect _veil;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _text;
    private float _remaining;

    public LoadingScreen(Node parent, MapSource map, float seconds = 2.2f)
    {
        _remaining = Mathf.Max(0.4f, seconds);

        _layer = new CanvasLayer { Name = "LoadingScreen", Layer = 50 };
        parent.AddChild(_layer);

        _veil = new ColorRect
        {
            Color = new Color(0.02f, 0.02f, 0.03f, 0.94f),
            Position = Vector2.Zero,
            Size = new Vector2(1600f, 900f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _layer.AddChild(_veil);

        string title = map?.Info != null && !string.IsNullOrWhiteSpace(map.Info.LoadingScreenTitle)
            ? War3Strings.Resolve(map.Info.LoadingScreenTitle)
            : (map != null ? War3Strings.Resolve(map.Name) : "");
        string subtitle = map?.Info != null && !string.IsNullOrWhiteSpace(map.Info.LoadingScreenSubtitle)
            ? War3Strings.Resolve(map.Info.LoadingScreenSubtitle)
            : "";
        string text = map?.Info != null && !string.IsNullOrWhiteSpace(map.Info.LoadingScreenText)
            ? War3Strings.Resolve(map.Info.LoadingScreenText)
            : "";

        _title = MakeLabel(title, 40, new Vector2(0f, 250f), new Color(0.95f, 0.86f, 0.6f), true);
        _layer.AddChild(_title);

        _subtitle = MakeLabel(subtitle, 22, new Vector2(0f, 320f), new Color(0.82f, 0.85f, 0.9f), true);
        _layer.AddChild(_subtitle);

        _text = MakeLabel(text, 17, new Vector2(0f, 700f), new Color(0.68f, 0.7f, 0.74f), true);
        _layer.AddChild(_text);

        var hint = MakeLabel("正在载入地图……", 18, new Vector2(0f, 820f), new Color(0.55f, 0.62f, 0.7f), true);
        _layer.AddChild(hint);
    }

    private static Label MakeLabel(string text, int size, Vector2 position, Color color, bool centered)
    {
        var label = new Label
        {
            Text = text ?? "",
            Position = position,
            Size = new Vector2(1600f, 40f),
            HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    public void Tick(float delta)
    {
        if (_remaining <= 0f) return;
        _remaining -= delta;
        if (_remaining > 0.5f) return;

        _veil.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_remaining / 0.5f, 0f, 1f));
        if (_remaining > 0f) return;

        _layer.QueueFree();
    }
}