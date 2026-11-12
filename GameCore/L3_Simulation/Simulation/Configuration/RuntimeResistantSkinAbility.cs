using Godot;

namespace Launcher;

public sealed class ResistantSkin : Ability
{
    public override bool IsPassive => true;
    public override float DebuffDurationScale => 0.5f;
}
