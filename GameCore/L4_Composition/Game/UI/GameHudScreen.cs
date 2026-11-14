using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game.UI.Views;
using OpenWC3.RHI;
using OpenWC3.UI;
using NVector2 = System.Numerics.Vector2;

namespace OpenWC3.Game.UI;

public sealed partial class GameHudScreen : Control
{
    public const float DesignWidth = 1024f;
    public const float DesignHeight = 768f;
    private const float TopChromeHeight = 40.96f;
    private const float BottomChromeHeight = 225.28f;

    private UiRoot _root;
    private UiEventRouter _router;
    private GodotUiRenderDevice _device;
    private GodotWar3FontFace _font;
    private ConsoleSkinFrame _console;
    private TopBarFrame _topBar;
    private CommandCardBuilder _card;
    private UnitInfoPanelFrame _infoPanel;
    private ItemDetailFrame _itemDetail;
    private MinimapFrame _minimap;
    private HeroBarFrame _heroBar;
    private PortraitFrame _portrait;
    private SelectionGroupFrame _selectionGroup;
    private IdlePeonFrame _idlePeon;
    private PortraitViewport _portraitViewport;
    private TimeOfDayViewport _timeOfDayViewport;
    private InventoryFrame _inventory;
    private QueueIconRowFrame _queue;
    private TooltipFrame _tooltip;
    private TextTagFrame _textTag;
    private AboutDialogFrame _aboutDialog;
    private IReadOnlyList<TextTagFrame.TagView> _worldText;
    private readonly CommandEntry[] _inventoryEntries = new CommandEntry[6];
    private GodotUiTexture _portraitTexture;
    private GodotUiTexture _timeOfDayTexture;
    private Unit _selectedUnit;
    public Action<Unit> SelectionClicked;
    public Action<int> ItemDragStarted;
    public Action<int, int, NVector2> ItemDragReleased;
    public Action<Unit> IdlePeonClicked;
    public Action<Unit> PortraitFollowStarted;
    public Action PortraitFollowStopped;
    public Action MenuClicked;
    public Action<float> CameraDistanceChanged;
    public Action<float> PanSensitivityChanged;
    public Action<float> MusicVolumeChanged;
    public Action<float> SoundVolumeChanged;
    public Action<int> FrameRateLimitChanged;
    public Action<float> RenderScaleChanged;
    public Action AboutDialogClosed;
    public Action AboutDialogExitRequested;
    private int _gold, _lumber, _food, _foodCap;
    private double _timeOfDay;
    private CommandCardView _commands = CommandCardView.Empty;
    private readonly CommandEntry[] _slotEntries = new CommandEntry[CommandCardBuilder.SlotCount];
    private bool _uiPointerDown;
    private UiRect? _selectionBox;
    private UiCommandKind? _pendingKind;

    public IUiCommandListener Listener { get; set; }

    public int FrameCount => _root?.Children.Count ?? 0;
    internal MinimapFrame Minimap => _minimap;
    internal HeroBarFrame HeroBar => _heroBar;

    public void Load(string race = "Human", bool sample = true, GameUserSettings settings = null)
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Pass;

        War3UiSkin skin = War3UiSkin.Load(race);
        _font = GodotWar3FontFace.Load(skin);
        var resolver = new GodotUiTextureResolver { Skin = skin };
        _device = new GodotUiRenderDevice(this, _font.Font)
        {
            LineSizeResolver = _font.PixelSizeForLine,
        };

