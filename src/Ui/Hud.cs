using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class Hud
{
    private readonly Label _title;
    private readonly Label _resources;
    private readonly Label _selection;
    private readonly Label _hint;
    private readonly Button _trainButton;
    private readonly Label _queueLabel;

    public Action TrainRequested;

    public Hud(Node parent, string mapName)
    {
        var layer = new CanvasLayer { Name = "Hud" };
        parent.AddChild(layer);

        var topBar = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.06f, 0.85f),
            Position = new Vector2(0f, 0f),
            Size = new Vector2(1600f, 44f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(topBar);

        _title = Label(mapName, 18, new Vector2(16f, 10f), Colors.White);
        layer.AddChild(_title);

        _resources = Label("金币 500    木材 200    人口 0/0", 16, new Vector2(1180f, 12f), Color.FromHtml("ffd75e"));
        layer.AddChild(_resources);

        var bottomBar = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.06f, 0.85f),
            Position = new Vector2(0f, 820f),
            Size = new Vector2(1600f, 80f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(bottomBar);

        _selection = Label("未选中单位", 17, new Vector2(20f, 826f), Colors.White);
        layer.AddChild(_selection);

        _hint = Label("左键选中  右键移动/攻击/采金/伐木  农民可建造  中键拖拽  滚轮缩放  WASD 平移  ESC 返回",
            14, new Vector2(20f, 862f), new Color(0.7f, 0.72f, 0.75f));
        layer.AddChild(_hint);

        _queueLabel = Label("", 14, new Vector2(1150f, 890f), new Color(0.85f, 0.85f, 0.85f));
        layer.AddChild(_queueLabel);

        _trainButton = new Button
        {
            Position = new Vector2(1380f, 824f),
            Size = new Vector2(200f, 64f),
            Visible = false,
        };
        _trainButton.AddThemeFontSizeOverride("font_size", 17);
        _trainButton.Pressed += () => TrainRequested?.Invoke();
        layer.AddChild(_trainButton);
    }

    public void SetResources(int gold, int lumber, int pop, int cap)
    {
        _resources.Text = $"金币 {gold}    木材 {lumber}    人口 {pop}/{cap}";
    }

    public void ShowTrain(string text, bool enabled)
    {
        _trainButton.Text = text;
        _trainButton.Disabled = !enabled;
        _trainButton.Visible = true;
        TrainRequested = null;
    }

    public void ShowTrain(string text, bool enabled, Action onClick)
    {
        ShowTrain(text, enabled);
        TrainRequested = onClick;
    }

    public void HideTrain()
    {
        _trainButton.Visible = false;
        TrainRequested = null;
    }

    public void SetQueue(string text)
    {
        _queueLabel.Text = text;
    }

    private static Label Label(string text, int size, Vector2 position, Color color)
    {
        var label = new Label
        {
            Text = text,
            Position = position,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    public void SetSelection(IReadOnlyList<UnitState> selected)
    {
        if (selected == null || selected.Count == 0)
        {
            _selection.Text = "未选中单位";
            return;
        }
        if (selected.Count == 1)
        {
            UnitState unit = selected[0];
            _selection.Text = $"{unit.Type.Name}    HP {Mathf.CeilToInt(Mathf.Max(0f, unit.Hp))}/{Mathf.CeilToInt(unit.MaxHp)}"
                + $"    攻击 {unit.Type.MinDamage:0}-{unit.Type.MaxDamage:0}    护甲 -    速度 {unit.Type.Speed:0}";
            return;
        }

        int count = selected.Count;
        float hp = 0f;
        float max = 0f;
        foreach (UnitState unit in selected)
        {
            hp += Mathf.Max(0f, unit.Hp);
            max += unit.MaxHp;
        }
        _selection.Text = $"已选中 {count} 个单位    HP {hp:0}/{max:0}";
    }

    public void SetHint(string text)
    {
        _hint.Text = text;
    }
}