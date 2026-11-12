using Godot;

namespace Launcher;

public sealed class RuntimeMoonWellReplenishAbility : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.ManaRegenPerSecond = Mathf.Max(bonus.ManaRegenPerSecond, Data('B'));
    }
}
