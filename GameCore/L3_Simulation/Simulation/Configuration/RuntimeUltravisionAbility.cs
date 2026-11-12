using Godot;

namespace Launcher;

public sealed class RuntimeUltravisionAbility : Ability
{
    public override bool IsPassive => true;
    public override bool NightSight => true;
    public override float PassiveSightBonus => Data('A');
}
