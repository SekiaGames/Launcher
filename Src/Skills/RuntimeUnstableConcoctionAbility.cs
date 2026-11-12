using Godot;

namespace OpenWC3;

public sealed class RuntimeUnstableConcoctionAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('B');
    public float Radius => Data('C') > 0f ? Data('C') : 200f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        foreach (Unit victim in world.EnemiesInRange(Owner, target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage, AttackType.Spells, DamageType.Magic));
        }
        world.KillUnit(Owner);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;

        int nearby = 0;
        foreach (Unit other in world.EnemiesInRange(Owner, target.Position, Radius))
        {
            if (other.IsBuilding) continue;
            nearby++;
        }
        if (nearby < 2) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
