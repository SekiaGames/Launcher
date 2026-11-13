using System;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class InventoryFrame : UiContainer
{
    const int Slots = 6;
    const float CellSize = 40f, OriginX = 660.096f, OriginY = 144.512f;
    const float StepX = 51.2f, StepY = 48.832f;

    readonly ButtonFrame[] _buttons = new ButtonFrame[Slots];
    readonly StringFrame[] _charges = new StringFrame[Slots];
    readonly StringFrame _label;
    readonly Func<string, ITexture> _iconResolver;
    CommandEntry[] _entries = new CommandEntry[Slots];

    public Action<int> SlotClicked;

    public InventoryFrame(IUiTextureResolver textures) : base("Inventory")
    {
        _iconResolver = textures.Resolve;
        _label = new StringFrame("InventoryLabel")
        {
            Text = "物品栏",
            FontSize = 12f,
            Color = new Vector4(252f / 255f, 222f / 255f, 58f / 255f, 1f),
        };
        AddChild(_label);
        for (int i = 0; i < Slots; i++)
        {
            int index = i;
            var button = new ButtonFrame($"InventorySlot{i}")
            {
                IconColor = Vector4.One,
            };
            button.Clicked += _ => SlotClicked?.Invoke(index);
            var charges = new StringFrame($"InventoryCharges{i}")
            {
                FontSize = 12f,
                Alignment = TextAlignment.Center,
                VerticalAlignment = TextVerticalAlignment.Middle,
            };
            _buttons[i] = button;
            _charges[i] = charges;
            AddChild(button);
            AddChild(charges);
        }
        Visible = false;
    }

    public void Update(System.Collections.Generic.IReadOnlyList<CommandEntry> entries)
    {
        bool any = false;
        for (int i = 0; i < Slots; i++)
        {
            CommandEntry entry = entries != null && i < entries.Count ? entries[i] : null;
            _entries[i] = entry;
            bool show = entry != null;
            any |= show;
            _buttons[i].Visible = show;
            _charges[i].Visible = show && entry.Charges > 0;
            if (!show) continue;
            _buttons[i].Icon = entry.Icon != null ? new GodotUiTexture(entry.Icon) : _iconResolver(entry.Id);
            _buttons[i].Disabled = !entry.Enabled;
            _buttons[i].TooltipTitle = entry.Name;
            _buttons[i].TooltipBody = string.IsNullOrWhiteSpace(entry.Cost)
                ? entry.Tooltip ?? ""
                : (entry.Tooltip ?? "") + (string.IsNullOrEmpty(entry.Tooltip) ? "" : "\n") + entry.Cost;
            _buttons[i].TooltipCosts = null;
            _charges[i].Text = entry.Charges.ToString();
        }
        Visible = any;
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        _label.RenderBounds = new UiRect(681f, viewportHeight - 169f, 80f, 14f);
        float originY = viewportHeight - OriginY;
        for (int i = 0; i < Slots; i++)
        {
            float x = OriginX + i % 2 * StepX;
            float y = originY + i / 2 * StepY;
            _buttons[i].RenderBounds = new UiRect(x, y, CellSize, CellSize);
            _charges[i].RenderBounds = new UiRect(x + 26f, y + 26f, 14f, 14f);
        }
    }
}