using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeLifeRegenerationAuraAbility : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.HpRegenPct = Mathf.Max(bonus.HpRegenPct, Data('A'));
    }
}
