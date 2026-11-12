using Godot;

namespace Launcher;

public sealed class Ultravision : Ability
{
    public override bool IsPassive => true;
    public override bool NightSight => true;
    public override float PassiveSightBonus => Data('A');
}
