using Godot;
using System;
using OpenWC3.Game.UI.Views;
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

    public int FrameCount => _root?.Children.Count ?? 0;

    public void Load(string race = "Human")
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
        _root.AddChild(_console);
        _root.AddChild(_card.Root);
        _root.AddChild(_topBar);

        _topBar.UpdateResources(500, 220, 12, 24);
        _topBar.UpdateTime(6.0);
        _card.Build(SampleCommands());
        QueueRedraw();
    }

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
        _card.Layout(DesignWidth, DesignHeight);
        _topBar.Layout(DesignWidth, DesignHeight);

        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        _root.Render(_device);
        _device.End();
    }
}