        _root = new UiRoot { MouseTransparent = true };
        _router = new UiEventRouter(_root);
        _router.CommandClicked += OnCommandClicked;
        _router.CommandRightClicked += OnCommandRightClicked;
        _router.CommandLongPressed += (frame, command, button) =>
        {
            if (command.Kind == UiCommandKind.UseItem)
            {
                if (_inventory.BeginDrag(command.Slot, _inventory.SlotCenter(command.Slot)))
                    ItemDragStarted?.Invoke(command.Slot);
                return;
            }
            DispatchCommand(command, 3);
        };
        _router.CommandDragMoved += (frame, command, position) =>
        {
            if (command.Kind == UiCommandKind.UseItem) _inventory.MoveDrag(position);
        };
        _router.CommandDragReleased += (frame, command, position) =>
        {
            if (command.Kind != UiCommandKind.UseItem) return;
            _inventory.EndDrag();
            ItemDragReleased?.Invoke(command.Slot, _inventory.HitSlot(position), position);
        };
        _console = new ConsoleSkinFrame(resolver, skin);
        _topBar = new TopBarFrame(resolver, skin);
        _topBar.SetChromeVisible(true);
        _card = new CommandCardBuilder(resolver);
        _infoPanel = new UnitInfoPanelFrame(resolver, skin);
        _itemDetail = new ItemDetailFrame(resolver.Resolve);
        _minimap = new MinimapFrame(resolver, skin, new War3MiscData());
        _heroBar = new HeroBarFrame(resolver);
        _portrait = new PortraitFrame();
        _selectionGroup = new SelectionGroupFrame(resolver);
        _idlePeon = new IdlePeonFrame(resolver, skin);
        _inventory = new InventoryFrame(resolver);
        _queue = new QueueIconRowFrame(resolver, skin);
        _tooltip = new TooltipFrame();
        _textTag = new TextTagFrame();
        _portraitViewport = new PortraitViewport(this);
        _portraitTexture = new GodotUiTexture(_portraitViewport.Texture);
        _timeOfDayViewport = new TimeOfDayViewport(this, skin.Get("TimeOfDayIndicator",
            @"UI\Console\Human\HumanUI-TimeIndicator.mdl"));
        _timeOfDayTexture = new GodotUiTexture(_timeOfDayViewport.Texture);
        _portrait.OnStartFollow = () => PortraitFollowStarted?.Invoke(_selectedUnit);
        _portrait.OnStopFollow = () => PortraitFollowStopped?.Invoke();
        _selectionGroup.OnUnitClick = unit => SelectionClicked?.Invoke(unit);
        _idlePeon.OnIdlePeonClick = unit => IdlePeonClicked?.Invoke(unit);
        settings ??= GameUserSettings.Load();
        _aboutDialog = new AboutDialogFrame(resolver, skin,
            settings.CameraDistanceScale, settings.PanSensitivity,
            settings.MusicVolume, settings.SoundVolume,
            settings.FrameRateLimit, settings.RenderScale,
            value => CameraDistanceChanged?.Invoke(value),
            value => PanSensitivityChanged?.Invoke(value),
            value => MusicVolumeChanged?.Invoke(value),
            value => SoundVolumeChanged?.Invoke(value),
            value => FrameRateLimitChanged?.Invoke(value),
            value => RenderScaleChanged?.Invoke(value));
        _aboutDialog.Closed += () => AboutDialogClosed?.Invoke();
        _aboutDialog.ExitToMenuRequested += () => AboutDialogExitRequested?.Invoke();
        _topBar.MenuButton.Clicked += button =>
        {
            if (button == 0) MenuClicked?.Invoke();
        };
        _root.AddChild(_console);
        _root.AddChild(_infoPanel);
        _root.AddChild(_itemDetail);
        _root.AddChild(_card.Root);
        _root.AddChild(_topBar);
        _root.AddChild(_minimap);
        _root.AddChild(_heroBar);
        _root.AddChild(_portrait);
        _root.AddChild(_selectionGroup);
        _root.AddChild(_idlePeon);
        _root.AddChild(_inventory);
        _root.AddChild(_queue);
        _root.AddChild(_tooltip);
        _root.AddChild(_aboutDialog);
        _router.HoverChanged += frame =>
        {
            if (frame is ButtonFrame button) _tooltip.Show(button);
            else _tooltip.Hide();
        };

