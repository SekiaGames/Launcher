using Godot;

namespace Launcher;

public sealed class RuntimeBurrowFormAbility : Ability
{
    public override bool IsToggle => true;

    public override void OnToggle(World world, bool on)
    {
        if (Owner == null) return;

        if (on)
        {
            Owner.State.Add(StateMod.Invisible);
            Owner.State.Add(StateMod.Disarm);
            return;
        }

        Owner.State.Remove(StateMod.Invisible);
        Owner.State.Remove(StateMod.Disarm);
    }
}
