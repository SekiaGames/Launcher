using Godot;
using System;

namespace OpenWC3.Game.UI;

public sealed class EscMenuFrame
{
    private readonly CanvasLayer _layer;
    private readonly Button _pauseButton;

    public Action PauseToggled;
    public Action OptionsRequested;

    public EscMenuFrame(Node parent, Action backToMapList)
    {
        _layer = new CanvasLayer { Name = "EscMenuFrame", Layer = 60, Visible = false };
        parent.AddChild(_layer);

        var veil = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.72f),
            Position = Vector2.Zero,
            Size = new Vector2(1600f, 900f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _layer.AddChild(veil);

        var panel = new ColorRect
        {
            Color = new Color(0.08f, 0.09f, 0.11f, 0.96f),
            Position = new Vector2(600f, 270f),
            Size = new Vector2(400f, 360f),
        };
        _layer.AddChild(panel);

        var title = new Label
        {
            Text = "游戏菜单",
            Position = new Vector2(600f, 290f),
            Size = new Vector2(400f, 40f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.86f, 0.6f));
        _layer.AddChild(title);

        AddButton(350f, "继续游戏（Esc）", () => Close());
        AddButton(420f, "选项", () =>
        {
            Close();
            OptionsRequested?.Invoke();
        });
        _pauseButton = AddButton(490f, "暂停游戏", () =>
        {
            PauseToggled?.Invoke();
        });
        AddButton(560f, "返回地图列表", () =>
        {
            Close();
            backToMapList?.Invoke();
        });
    }

    private Button AddButton(float y, string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Position = new Vector2(660f, y),
            Size = new Vector2(280f, 44f),
        };
        button.Pressed += action;
        _layer.AddChild(button);
        return button;
    }

    public void SetPaused(bool paused)
    {
        _pauseButton.Text = paused ? "继续游戏（暂停中）" : "暂停游戏";
    }

    public bool Visible => _layer.Visible;

    public void Open() => _layer.Visible = true;

    public void Close() => _layer.Visible = false;

    public void Toggle()
    {
        _layer.Visible = !_layer.Visible;
    }
}