using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeUltravisionAbility : Ability
{
    public override bool IsPassive => true;
    public override bool NightSight => true;
    public override float PassiveSightBonus => Data('A');
}
