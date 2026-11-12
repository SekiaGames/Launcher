using Godot;

namespace Launcher;

public sealed class MoonGlaive : Ability
{
    public override bool IsPassive => true;

    public int Bounces => 2 + (Owner?.TechTargetCountBonus ?? 0);
    public float Radius => 400f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null) return;

        Unit previous = info.Target;
        float ratio = 0.5f;
        for (int bounce = 0; bounce < Bounces; bounce++)
        {
            Unit next = null;
            float bestDistance = float.MaxValue;
            foreach (Unit other in world.EnemiesInRange(Owner, previous.Position, Radius))
            {
                if (other == previous || other.IsBuilding) continue;
                if (other == info.Target) continue;

                float distance = previous.DistanceTo(other);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                next = other;
            }
            if (next == null) break;

            world.DealDamage(World.MakeAbilityDamage(Owner, next, info.Amount * ratio,
                AttackType.Pierce, DamageType.Normal));
            previous = next;
            ratio *= 0.5f;
        }
    }
}
