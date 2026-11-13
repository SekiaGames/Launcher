using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game.UI;

public sealed partial class PreGameDialog : Control
{
    private static readonly (string Label, int Race)[] Races =
    {
        ("人族", 1), ("兽族", 2), ("不死族", 3), ("暗夜精灵", 4),
    };

    private static readonly (string Label, int Difficulty)[] Difficulties =
    {
        ("普通", 1), ("疯狂", 2), ("彻底疯狂", 2),
    };

    private readonly VBoxContainer _box;
    private readonly Label _title;
    private readonly Label _hint;
    private int _race = 1;

    public Action<int, int> Confirmed;
    public Action Cancelled;

    public PreGameDialog(string mapName)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        AddChild(new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.82f),
            MouseFilter = MouseFilterEnum.Ignore,
            Name = "Backdrop",
        });

        _box = new VBoxContainer { Name = "Options" };
        _box.AddThemeConstantOverride("separation", 12);
        AddChild(_box);

        _title = new Label { Text = "选择玩家种族", HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 28);
        _box.AddChild(_title);

        _hint = new Label
        {
            Text = mapName ?? "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _hint.AddThemeFontSizeOverride("font_size", 14);
        _hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        _box.AddChild(_hint);

        ShowRaces();
    }

    public override void _Ready()
    {
        Vector2 viewport = GetViewportRect().Size;
        var backdrop = GetNodeOrNull<ColorRect>("Backdrop");
        if (backdrop != null)
        {
            backdrop.Position = Vector2.Zero;
            backdrop.Size = viewport;
        }

        const float width = 460f, height = 380f;
        _box.Position = new Vector2((viewport.X - width) * 0.5f, (viewport.Y - height) * 0.5f);
        _box.Size = new Vector2(width, height);
    }

    private void ShowRaces()
    {
        _title.Text = "选择玩家种族";
        ClearOptions();
        foreach ((string label, int race) in Races)
        {
            int captured = race;
            AddOption(label, () =>
            {
                _race = captured;
                ShowDifficulties();
            });
        }
        AddOption("取消", () => Cancelled?.Invoke(), muted: true);
    }

    private void ShowDifficulties()
    {
        _title.Text = "选择电脑难度";
        ClearOptions();
        foreach ((string label, int difficulty) in Difficulties)
        {
            int captured = difficulty;
            AddOption(label, () => Confirmed?.Invoke(_race, captured));
        }
        AddOption("返回", ShowRaces, muted: true);
    }

    private void ClearOptions()
    {
        var stale = new List<Node>();
        for (int i = 2; i < _box.GetChildCount(); i++) stale.Add(_box.GetChild(i));
        foreach (Node node in stale)
        {
            _box.RemoveChild(node);
            node.QueueFree();
        }
    }

    private void AddOption(string label, Action action, bool muted = false)
    {
        var button = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(0f, 52f),
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        if (muted) button.AddThemeColorOverride("font_color", new Color(0.72f, 0.74f, 0.78f));
        button.Pressed += action;
        _box.AddChild(button);
    }
}
