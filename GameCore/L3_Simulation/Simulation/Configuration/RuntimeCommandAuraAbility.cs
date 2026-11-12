using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeCommandAuraAbility : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.DamagePct = Mathf.Max(bonus.DamagePct, Data('A'));
    }
}
