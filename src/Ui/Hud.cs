using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class CommandEntry
{
    public string Id;
    public string Name;
    public string Cost;
    public string Tooltip;
    public Texture2D Icon;
    public bool Enabled = true;
    public bool Active;
    public Action Action;
}

public sealed class Hud
{
    private const int SlotColumns = 4;
    private const int SlotRows = 3;
    private const float SlotSize = 60f;
    private const float SlotGap = 4f;

    private readonly Label _title;
    private readonly Label _resources;
    private readonly Label _selection;
    private readonly Label _selectionDetail;
    private readonly Label _hint;
    private readonly Label _queueLabel;
    private readonly Label _playerInfo;
    private readonly Panel _box;
    private readonly List<Button> _slots = new();
    private readonly List<Label> _slotCosts = new();
    private readonly List<CommandEntry> _commands = new();

    public Func<UnitState, string> DetailProvider;
    public Minimap Minimap { get; }

    public void SetSelectionBox(Rect2 rect, bool visible)
    {
        _box.Visible = visible && rect.Size.X > 1f && rect.Size.Y > 1f;
        if (!_box.Visible) return;
        _box.Position = rect.Position;
        _box.Size = rect.Size;
    }

    public void SetPlayerInfo(string text)
    {
        _playerInfo.Text = text;
    }

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

        _resources = Label("金币 0    木材 0    人口 0/0", 17, new Vector2(1280f, 10f), Color.FromHtml("ffd75e"));
        layer.AddChild(_resources);

        _playerInfo = Label("", 16, new Vector2(320f, 11f), new Color(0.82f, 0.86f, 0.92f));
        layer.AddChild(_playerInfo);

        var bottomBar = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.06f, 0.88f),
            Position = new Vector2(0f, 676f),
            Size = new Vector2(1600f, 224f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(bottomBar);

        _selection = Label("未选中单位", 18, new Vector2(222f, 690f), Colors.White);
        layer.AddChild(_selection);

        _selectionDetail = Label("", 15, new Vector2(222f, 724f), new Color(0.78f, 0.8f, 0.84f));
        layer.AddChild(_selectionDetail);

        _queueLabel = Label("", 14, new Vector2(222f, 758f), new Color(0.85f, 0.85f, 0.85f));
        layer.AddChild(_queueLabel);

        _hint = Label("左键选中/框选  双击选同类  Ctrl+数字编队  右键移动/攻击/采集  中键拖拽  滚轮缩放  WASD 平移  ESC 返回",
            14, new Vector2(222f, 862f), new Color(0.7f, 0.72f, 0.75f));
        layer.AddChild(_hint);

        Minimap = new Minimap { Name = "Minimap", Position = new Vector2(14f, 682f) };
        layer.AddChild(Minimap);

        _box = new Panel { Name = "SelectionBox", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _box.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.3f, 1f, 0.4f, 0.10f),
            BorderColor = new Color(0.35f, 0.95f, 0.45f, 0.9f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });
        layer.AddChild(_box);

        BuildCommandSlots(layer);
    }

    private void BuildCommandSlots(Node layer)
    {
        float gridWidth = SlotColumns * (SlotSize + SlotGap);
        float gridX = 1600f - 16f - gridWidth;
        float gridY = 690f;

        for (int row = 0; row < SlotRows; row++)
        {
            for (int column = 0; column < SlotColumns; column++)
            {
                int index = row * SlotColumns + column;
                var button = new Button
                {
                    Position = new Vector2(gridX + column * (SlotSize + SlotGap), gridY + row * (SlotSize + SlotGap)),
                    Size = new Vector2(SlotSize, SlotSize),
                    Visible = false,
                    ExpandIcon = true,
                    IconAlignment = HorizontalAlignment.Center,
                    Flat = false,
                };
                button.AddThemeStyleboxOverride("normal", SlotStyle(new Color(0.12f, 0.13f, 0.15f)));
                button.AddThemeStyleboxOverride("hover", SlotStyle(new Color(0.2f, 0.24f, 0.2f)));
                button.AddThemeStyleboxOverride("pressed", SlotStyle(new Color(0.28f, 0.32f, 0.26f)));
                button.AddThemeStyleboxOverride("disabled", SlotStyle(new Color(0.08f, 0.08f, 0.09f)));
                button.Pressed += () => Press(index);
                layer.AddChild(button);
                _slots.Add(button);

                var cost = Label("", 11, Vector2.Zero, Color.FromHtml("ffe27a"));
                cost.Position = new Vector2(button.Position.X + 3f, button.Position.Y + SlotSize - 17f);
                cost.Size = new Vector2(SlotSize - 6f, 15f);
                cost.HorizontalAlignment = HorizontalAlignment.Center;
                cost.Visible = false;
                layer.AddChild(cost);
                _slotCosts.Add(cost);
            }
        }
    }

    private static StyleBoxFlat SlotStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = new Color(0.45f, 0.42f, 0.3f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    private void Press(int index)
    {
        if (index < 0 || index >= _commands.Count) return;
        CommandEntry entry = _commands[index];
        if (!entry.Enabled || entry.Action == null) return;
        entry.Action();
    }

    public void SetCommands(IReadOnlyList<CommandEntry> commands)
    {
        _commands.Clear();
        if (commands != null) _commands.AddRange(commands);

        for (int index = 0; index < _slots.Count; index++)
        {
            bool used = index < _commands.Count;
            _slots[index].Visible = used;
            _slotCosts[index].Visible = used && !string.IsNullOrEmpty(_commands[index].Cost);
            if (!used) continue;

            CommandEntry entry = _commands[index];
            _slots[index].Icon = entry.Icon;
            _slots[index].Disabled = !entry.Enabled;
            _slots[index].Modulate = entry.Enabled ? Colors.White : new Color(0.45f, 0.45f, 0.45f);
            _slots[index].TooltipText = BuildTooltip(entry);
            _slotCosts[index].Text = entry.Cost ?? "";
        }
    }

    private static string BuildTooltip(CommandEntry entry)
    {
        string text = entry.Name ?? "";
        if (!string.IsNullOrEmpty(entry.Tooltip)) text += "\n" + entry.Tooltip;
        if (!entry.Enabled) text += "\n（条件不足）";
        return text;
    }

    public void ClearCommands()
    {
        SetCommands(null);
    }

    public void SetResources(int gold, int lumber, int pop, int cap)
    {
        _resources.Text = $"金币 {gold}    木材 {lumber}    人口 {pop}/{cap}";
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
            _selectionDetail.Text = "";
            return;
        }
        if (selected.Count == 1)
        {
            UnitState unit = selected[0];
            string stage = unit.IsUnderConstruction ? "  建造中" : "";
            _selection.Text = $"{unit.Type.Name}{stage}    HP {Mathf.CeilToInt(Mathf.Max(0f, unit.Hp))}/{Mathf.CeilToInt(unit.MaxHp)}";
            _selectionDetail.Text = DetailProvider != null ? DetailProvider(unit) : "";
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
        _selectionDetail.Text = "";
    }

    public void SetHint(string text)
    {
        _hint.Text = text;
    }
}
