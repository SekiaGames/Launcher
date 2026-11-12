using Godot;

namespace Launcher;

public sealed class RuntimeCorporealFormAbility : Ability
{
    public override bool IsToggle => true;

    public override void OnToggle(World world, bool on)
    {
        if (Owner == null) return;

        if (on)
        {
            Owner.State.RemoveAll(StateMod.Ethereal);
            return;
        }

        if (!Owner.State.Ethereal) Owner.State.Add(StateMod.Ethereal);
    }
}
