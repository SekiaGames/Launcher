using Godot;

namespace Launcher;

public sealed class SlowUnit : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float MoveSlowPct => Data('A');
    public float AttackSlowPct => Data('B');
    public bool AlwaysAutoCast => Data('C') > 0f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.IsBuilding) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        target.ApplySlow(MoveSlowPct, AttackSlowPct, seconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.IsBuilding && !other.Slowed);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
