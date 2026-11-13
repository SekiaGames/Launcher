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
    private MinimapFrame _minimap;
    private HeroBarFrame _heroBar;
    private int _gold, _lumber, _food, _foodCap;
    private double _timeOfDay;
    private CommandCardView _commands = CommandCardView.Empty;
    private readonly CommandEntry[] _slotEntries = new CommandEntry[CommandCardBuilder.SlotCount];
    private bool _uiPointerDown;

    public int FrameCount => _root?.Children.Count ?? 0;
    internal MinimapFrame Minimap => _minimap;
    internal HeroBarFrame HeroBar => _heroBar;

    public void Load(string race = "Human", bool sample = true)
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
        _console = new ConsoleSkinFrame(resolver, skin);
        _topBar = new TopBarFrame(resolver, skin);
        _card = new CommandCardBuilder(resolver);
        _infoPanel = new UnitInfoPanelFrame(resolver, skin);
        _minimap = new MinimapFrame(resolver, skin, new War3MiscData());
        _heroBar = new HeroBarFrame(resolver);
        _root.AddChild(_console);
        _root.AddChild(_infoPanel);
        _root.AddChild(_card.Root);
        _root.AddChild(_topBar);
        _root.AddChild(_minimap);
        _root.AddChild(_heroBar);

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
        QueueRedraw();
    }

    public void SetUnitInfo(UnitInfoView? view)
    {
        _infoPanel.Update(view);
        QueueRedraw();
    }

    public void SetHeroes(IReadOnlyList<Unit> heroes, Unit selected)
    {
        _heroBar.Update(heroes, selected);
        QueueRedraw();
    }

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
                int index = entry.SlotIndex is >= 0 and < CommandCardBuilder.SlotCount
                    ? entry.SlotIndex
                    : next;
                while (index < CommandCardBuilder.SlotCount && slots[index] != null) index++;
                if (index >= CommandCardBuilder.SlotCount) break;
                next = index + 1;

                float cooldown = entry.CooldownTotal > 0.01f
                    ? Mathf.Clamp(entry.CooldownRemaining / entry.CooldownTotal, 0f, 1f)
                    : 0f;
                string overlay = entry.MaxCharges > 0 && entry.Charges > 0
                    ? entry.Charges.ToString()
                    : null;
                slots[index] = new CommandSlotView(
                    Icon: "", Hotkey: entry.Hotkey, Disabled: !entry.Enabled,
                    Cooldown: cooldown, Overlay: overlay,
                    Title: entry.Name, Body: entry.Tooltip,
                    IconTexture: entry.Icon == null ? null : new GodotUiTexture(entry.Icon));
                _slotEntries[index] = entry;
            }
        }
        _commands = new CommandCardView(slots);
        _card.Build(_commands);
        QueueRedraw();
    }

    private void OnCommandClicked(UiFrame frame, UiCommand command, int button)
        => SlotEntry(command.Slot)?.Action?.Invoke();

    private void OnCommandRightClicked(UiFrame frame, UiCommand command)
        => SlotEntry(command.Slot)?.RightAction?.Invoke();

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
        _card.Layout(DesignWidth, DesignHeight);
        _topBar.Layout(DesignWidth, DesignHeight);
        _minimap.Layout(DesignWidth, DesignHeight);
        _heroBar.Layout(DesignWidth, DesignHeight);

        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        _root.Render(_device);
        _device.End();
    }
}
