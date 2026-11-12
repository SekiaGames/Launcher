using Godot;

namespace Launcher;

public sealed class RuntimeResistantSkinAbility : Ability
{
    public override bool IsPassive => true;
    public override float DebuffDurationScale => 0.5f;
}
