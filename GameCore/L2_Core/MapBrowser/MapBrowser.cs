using Godot;
using System;

namespace OpenWC3;

public sealed partial class MapBrowser : Control
{
    public Action<string> OnPick;

    public MapBrowser()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Pass;
    }

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Color = new Color(0.06f, 0.07f, 0.09f),
            Position = Vector2.Zero,
            Size = GetViewportRect().Size,
            MouseFilter = MouseFilterEnum.Ignore,
        });

        var box = new VBoxContainer
        {
            Position = new Vector2(120f, 90f),
            Size = new Vector2(900f, 700f),
        };
        box.AddThemeConstantOverride("separation", 14);
        AddChild(box);

        var title = new Label { Text = "Warcraft III Launcher" };
        title.AddThemeFontSizeOverride("font_size", 40);
        box.AddChild(title);

        var status = new Label
        {
            Text = Assets.ExtractMounted
                ? $"资源包已挂载：{Assets.ExtractPck}"
                : $"未找到资源包：{Assets.ExtractPck}",
        };
        status.AddThemeFontSizeOverride("font_size", 15);
        status.AddThemeColorOverride("font_color", Assets.ExtractMounted ? Color.FromHtml("7ee07e") : Color.FromHtml("e07e7e"));
        box.AddChild(status);

        box.AddChild(new HSeparator());

        string[] maps = Assets.ListGmaps();
        var heading = new Label { Text = maps.Length > 0 ? "选择地图" : "根目录没有 .gmap" };
        heading.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(heading);

        foreach (string name in maps)
        {
            var button = new Button
            {
                Text = "  " + name,
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(640f, 56f),
            };
            button.AddThemeFontSizeOverride("font_size", 20);
            string picked = name;
            button.Pressed += () => OnPick?.Invoke(picked);
            box.AddChild(button);
        }

        var hint = new Label
        {
            Text = maps.Length > 0
                ? "点击地图进入对局。ESC 返回本列表。"
                : "先在根目录生成 gmap：\n"
                  + "Godot --headless --path Extract -- --gmap-export=\"<地图.w3x>|<输出.gmap>\"",
        };
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.75f));
        box.AddChild(hint);
    }
}