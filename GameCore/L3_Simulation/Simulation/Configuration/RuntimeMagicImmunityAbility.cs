using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeMagicImmunityAbility : Ability
{
    public override bool IsPassive => true;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive) return;
        if (Owner.State.Has(StateMod.MagicImmune)) return;
        Owner.State.Add(StateMod.MagicImmune, 0.5f);
    }
}
