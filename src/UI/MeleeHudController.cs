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
    /// <summary>固定格位（0..11）；&lt;0 表示按顺序排。</summary>
    public int SlotIndex = -1;
    public string Hotkey = "";
    public Action Action;
    public Action RightAction;
}

public sealed class MeleeHudController
{
    private const int SlotColumns = 4;
    private const int SlotRows = 3;
    private const float SlotSize = 60f;
    private const float SlotGap = 4f;
    private const int ItemColumns = 2;
    private const int ItemRows = 3;

    private readonly Label _title;
    private readonly Label _resources;
    private readonly Label _selection;
    private readonly Label _selectionDetail;
    private readonly Label _hint;
    private readonly Label _queueLabel;
    private readonly Label _playerInfo;
    private readonly Label _clock;
    private readonly Panel _box;
    private readonly List<Button> _selSlots = new();
    private readonly List<ColorRect> _selHp = new();
    private readonly List<Unit> _selectedUnits = new();
    public Action<Unit> SelectionSlotClicked;
    private readonly List<Button> _slots = new();
    private readonly List<Label> _slotCosts = new();
    private readonly List<Label> _slotHotkeys = new();
    private readonly List<CommandEntry> _commands = new();
    private readonly List<Button> _itemSlots = new();
    private readonly List<Label> _itemCosts = new();
    private readonly List<Label> _itemHotkeys = new();
    private readonly List<CommandEntry> _items = new();
    private readonly Panel _detailPanel;
    private readonly Label _detailText;

    public Func<Unit, string> DetailProvider;
    public MinimapFrame MinimapFrame { get; }

