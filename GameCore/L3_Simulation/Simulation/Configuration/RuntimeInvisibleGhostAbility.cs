using Godot;

namespace Launcher;

public sealed class RuntimeInvisibleGhostAbility : Ability
{
    public override bool IsPassive => true;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive) return;
        if (Owner.State.Invisible) return;
        Owner.State.Add(StateMod.Invisible, 0.5f);
    }
}
