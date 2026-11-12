using Godot;

namespace Launcher;

public sealed class RuntimeTauntAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float Radius => Area > 0f ? Area : 450f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.IsBuilding) continue;
            enemy.Target = Owner;
            enemy.MoveTarget = null;
            enemy.Path = null;
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        int nearby = 0;
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.IsBuilding || enemy.Target == Owner) continue;
            nearby++;
        }
        if (nearby < 2) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
