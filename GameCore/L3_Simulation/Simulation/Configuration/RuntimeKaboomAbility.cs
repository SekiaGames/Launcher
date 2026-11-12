using Godot;

namespace Launcher;

public sealed class SelfDestruct : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Radius => Area > 0f ? Area : 250f;
    public float Damage => Data('B');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage,
                AttackType.Spells, DamageType.Magic));
        }
        world.KillUnit(Owner);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        int nearby = 0;
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.IsBuilding) continue;
            nearby++;
        }
        if (nearby < 2) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
