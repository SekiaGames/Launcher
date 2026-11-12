using Godot;

namespace Launcher;

public sealed class RuntimeRejuvenationAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float TotalHeal => Data('A');
    public float TotalSeconds => Duration > 0f ? Duration : 12f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.IsBuilding) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new HealOverTimeBuff
        {
            AmountPerTick = TotalHeal / Mathf.Max(1f, TotalSeconds),
            Interval = 1f,
        }, TotalSeconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit best = null;
        float bestRatio = 0.85f;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.IsBuilding) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (!InRange(other) || other.FindBuff<HealOverTimeBuff>() != null) continue;

            float ratio = other.Hp / Mathf.Max(1f, other.MaxHp);
            if (ratio >= bestRatio) continue;
            bestRatio = ratio;
            best = other;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}
