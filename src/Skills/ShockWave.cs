using Godot;

namespace Launcher;

/// <summary>
/// 震荡波 AOsh（牛头人酋长）。朝落点放一条 Area(125) 宽的能量波，轨迹上的地面单位与建筑吃 DataA 伤害。
/// 物编：Cool=8 Cost=100 Rng=700 Area=125 DataA=75/130/200 DataC=800（长度）。
/// </summary>
public sealed class ShockWave : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        return FlatDistance(Owner.Position, point) <= Mathf.Max(1f, CastRange) + 120f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 from = Owner.Position;
        Vector3 landing = Flatten(point);
        float distance = FlatDistance(from, landing);
        float length = Data('C') > 0f ? Data('C') : 800f;
        Vector3 direction = distance < 1f
            ? new Vector3(Mathf.Sin(Owner.Facing), 0f, Mathf.Cos(Owner.Facing))
            : new Vector3(landing.X - from.X, 0f, landing.Z - from.Z) / distance;

        Vector3 end = from + direction * Mathf.Min(length, Mathf.Max(240f, distance));
        foreach (Unit victim in world.EnemiesAlongLine(Owner, from, end, Area, groundOnly: true))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
        }
        return true;
    }

    /// <summary>AI：爆发，朝当前目标/射程内最近的敌人发一道直线。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
