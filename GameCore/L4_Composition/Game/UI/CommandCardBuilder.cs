using OpenWC3.Game.UI.Views;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class CommandCardBuilder
{
    private const int Columns = 4, Rows = 3, Slots = 12;
    private const float OriginX = 790.4f, BottomOffset = 171.52f;
    private const float CellSize = 49.92f, StepX = 55.552f, StepY = 56.32f;

    private static readonly string[] Hotkeys =
    {
        "Q", "W", "E", "R", "A", "S", "D", "F", "Z", "X", "C", "V",
    };

    internal static string FallbackHotkey(int slot)
        => slot >= 0 && slot < Hotkeys.Length ? Hotkeys[slot] : "";

    private readonly UiContainer _card;
    private readonly ButtonFrame[] _buttons = new ButtonFrame[Slots];
    private readonly IUiTextureResolver _textures;
    private ITexture _activeHighlight;

    /// <summary>命令卡的翻页栈（客户端 NativeCommandMenu&lt;MenuPage&gt; 的同口径）：学习页 / 建造页。</summary>
    private readonly System.Collections.Generic.List<MenuPage> _menus = new();
    private Unit _menuUnit;

    private enum MenuKind { Learn, Build }

    private sealed record MenuPage(MenuKind Kind);

    private MenuPage CurrentMenu => _menus.Count > 0 ? _menus[^1] : null;

    /// <summary>当前命令卡显示的是不是英雄「学习技能」页（该页用 Researchbuttonpos 排位）。</summary>
    public bool LearnMenuOpen => CurrentMenu?.Kind == MenuKind.Learn;

    public bool BuildMenuOpen => CurrentMenu?.Kind == MenuKind.Build;

    public UiContainer Root => _card;

    public CommandCardBuilder(IUiTextureResolver textures)
    {
        _textures = textures;
        _card = new UiContainer("CommandCard");
        for (int i = 0; i < Slots; i++)
        {
            int col = i % Columns, row = i / Columns;
            var button = new ButtonFrame($"CmdSlot{i}")
            {
                Width = CellSize,
                Height = CellSize,
                RenderBounds = new UiRect(OriginX + col * StepX,
                    War3UiViewport.BaseHeight - BottomOffset + row * StepY, CellSize, CellSize),
                Hotkey = Hotkeys[i],
                Visible = false,
            };
            _buttons[i] = button;
            _card.AddChild(button);
        }
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float originX = (viewportWidth - War3UiViewport.BaseWidth) * .5f + OriginX;
        float originY = viewportHeight - BottomOffset;
        for (int i = 0; i < Slots; i++)
        {
            int col = i % Columns, row = i / Columns;
            _buttons[i].RenderBounds = new UiRect(
                originX + col * StepX, originY + row * StepY, CellSize, CellSize);
        }
    }

    public void OpenLearnMenu(Unit selected)
    {
        if (selected?.IsHero != true) return;
        UpdateMenuSelection(selected);
        _menus.Clear();
        _menus.Add(new MenuPage(MenuKind.Learn));
    }

    public void CloseLearnMenu()
    {
        if (LearnMenuOpen) _menus.RemoveAt(_menus.Count - 1);
    }

    public void OpenBuildMenu(Unit selected)
    {
        if (selected == null) return;
        UpdateMenuSelection(selected);
        _menus.Clear();
        _menus.Add(new MenuPage(MenuKind.Build));
    }

    public void CloseBuildMenu()
    {
        if (BuildMenuOpen) _menus.RemoveAt(_menus.Count - 1);
    }

    public void BackMenu()
    {
        if (_menus.Count > 0) _menus.RemoveAt(_menus.Count - 1);
    }

    /// <summary>换选中单位 = 收掉翻页（客户端 CommandCardBuilder.UpdateMenuSelection）。</summary>
    public void UpdateMenuSelection(Unit selected)
    {
        if (ReferenceEquals(_menuUnit, selected)) return;
        _menus.Clear();
        _menuUnit = selected;
    }

    public void Build(CommandCardView view, Unit selected = null)
    {
        UpdateMenuSelection(selected);
        _activeHighlight ??= _textures.Resolve(
            @"UI\Widgets\Console\Human\CommandButton\human-activebutton.blp");

        for (int i = 0; i < Slots; i++)
        {
            ButtonFrame button = _buttons[i];
            CommandSlotView? slot = view?.Slots != null && i < view.Slots.Count ? view.Slots[i] : null;
            if (slot is not { } command
                || command.IconTexture == null && string.IsNullOrWhiteSpace(command.Icon))
            {
                button.Visible = false;
                button.Icon = null;
                button.DisabledIcon = null;
                button.Command = null;
                button.OverlayText = null;
                button.Cooldown = 0f;
                continue;
            }

            button.Icon = command.IconTexture ?? _textures.Resolve(command.Icon);
            button.DisabledIcon = string.IsNullOrWhiteSpace(command.Icon)
                ? null
                : _textures.Resolve(NativeCommandButtonBinding.DisabledArt(command.Icon,
                    @"ReplaceableTextures\CommandButtonsDisabled\"));
            button.ActiveHighlight = _activeHighlight;
            button.Hotkey = string.IsNullOrEmpty(command.Hotkey) ? FallbackHotkey(i) : command.Hotkey;
            button.Disabled = command.Disabled;
            button.Cooldown = command.Cooldown;
            button.OverlayText = command.Overlay;
            button.TooltipTitle = command.Title ?? "";
            button.TooltipBody = command.Body ?? "";
            button.Command = new UiCommand(UiCommandKind.Ability, Slot: i);
            button.Visible = true;
        }
    }

    public const int SlotCount = Slots;

    /// <summary>
    /// 槽位落位（Game.dll 1.27 0x6f38ccb0）：从请求格往后找空位，再往前回退到 0，满卡就丢。
    /// </summary>
    public static int FindNativeSlot(CommandSlotView?[] slots, int requested)
    {
        if ((uint)requested >= Slots || slots == null || slots.Length != Slots) return -1;
        for (int i = requested; i < Slots; i++)
            if (slots[i] == null) return i;
        for (int i = requested - 1; i >= 0; i--)
            if (slots[i] == null) return i;
        return -1;
    }

    public void SetPending(System.Func<int, bool> isPending)
    {
        for (int i = 0; i < Slots; i++)
            _buttons[i].PendingHighlight = isPending(i);
    }
}
