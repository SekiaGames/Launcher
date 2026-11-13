using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game.UI.Views;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class GameHudScreen : Control
{
    public const float DesignWidth = 1024f;
    public const float DesignHeight = 768f;

    private UiRoot _root;
    private GodotUiRenderDevice _device;
    private GodotWar3FontFace _font;
    private ConsoleSkinFrame _console;
    private TopBarFrame _topBar;
    private CommandCardBuilder _card;
    private UnitInfoPanelFrame _infoPanel;
    private int _gold, _lumber, _food, _foodCap;
    private double _timeOfDay;
    private CommandCardView _commands = CommandCardView.Empty;

    public int FrameCount => _root?.Children.Count ?? 0;

    public void Load(string race = "Human", bool sample = true)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        War3UiSkin skin = War3UiSkin.Load(race);
        _font = GodotWar3FontFace.Load(skin);
        var resolver = new GodotUiTextureResolver { Skin = skin };
        _device = new GodotUiRenderDevice(this, _font.Font)
        {
            LineSizeResolver = _font.PixelSizeForLine,
        };

        _root = new UiRoot();
        _console = new ConsoleSkinFrame(resolver, skin);
        _topBar = new TopBarFrame(resolver, skin);
        _card = new CommandCardBuilder(resolver);
        _infoPanel = new UnitInfoPanelFrame(resolver, skin);
        _root.AddChild(_console);
        _root.AddChild(_infoPanel);
        _root.AddChild(_card.Root);
        _root.AddChild(_topBar);

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

    public void SetCommands(IReadOnlyList<CommandEntry> commands)
    {
        var slots = new CommandSlotView?[CommandCardBuilder.SlotCount];
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
            }
        }
        _commands = new CommandCardView(slots);
        _card.Build(_commands);
        QueueRedraw();
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
        float scaleX = viewport.X / DesignWidth;
        float scaleY = viewport.Y / DesignHeight;

        _root.UpdateViewport(DesignWidth, DesignHeight);
        _console.Layout(DesignWidth, DesignHeight);
        _infoPanel.Layout(DesignWidth, DesignHeight);
        _card.Layout(DesignWidth, DesignHeight);
        _topBar.Layout(DesignWidth, DesignHeight);

        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        _root.Render(_device);
        _device.End();
    }
}
