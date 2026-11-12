using Godot;

namespace Launcher;

public sealed class StormHammers : Ability
{
    public override bool IsPassive => true;

    public float Ratio => 0.5f;
    public float Radius => 500f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || info.Amount <= 0f) return;

        Unit second = null;
        float best = float.MaxValue;
        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target || victim.IsBuilding) continue;
            float distance = info.Target.DistanceTo(victim);
            if (distance >= best) continue;
            best = distance;
            second = victim;
        }
        if (second == null) return;

        world.DealDamage(World.MakeAbilityDamage(Owner, second, info.Amount * Ratio,
            AttackType.Spells, DamageType.Magic));
    }
}
