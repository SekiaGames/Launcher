using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed class HealingWard : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public string WardUnitId => Type?.SummonAt(Level) ?? "ohwd";
    public float Lifetime => Duration > 0f ? Duration : 30f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        return world.Summon(Owner, WardUnitId, Flatten(point), Lifetime) != null;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
