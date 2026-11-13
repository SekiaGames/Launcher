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

    private readonly UiContainer _card;
    private readonly ButtonFrame[] _buttons = new ButtonFrame[Slots];
    private readonly IUiTextureResolver _textures;
    private ITexture _activeHighlight;

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

    public void Build(CommandCardView view)
    {
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
            button.Hotkey = string.IsNullOrEmpty(command.Hotkey) ? Hotkeys[i] : command.Hotkey;
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
}
