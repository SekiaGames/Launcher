using Godot;

namespace Launcher;

public sealed class AttributeModifier : Ability
{
    public override bool IsPassive => true;

    private bool _applied;

    public override void OnWorldTick(World world, float delta)
    {
        if (_applied || Owner == null) return;
        _applied = true;

        Owner.BonusStrength += Mathf.RoundToInt(Data('A'));
        Owner.BonusAgility += Mathf.RoundToInt(Data('B'));
        Owner.BonusIntelligence += Mathf.RoundToInt(Data('C'));
        world?.RefreshHeroAttributes(Owner);
    }
}
