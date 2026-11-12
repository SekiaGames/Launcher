using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeAlternateFormAbility : Ability
{
    public override bool IsToggle => true;

    public float ArmorBonus => Data('B');

    public override void OnToggle(World world, bool on)
    {
        if (Owner == null) return;

        if (on)
        {
            Owner.BonusArmor += ArmorBonus;
            Owner.State.Add(StateMod.Snare);
            Owner.State.Add(StateMod.Disarm);
            return;
        }

        Owner.BonusArmor -= ArmorBonus;
        Owner.State.Remove(StateMod.Snare);
        Owner.State.Remove(StateMod.Disarm);
    }
}
