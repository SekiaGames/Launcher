using Godot;

namespace Launcher;

public sealed class RuntimePillageAbility : Ability
{
    public override bool IsPassive => true;

    public float Ratio => Data('A');

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (!info.Target.IsBuilding || Ratio <= 0f || info.Amount <= 0f) return;

        int gold = Mathf.Max(1, Mathf.RoundToInt(info.Amount * Ratio * 0.5f));
        world.AwardGold(Owner.Player, gold, info.Target);
    }
}
