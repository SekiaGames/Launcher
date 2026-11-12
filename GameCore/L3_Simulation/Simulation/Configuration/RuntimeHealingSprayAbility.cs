using Godot;

namespace Launcher;

public sealed class RuntimeHealingSprayAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 250f;
    public float HealAmount => Data('D');

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive || HealAmount <= 0f) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Flatten(point), Radius))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            ally.Heal(HealAmount);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit wounded = null;
        float lowest = 0.9f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 800f))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            float ratio = ally.Hp / Mathf.Max(1f, ally.MaxHp);
            if (ratio >= lowest) continue;
            lowest = ratio;
            wounded = ally;
        }
        return wounded != null && world.TryCast(Owner, this, null, wounded.Position);
    }
}
