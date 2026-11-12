using Godot;

namespace Launcher;

public sealed class RuntimeUnholyFrenzyAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float AttackSpeedPct => Data('A');
    public float DrainPerSecond => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.FindBuff<RuntimeUnholyFrenzyBuff>() != null) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new RuntimeUnholyFrenzyBuff
        {
            AttackSpeedPct = AttackSpeedPct,
            DrainPerSecond = DrainPerSecond,
            DamageSource = Owner,
        }, Duration > 0f ? Duration : 45f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.IsBuilding || !ally.Alive || ally.IsHero) continue;
            if (ally.FindBuff<RuntimeUnholyFrenzyBuff>() != null) continue;
            return world.TryCast(Owner, this, ally, ally.Position);
        }
        return false;
    }
}
