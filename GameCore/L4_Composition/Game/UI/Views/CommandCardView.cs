using System.Collections.Generic;
using OpenWC3.RHI;

namespace OpenWC3.Game.UI.Views;

public readonly record struct CommandSlotView(
    string Icon,
    string Hotkey,
    bool Disabled = false,
    float Cooldown = 0f,
    string Overlay = null,
    string Title = null,
    string Body = null,
    ITexture IconTexture = null,
    bool AutoCastActive = false,
    string CooldownId = null);

public sealed record CommandCardView(IReadOnlyList<CommandSlotView?> Slots)
{
    public static readonly CommandCardView Empty = new(new CommandSlotView?[12]);
}
