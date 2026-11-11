using Godot;
using System;

namespace Launcher;

/// <summary>
/// 对局结束面板（客户端战役结算前的胜负提示）：大标题 + 胜者名单 + 返回列表。
/// </summary>
public sealed class GameOverPanel
{
    private readonly CanvasLayer _layer;
    private readonly Label _title;
    private readonly Label _detail;

    public GameOverPanel(Node parent, Action backToMapList)
    {
        _layer = new CanvasLayer { Name = "GameOver", Layer = 70, Visible = false };
        parent.AddChild(_layer);

        var veil = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.7f),
            Position = Vector2.Zero,
            Size = new Vector2(1600f, 900f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _layer.AddChild(veil);

        var panel = new ColorRect
        {
            Color = new Color(0.07f, 0.08f, 0.1f, 0.96f),
            Position = new Vector2(500f, 280f),
            Size = new Vector2(600f, 280f),
        };
        _layer.AddChild(panel);

        _title = new Label
        {
            Text = "",
            Position = new Vector2(500f, 306f),
            Size = new Vector2(600f, 60f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _title.AddThemeFontSizeOverride("font_size", 42);
        _title.AddThemeColorOverride("font_color", Color.FromHtml("ffd75e"));
        _title.AddThemeColorOverride("font_outline_color", Colors.Black);
        _title.AddThemeConstantOverride("outline_size", 8);
        _layer.AddChild(_title);

        _detail = new Label
        {
            Text = "",
            Position = new Vector2(530f, 376f),
            Size = new Vector2(540f, 90f),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _detail.AddThemeFontSizeOverride("font_size", 18);
        _detail.AddThemeColorOverride("font_color", new Color(0.88f, 0.9f, 0.94f));
        _layer.AddChild(_detail);

        var button = new Button
        {
            Text = "返回地图列表",
            Position = new Vector2(700f, 486f),
            Size = new Vector2(200f, 44f),
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.Pressed += () =>
        {
            _layer.Visible = false;
            backToMapList?.Invoke();
        };
        _layer.AddChild(button);
    }

    public void Show(bool victory, string winners)
    {
        _title.Text = victory ? "胜利" : "失败";
        _title.AddThemeColorOverride("font_color", victory ? Color.FromHtml("ffd75e") : Color.FromHtml("e2574c"));
        _detail.Text = string.IsNullOrEmpty(winners) ? "" : $"胜者：{winners}";
        _layer.Visible = true;
    }
}