    /// <summary>底部多选单位格（客户端那排头像格）：点一下只选中该单位。</summary>
    private void BuildSelectionSlots(Node layer)
    {
        const float size = 34f;
        const float gap = 4f;
        for (int i = 0; i < 12; i++)
        {
            int index = i;
            var button = new Button
            {
                Position = new Vector2(222f + i * (size + gap), 784f),
                Size = new Vector2(size, size),
                Visible = false,
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center,
                Flat = false,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            button.AddThemeStyleboxOverride("normal", SlotStyle(new Color(0.12f, 0.13f, 0.15f)));
            button.AddThemeStyleboxOverride("hover", SlotStyle(new Color(0.26f, 0.3f, 0.24f)));
            button.AddThemeStyleboxOverride("pressed", SlotStyle(new Color(0.32f, 0.36f, 0.28f)));
            button.Pressed += () =>
            {
                if (index < _selectedUnits.Count) SelectionSlotClicked?.Invoke(_selectedUnits[index]);
            };
            layer.AddChild(button);
            _selSlots.Add(button);

            var hp = new ColorRect
            {
                Color = new Color(0.25f, 0.85f, 0.3f),
                Position = new Vector2(button.Position.X + 1f, button.Position.Y + size - 4f),
                Size = new Vector2(size - 2f, 3f),
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            layer.AddChild(hp);
            _selHp.Add(hp);
        }
    }

    private void RefreshSelectionSlots(IReadOnlyList<Unit> selected)
    {
        _selectedUnits.Clear();
        if (selected != null) _selectedUnits.AddRange(selected);

        for (int i = 0; i < _selSlots.Count; i++)
        {
            bool show = i < _selectedUnits.Count;
            _selSlots[i].Visible = show;
            _selHp[i].Visible = show;
            if (!show) continue;

            Unit unit = _selectedUnits[i];
            UnitType type = UnitCatalog.Get(unit.Type.Id);
            _selSlots[i].Icon = type != null && !string.IsNullOrEmpty(type.IconPath)
                ? Assets.LoadTexture(type.IconPath)
                : null;

            float ratio = unit.MaxHp > 0f ? Mathf.Clamp(unit.Hp / unit.MaxHp, 0f, 1f) : 0f;
            Color color = ratio > 0.6f ? new Color(0.25f, 0.85f, 0.3f)
                : ratio > 0.3f ? new Color(0.9f, 0.8f, 0.25f)
                : new Color(0.9f, 0.25f, 0.2f);
            _selHp[i].Color = color;
            _selHp[i].Size = new Vector2((_selSlots[i].Size.X - 2f) * ratio, 3f);
        }
    }

    public void SetSelectionBox(Rect2 rect, bool visible)
    {
        _box.Visible = visible && rect.Size.X > 1f && rect.Size.Y > 1f;
        if (!_box.Visible) return;
        _box.Position = rect.Position;
        _box.Size = rect.Size;
    }

    public void SetClock(string text)
    {
        _clock.Text = text;
    }

    public void SetPlayerInfo(string text)
    {
        _playerInfo.Text = text;
    }

    public MeleeHudController(Node parent, string mapName)
    {
        var layer = new CanvasLayer { Name = "MeleeHudController" };
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

        _clock = Label("08:00", 15, new Vector2(180f, 12f), new Color(0.72f, 0.78f, 0.86f));
        layer.AddChild(_clock);

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

        _hint = Label("左键选中/框选  双击选同类  Ctrl+数字编队  右键移动/攻击/采集  Shift+右键排队  M移动 S停 H据守 A攻击 P巡逻  中键拖拽  滚轮缩放  WASD 平移  ESC 返回",
            14, new Vector2(222f, 862f), new Color(0.7f, 0.72f, 0.75f));
        layer.AddChild(_hint);

        MinimapFrame = new MinimapFrame { Name = "MinimapFrame", Position = new Vector2(14f, 682f) };
        layer.AddChild(MinimapFrame);

        BuildSelectionSlots(layer);

        _detailPanel = new Panel
        {
            Name = "CommandDetail",
            Position = new Vector2(1058f, 552f),
            Size = new Vector2(524f, 124f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _detailPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.08f, 0.92f),
            BorderColor = new Color(0.42f, 0.38f, 0.26f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });
        layer.AddChild(_detailPanel);

        _detailText = Label("", 14, new Vector2(1068f, 560f), new Color(0.86f, 0.89f, 0.94f));
        _detailText.Size = new Vector2(504f, 108f);
        _detailText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detailText.VerticalAlignment = VerticalAlignment.Top;
        layer.AddChild(_detailText);

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
        BuildItemSlots(layer);
    }

    private void BuildItemSlots(Node layer)
    {
        float gridWidth = SlotColumns * (SlotSize + SlotGap);
        float cardX = 1600f - 16f - gridWidth;
        float itemWidth = ItemColumns * (SlotSize + SlotGap);
        float originX = cardX - 14f - itemWidth;
        float originY = 690f;

        for (int row = 0; row < ItemRows; row++)
        {
            for (int column = 0; column < ItemColumns; column++)
            {
                int index = row * ItemColumns + column;
                var button = new Button
                {
                    Position = new Vector2(originX + column * (SlotSize + SlotGap), originY + row * (SlotSize + SlotGap)),
                    Size = new Vector2(SlotSize, SlotSize),
                    Visible = false,
                    ExpandIcon = true,
                    IconAlignment = HorizontalAlignment.Center,
                    Flat = false,
                };
                button.AddThemeStyleboxOverride("normal", SlotStyle(new Color(0.16f, 0.14f, 0.1f)));
                button.AddThemeStyleboxOverride("hover", SlotStyle(new Color(0.24f, 0.21f, 0.15f)));
                button.AddThemeStyleboxOverride("pressed", SlotStyle(new Color(0.3f, 0.26f, 0.18f)));
                button.AddThemeStyleboxOverride("disabled", SlotStyle(new Color(0.08f, 0.08f, 0.09f)));
                button.Pressed += () => PressItem(index);
                button.MouseEntered += () => ShowItemDetail(index);
                button.MouseExited += HideDetail;
                button.GuiInput += @event =>
                {
                    if (@event is not InputEventMouseButton mouse) return;
                    if (!mouse.Pressed || mouse.ButtonIndex != MouseButton.Right) return;
                    RightPressItem(index);
                };
                layer.AddChild(button);
                _itemSlots.Add(button);

                var cost = Label("", 11, Vector2.Zero, Color.FromHtml("ffe27a"));
                cost.Position = new Vector2(button.Position.X + 3f, button.Position.Y + SlotSize - 17f);
                cost.Size = new Vector2(SlotSize - 6f, 15f);
                cost.HorizontalAlignment = HorizontalAlignment.Center;
                cost.Visible = false;
                layer.AddChild(cost);
                _itemCosts.Add(cost);

                var hotkey = Label("", 11, Vector2.Zero, new Color(0.85f, 0.9f, 1f));
                hotkey.Position = new Vector2(button.Position.X + SlotSize - 14f, button.Position.Y + 1f);
                hotkey.Size = new Vector2(13f, 14f);
                hotkey.HorizontalAlignment = HorizontalAlignment.Right;
                hotkey.Visible = false;
                layer.AddChild(hotkey);
                _itemHotkeys.Add(hotkey);
            }
        }
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
                button.MouseEntered += () => ShowDetail(index);
                button.MouseExited += HideDetail;
                button.GuiInput += @event =>
                {
                    if (@event is not InputEventMouseButton mouse) return;
                    if (!mouse.Pressed || mouse.ButtonIndex != MouseButton.Right) return;
                    RightPress(index);
                };
                layer.AddChild(button);
                _slots.Add(button);

                var cost = Label("", 11, Vector2.Zero, Color.FromHtml("ffe27a"));
                cost.Position = new Vector2(button.Position.X + 3f, button.Position.Y + SlotSize - 17f);
                cost.Size = new Vector2(SlotSize - 6f, 15f);
                cost.HorizontalAlignment = HorizontalAlignment.Center;
                cost.Visible = false;
                layer.AddChild(cost);
                _slotCosts.Add(cost);

                var hotkey = Label("", 11, Vector2.Zero, new Color(0.85f, 0.9f, 1f));
                hotkey.Position = new Vector2(button.Position.X + SlotSize - 14f, button.Position.Y + 1f);
                hotkey.Size = new Vector2(13f, 14f);
                hotkey.HorizontalAlignment = HorizontalAlignment.Right;
                hotkey.Visible = false;
                layer.AddChild(hotkey);
                _slotHotkeys.Add(hotkey);
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

    private void ShowDetail(int index)
    {
        ShowDetailEntry(index >= 0 && index < _commands.Count ? _commands[index] : null);
    }

    private void ShowItemDetail(int index)
    {
        ShowDetailEntry(index >= 0 && index < _items.Count ? _items[index] : null);
    }

    private void ShowDetailEntry(CommandEntry entry)
    {
        if (entry == null)
        {
            HideDetail();
            return;
        }

        _detailText.Text = BuildTooltip(entry);
        _detailPanel.Visible = true;
    }

    private void HideDetail()
    {
        _detailPanel.Visible = false;
    }

    private void Press(int index)
    {
        if (index < 0 || index >= _commands.Count) return;
        CommandEntry entry = _commands[index];
        if (entry == null || !entry.Enabled || entry.Action == null) return;
        entry.Action();
    }

    private void RightPress(int index)
    {
        if (index < 0 || index >= _commands.Count) return;
        CommandEntry entry = _commands[index];
        if (entry == null || entry.RightAction == null) return;
        entry.RightAction();
    }

    private void PressItem(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        CommandEntry entry = _items[index];
        if (entry == null || !entry.Enabled || entry.Action == null) return;
        entry.Action();
    }

    private void RightPressItem(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        CommandEntry entry = _items[index];
        if (entry == null || entry.RightAction == null) return;
        entry.RightAction();
    }

    /// <summary>小键盘 1~6 用物品；返回 true 表示这一格有东西且按得动。</summary>
    public bool ActivateItemSlot(int index)
    {
        if (index < 0 || index >= _items.Count) return false;
        CommandEntry entry = _items[index];
        if (entry == null) return false;
        PressItem(index);
        return true;
    }

    /// <summary>英雄物品：原版独立 2×3 六格（不在命令卡里），序号 1~6 是左上→右下的列优先。</summary>
    public void SetInventory(IReadOnlyList<CommandEntry> items)
    {
        _items.Clear();
        for (int index = 0; index < ItemColumns * ItemRows; index++)
        {
            _items.Add(items != null && index < items.Count ? items[index] : null);
        }

        for (int index = 0; index < _itemSlots.Count; index++)
        {
            CommandEntry entry = _items[index];
            bool used = entry != null;
            _itemSlots[index].Visible = used;
            _itemCosts[index].Visible = used && !string.IsNullOrEmpty(entry.Cost);
            _itemHotkeys[index].Visible = used && !string.IsNullOrEmpty(entry.Hotkey);
            if (!used) continue;

            _itemSlots[index].Icon = entry.Icon;
            _itemSlots[index].Disabled = !entry.Enabled;
            _itemSlots[index].Modulate = entry.Enabled ? Colors.White : new Color(0.45f, 0.45f, 0.45f);
            _itemSlots[index].TooltipText = BuildTooltip(entry);
            _itemCosts[index].Text = entry.Cost ?? "";
            _itemHotkeys[index].Text = entry.Hotkey ?? "";
        }
    }

    /// <summary>
    /// 排格子：原版命令卡是 4×3 = 12 格，**左两列放技能/命令，右两列固定放物品**
    /// （index 2,3,6,7,10,11）。有固定格位的先占位，其余按顺序填剩下的格子。
    /// </summary>
    public void SetCommands(IReadOnlyList<CommandEntry> commands)
    {
        var placed = new CommandEntry[_slots.Count];
        var leftovers = new List<CommandEntry>();

        if (commands != null)
        {
            foreach (CommandEntry entry in commands)
            {
                if (entry.SlotIndex >= 0 && entry.SlotIndex < placed.Length && placed[entry.SlotIndex] == null)
                {
                    placed[entry.SlotIndex] = entry;
                }
                else
                {
                    leftovers.Add(entry);
                }
            }
        }

        int cursor = 0;
        foreach (CommandEntry entry in leftovers)
        {
            while (cursor < placed.Length && placed[cursor] != null) cursor++;
            if (cursor >= placed.Length) break;
            placed[cursor] = entry;
            cursor++;
        }

        _commands.Clear();
        for (int index = 0; index < _slots.Count; index++)
        {
            CommandEntry entry = placed[index];
            bool used = entry != null;
            _slots[index].Visible = used;
            _slotCosts[index].Visible = used && !string.IsNullOrEmpty(entry.Cost);
            _slotHotkeys[index].Visible = used && !string.IsNullOrEmpty(entry.Hotkey);
            if (!used)
            {
                _commands.Add(null);
                continue;
            }

            _commands.Add(entry);
            _slots[index].Icon = entry.Icon;
            _slots[index].Disabled = !entry.Enabled;
            _slots[index].Modulate = entry.Enabled ? Colors.White : new Color(0.45f, 0.45f, 0.45f);
            _slots[index].TooltipText = BuildTooltip(entry);
            _slotCosts[index].Text = entry.Cost ?? "";
            _slotHotkeys[index].Text = entry.Hotkey ?? "";
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
        HideDetail();
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

    public void SetSelection(IReadOnlyList<Unit> selected)
    {
        RefreshSelectionSlots(selected);
        if (selected == null || selected.Count == 0)
        {
            _selection.Text = "未选中单位";
            _selectionDetail.Text = "";
            return;
        }
        if (selected.Count == 1)
        {
            Unit unit = selected[0];
            string stage = unit.IsUnderConstruction ? "  建造中" : "";
            _selection.Text = $"{unit.Type.Name}{stage}    HP {Mathf.CeilToInt(Mathf.Max(0f, unit.Hp))}/{Mathf.CeilToInt(unit.MaxHp)}";
            _selectionDetail.Text = DetailProvider != null ? DetailProvider(unit) : "";
            return;
        }

        int count = selected.Count;
        float hp = 0f;
        float max = 0f;
        foreach (Unit unit in selected)
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
