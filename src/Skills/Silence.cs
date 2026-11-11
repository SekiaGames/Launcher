using Godot;

namespace Launcher;

/// <summary>
/// 沉默 ANsi（黑暗游侠）。点地面：范围内敌方单位在 Dur 秒内不能施法。
/// 物编：Cool=15 Cost=75 Rng=900 Area=200/275/350 Dur=16/20/24。
/// 沉默状态本身是 <see cref="StateMod.Silence"/>，<c>World.TryCast</c> 与引导都会尊重它。
/// </summary>
public sealed class Silence : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;

    public float Radius => Area > 0f ? Area : 200f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 900f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 center = Flatten(point);
        float duration = Duration > 0f ? Duration : 16f;
        int silenced = 0;
        foreach (Unit victim in world.EnemiesInRange(Owner, center, Radius))
        {
            if (victim.IsBuilding) continue;
            victim.State.Add(StateMod.Silence, duration);
            silenced++;
        }

        LastSilenced = silenced;
        return true;
    }

    /// <summary>最近一次沉默到的人数，探针用。</summary>
    public int LastSilenced;
}
