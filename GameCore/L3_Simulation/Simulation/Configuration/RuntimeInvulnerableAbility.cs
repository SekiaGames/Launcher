using Godot;

namespace Launcher;

public sealed class RuntimeInvulnerableAbility : Ability
{
    public override bool IsPassive => true;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive) return;
        if (Owner.State.Has(StateMod.Invulnerable)) return;
        Owner.State.Add(StateMod.Invulnerable, 0.5f);
    }
}
