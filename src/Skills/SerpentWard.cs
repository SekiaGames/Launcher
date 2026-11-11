using Godot;

namespace Launcher;

/// <summary>
/// 毒蛇守卫 AOsw（暗影猎手）。在落点召出一座固定守卫（UnitID=osp1/osp2/osp3），持续 Dur(40) 秒。
/// 物编：Cool=6.5 Cost=30 Rng=500 DataA=1（数量）。
/// </summary>
public sealed class SerpentWard : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        return FlatDistance(Owner.Position, point) <= Mathf.Max(1f, CastRange) + 120f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 landing = Flatten(point);
        int count = Mathf.Max(1, Mathf.RoundToInt(Data('A')));
        string unitId = Type.SummonAt(Level);
        Unit ward = null;
        for (int index = 0; index < count; index++)
        {
            float angle = Mathf.Tau * index / count;
            Vector3 offset = new(Mathf.Cos(angle) * 56f, 0f, Mathf.Sin(angle) * 56f);
            ward = world.Summon(Owner, unitId, landing + offset, Duration);
        }
        return ward != null;
    }

    /// <summary>AI：把守卫插到当前目标脚下（射程内），当肉盾与火力点。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, target.Position);
    }
}
