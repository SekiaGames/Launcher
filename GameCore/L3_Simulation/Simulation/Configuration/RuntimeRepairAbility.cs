using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeRepairAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float RateFactor => Data('A') > 0f ? Data('A') : 0.35f;
    public float GoldPerHitPoint => Data('C') > 0f ? Data('C') : 0.15f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!target.IsBuilding) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Hp >= target.MaxHp) return false;
        if (target.State.Has(StateMod.RepairDisabled)) return false;
        return InRange(target);
    }

    public float RepairPerSecond(Unit target)
    {
        float build = target != null && target.BuildTotal > 0f ? target.BuildTotal : 30f;
        return target.MaxHp / build * RateFactor * 3f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        Owner.RepairTarget = target;
        Owner.Repairing = false;
        Owner.RepairGoldDebt = 0f;
        Owner.BuildSite = null;
        Owner.Tree = null;
        Owner.GoldMine = null;
        Owner.Target = null;
        Owner.MoveTarget = null;
        Owner.Path = null;
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit best = null;
        float lowest = 1f;
        float range = CastRange > 0f ? CastRange : 800f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, range))
        {
            if (!ally.Alive || !ally.IsBuilding || ally.Hp >= ally.MaxHp) continue;
            if (ally.State.Has(StateMod.RepairDisabled)) continue;

            float ratio = ally.Hp / Mathf.Max(1f, ally.MaxHp);
            if (ratio >= lowest) continue;
            lowest = ratio;
            best = ally;
        }
        return best != null && world.TryCast(Owner, this, best, best.Position);
    }
}