        SetResources(500, 220, 12, 24);
        SetClock(6.0);
        if (sample)
        {
            _card.Build(SampleCommands());
            SetUnitInfo(new UnitInfoView("步兵", false, TypeLine: "人族",
                AttackText: "12-14", ArmorText: "2.5", Strength: 0));
            SetUnitInfo(new UnitInfoView("山丘之王", true, ClassLine: "等级 3 山丘之王",
                XpRatio: 0.45f, AttackText: "22-28", AttackLevel: "3",
                ArmorText: "4.2", ArmorLevel: "3", Strength: 24, Agility: 11, Intellect: 14));
        }
        else SetCommands(null);
        QueueRedraw();
    }

    public void SetResources(int gold, int lumber, int food, int foodCap)
    {
        _gold = gold;
        _lumber = lumber;
        _food = food;
        _foodCap = foodCap;
        _topBar.UpdateResources(gold, lumber, food, foodCap);
        QueueRedraw();
    }

    public void SetClock(double hours)
    {
        _timeOfDay = hours;
        _topBar.UpdateTime(hours);
        _timeOfDayViewport?.SetClock(hours);
        QueueRedraw();
    }

    public void SetUnitInfo(UnitInfoView? view)
    {
        _infoPanel.Update(view);
        QueueRedraw();
    }

    /// <summary>
    /// 世界层飘字（客户端 MeleeHudController.RenderWorldText）：只领取本帧的投影结果，
    /// 真正的绘制在 <see cref="_Draw"/> 里、所有 HUD 之前完成——数字盖在任何 UI 上都是错的。
    /// </summary>
    internal void RenderWorldText(IReadOnlyList<TextTagFrame.TagView> tags)
    {
        _worldText = tags;
        QueueRedraw();
    }

    /// <summary>选中地面物品时的详情面板；传 null 收起。</summary>
    public void SetGroundItem(ItemType type, int charges)
    {
        _itemDetail.Update(type, charges);
        QueueRedraw();
    }

    public void SetHeroes(IReadOnlyList<Unit> heroes, Unit selected)
    {
        _heroBar.Update(heroes, selected);
        QueueRedraw();
    }

    public void SetSelection(Unit selected, IReadOnlyList<Unit> selection,
        IReadOnlyList<Unit> cargo = null)
    {
        _selectedUnit = selected;
        _card.UpdateMenuSelection(selected);
        _portrait.Update(selected);
        _portraitViewport.Show(selected);
        bool cargoMode = cargo != null && cargo.Count > 0;
        _selectionGroup.Update(selected, cargoMode ? cargo : selection, cargoMode);
        QueueRedraw();
    }

    public bool LearnMenuOpen => _card.LearnMenuOpen;

    public bool AboutDialogVisible => _aboutDialog?.Visible == true;

    public void ShowAboutDialog() => _aboutDialog?.Show();

    public void HideAboutDialog() => _aboutDialog?.Close();

    public void ToggleAboutDialog() => _aboutDialog?.Toggle();

    public bool BuildMenuOpen => _card.BuildMenuOpen;

    public void OpenLearnMenu(Unit selected) => _card.OpenLearnMenu(selected);

    public void CloseLearnMenu() => _card.CloseLearnMenu();

    public void OpenBuildMenu(Unit selected) => _card.OpenBuildMenu(selected);

    public void CloseBuildMenu() => _card.CloseBuildMenu();

    public void BackMenu() => _card.BackMenu();

    /// <summary>调试用：等价于左键点某一格命令卡（走真实点击链路）。</summary>
    public void ClickCommandSlot(int slot) => InvokeEntry(SlotEntry(slot));

    public CommandEntry CommandAtHotkey(char hotkey)
    {
        for (int column = 0; column < 4; column++)
            for (int row = 0; row < 3; row++)
            {
                int slot = row * 4 + column;
                CommandEntry entry = _slotEntries[slot];
                if (entry == null || !entry.Enabled) continue;
                string key = string.IsNullOrWhiteSpace(entry.Hotkey)
                    ? CommandCardBuilder.FallbackHotkey(slot) : entry.Hotkey;
                if (key.Length != 1 || char.ToUpperInvariant(key[0]) != hotkey) continue;
                return entry;
            }
        return null;
    }

    /// <summary>命令卡当前 12 格的占用情况（调试用：格号:名字）。</summary>
    public string DescribeCommandSlots()
    {
        var parts = new List<string>();
        for (int i = 0; i < _slotEntries.Length; i++)
        {
            CommandEntry entry = _slotEntries[i];
            if (entry == null) continue;
            parts.Add($"{i}:{entry.Name}{(entry.Enabled ? "" : "(禁用)")}"
                + (string.IsNullOrEmpty(entry.Overlay) ? "" : $"[{entry.Overlay}]"));
        }
        return string.Join(" ", parts);
    }

    public void SetInventory(IReadOnlyList<CommandEntry> entries)
    {
        for (int i = 0; i < _inventoryEntries.Length; i++)
            _inventoryEntries[i] = entries != null && i < entries.Count ? entries[i] : null;
        _inventory.Update(entries);
        QueueRedraw();
    }

    public void SetQueueSlots(IReadOnlyList<QueueEntry> entries)
    {
        _queue.Update(entries);
        QueueRedraw();
    }

    private void InvokeEntry(CommandEntry entry)
    {
        if (entry == null) return;
        if (Listener != null)
        {
            Listener.OnUiCommand(new UiCommand(entry.Kind, entry.Payload,
                Context: entry, NativeEventKind: 1));
            return;
        }
        entry.Action?.Invoke();
    }

    public void UpdateIdleWorkers(IReadOnlyList<Unit> workers, int localPlayerId, float time)
    {
        _idlePeon.Update(workers, localPlayerId, time);
        QueueRedraw();
    }

    public void TriggerIdlePeon() => _idlePeon.TriggerIdlePeon();

    public void SetCommands(IReadOnlyList<CommandEntry> commands)
    {
        var slots = new CommandSlotView?[CommandCardBuilder.SlotCount];
        Array.Clear(_slotEntries, 0, _slotEntries.Length);
        int next = 0;
        if (commands != null)
        {
            foreach (CommandEntry entry in commands)
            {
                if (entry == null) continue;
                int requested = entry.SlotIndex is >= 0 and < CommandCardBuilder.SlotCount
                    ? entry.SlotIndex
                    : next;
                int index = CommandCardBuilder.FindNativeSlot(slots, requested);
                if (index < 0) continue;
                next = index + 1;

                float cooldown = entry.CooldownTotal > 0.01f
                    ? Mathf.Clamp(entry.CooldownRemaining / entry.CooldownTotal, 0f, 1f)
                    : 0f;
                string overlay = !string.IsNullOrEmpty(entry.Overlay)
                    ? entry.Overlay
                    : entry.MaxCharges > 0 && entry.Charges > 0 ? entry.Charges.ToString() : null;
                slots[index] = new CommandSlotView(
                    Icon: "", Hotkey: entry.Hotkey, Disabled: !entry.Enabled,
                    Cooldown: cooldown, Overlay: overlay,
                    Title: entry.Name,
                    Body: string.IsNullOrWhiteSpace(entry.Cost)
                        ? entry.Tooltip
                        : (entry.Tooltip ?? "")
                            + (string.IsNullOrEmpty(entry.Tooltip) ? "" : "\n") + entry.Cost,
                    IconTexture: entry.Icon == null ? null : new GodotUiTexture(entry.Icon));
                _slotEntries[index] = entry;
            }
        }
        _commands = new CommandCardView(slots);
        _card.Build(_commands, _selectedUnit);
        ApplyPendingKind();
        QueueRedraw();
    }

    public void SetPendingCommand(UiCommandKind? kind)
    {
        _pendingKind = kind;
        ApplyPendingKind();
        QueueRedraw();
    }

    private void ApplyPendingKind()
    {
        _card.SetPending(slot =>
        {
            CommandEntry entry = SlotEntry(slot);
            return _pendingKind.HasValue && entry != null && entry.Kind == _pendingKind.Value;
        });
    }

    private void OnCommandClicked(UiFrame frame, UiCommand command, int button)
    {
        if (command.Kind == UiCommandKind.UseItem)
        {
            if (button == 1) DispatchItem(command.Slot, right: true);
            else DispatchItem(command.Slot, right: false);
            return;
        }
        DispatchCommand(command, button == 1 ? 2 : 1);
    }

    private void DispatchItem(int slot, bool right)
    {
        CommandEntry entry = slot >= 0 && slot < _inventoryEntries.Length ? _inventoryEntries[slot] : null;
        if (entry == null) return;
        if (!right)
        {
            InvokeEntry(entry);
            return;
        }
        if (Listener != null)
        {
            Listener.OnUiCommand(new UiCommand(entry.Kind, entry.Payload, slot, entry, NativeEventKind: 2));
            return;
        }
        entry.RightAction?.Invoke();
    }

    /// <summary>UI 设计单位 → 窗口像素（拖放落点要拿去射线拾取）。</summary>
    public Vector2 UiToScreen(NVector2 point)
    {
        Vector2 size = Size;
        if (size.X <= 0f || size.Y <= 0f) size = GetViewportRect().Size;
        return new Vector2(point.X * size.X / DesignWidth, point.Y * size.Y / DesignHeight);
    }

    private void OnCommandRightClicked(UiFrame frame, UiCommand command)
        => DispatchCommand(command, 2);

    private void DispatchCommand(UiCommand command, int eventKind)
    {
        CommandEntry entry = SlotEntry(command.Slot);
        if (entry == null) return;
        if (Listener != null)
        {
            Listener.OnUiCommand(new UiCommand(entry.Kind, entry.Payload, command.Slot,
                entry, NativeEventKind: eventKind));
            return;
        }
        if (eventKind >= 2) entry.RightAction?.Invoke();
        else entry.Action?.Invoke();
    }

    public void SetSelectionBox(Rect2 rect, bool visible)
    {
        if (!visible || rect.Size.X <= 1f || rect.Size.Y <= 1f)
        {
            _selectionBox = null;
            QueueRedraw();
            return;
        }
        Vector2 size = GetViewportRect().Size;
        var lo = new Vector2(rect.Position.X * DesignWidth / Mathf.Max(1f, size.X),
            rect.Position.Y * DesignHeight / Mathf.Max(1f, size.Y));
        var hi = new Vector2(rect.End.X * DesignWidth / Mathf.Max(1f, size.X),
            rect.End.Y * DesignHeight / Mathf.Max(1f, size.Y));
        _selectionBox = new UiRect(lo.X, lo.Y, hi.X - lo.X, hi.Y - lo.Y);
        QueueRedraw();
    }

    private CommandEntry SlotEntry(int slot)
        => slot >= 0 && slot < _slotEntries.Length ? _slotEntries[slot] : null;

    public bool HandlePointerDown(NVector2 uiPos, int button)
    {
        _router.PointerDown(uiPos, button);
        return IsPointerOverHud(uiPos);
    }

    public bool HandlePointerUp(NVector2 uiPos, int button)
    {
        bool hit = IsPointerOverHud(uiPos);
        _router.PointerUp(uiPos, button);
        return hit;
    }

    public void HandlePointerMove(NVector2 uiPos) => _router.PointerMove(uiPos);

    public bool HandleWheel(NVector2 uiPos, float delta) => _router.Wheel(uiPos, delta);

    public void ClearHover() => _router.ClearHover();

    public bool IsPointerOverHud(NVector2 uiPos)
    {
        if (uiPos.X >= 0f && uiPos.X < DesignWidth
            && (uiPos.Y <= TopChromeHeight || uiPos.Y >= DesignHeight - BottomChromeHeight))
            return true;
        UiFrame hit = _root.HitTest(uiPos);
        return hit != null && !ReferenceEquals(hit, _root);
    }

    public bool IsScreenPointOverHud(Vector2 screenPoint)
        => IsPointerOverHud(ScreenToUi(screenPoint));

    public override void _GuiInput(InputEvent @event)
    {
        if (_router == null) return;

        if (@event is InputEventMouseButton button)
        {
            NVector2 point = ScreenToUi(button.Position);
            if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (button.Pressed && HandleWheel(point,
                    button.ButtonIndex == MouseButton.WheelUp ? 1f : -1f)) AcceptEvent();
                return;
            }

            if (button.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
            int index = button.ButtonIndex == MouseButton.Right ? 1 : 0;
            if (button.Pressed)
            {
                if (HandlePointerDown(point, index))
                {
                    _uiPointerDown = true;
                    AcceptEvent();
                }
            }
            else
            {
                bool hit = HandlePointerUp(point, index);
                if (hit || _uiPointerDown)
                {
                    _uiPointerDown = false;
                    AcceptEvent();
                }
            }
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            HandlePointerMove(ScreenToUi(motion.Position));
            if (_uiPointerDown) AcceptEvent();
        }
    }

    private NVector2 ScreenToUi(Vector2 point)
    {
        Vector2 size = Size;
        if (size.X <= 0f || size.Y <= 0f) size = GetViewportRect().Size;
        if (size.X <= 0f || size.Y <= 0f) size = new Vector2(1920f, 1080f);
        return new NVector2(point.X * DesignWidth / Mathf.Max(1f, size.X),
            point.Y * DesignHeight / Mathf.Max(1f, size.Y));
    }

    public override void _Process(double delta) => QueueRedraw();

    private static CommandCardView SampleCommands()
    {
        var slots = new CommandSlotView?[CommandCardBuilder.SlotCount];
        slots[0] = new(@"ReplaceableTextures\CommandButtons\BTNMove.blp", "M", Title: "移动");
        slots[1] = new(@"ReplaceableTextures\CommandButtons\BTNStop.blp", "S", Title: "停止");
        slots[2] = new(@"ReplaceableTextures\CommandButtons\BTNHoldPosition.blp", "H", Title: "保持位置");
        slots[3] = new(@"ReplaceableTextures\CommandButtons\BTNAttack.blp", "A", Title: "攻击");
        slots[4] = new(@"ReplaceableTextures\CommandButtons\BTNPatrol.blp", "P", Title: "巡逻");
        slots[5] = new(@"ReplaceableTextures\CommandButtons\BTNHolyBolt.blp", "T",
            Cooldown: 0.45f, Overlay: "3", Title: "圣光术");
        slots[7] = new(@"ReplaceableTextures\CommandButtons\BTNBuild.blp", "B", Disabled: true, Title: "建造");
        return new CommandCardView(slots);
    }

    public override void _Draw()
    {
        if (_root == null || _device == null) return;

        Vector2 viewport = GetViewportRect().Size;
        if (Size != viewport) Size = viewport;
        float scaleX = viewport.X / DesignWidth;
        float scaleY = viewport.Y / DesignHeight;

        _root.UpdateViewport(DesignWidth, DesignHeight);
        _console.Layout(DesignWidth, DesignHeight);
        _infoPanel.Layout(DesignWidth, DesignHeight);
        _itemDetail.Layout(DesignWidth, DesignHeight);
        _card.Layout(DesignWidth, DesignHeight);
        _topBar.Layout(DesignWidth, DesignHeight);
        _minimap.Layout(DesignWidth, DesignHeight);
        _heroBar.Layout(DesignWidth, DesignHeight);
        _portrait.Layout(DesignWidth, DesignHeight);
        _selectionGroup.Layout(DesignWidth, DesignHeight);
        _idlePeon.Layout(DesignWidth, DesignHeight);
        _inventory.Layout(DesignWidth, DesignHeight);
        _queue.Layout(DesignWidth, DesignHeight);
        _tooltip.Layout(DesignWidth, DesignHeight);
        _aboutDialog.Layout(DesignWidth, DesignHeight);

        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        _textTag.Update(_worldText, DesignWidth, DesignHeight);
        if (_textTag.Visible) _textTag.Render(_device);
        if (_selectionBox is { } box)
            _device.DrawTexturedQuad(box, _device.WhiteTexture,
                new System.Numerics.Vector4(0.35f, 0.85f, 0.35f, 0.16f));
        if (_timeOfDayViewport is { Loaded: true })
            _device.DrawTexturedQuad(_timeOfDayViewport.ModelArea,
                _timeOfDayTexture, System.Numerics.Vector4.One);
        _root.Render(_device);
        if (_selectedUnit != null && _portrait.Visible)
            _device.DrawTexturedQuad(PortraitFrame.ModelArea(DesignHeight),
                _portraitTexture, System.Numerics.Vector4.One);
        _device.End();
    }
}
