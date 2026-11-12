using Godot;

namespace Launcher;

/// <summary>
/// 腐臭蜂群 AUcs（恐惧魔王）。朝落点放一条直线，Area(100) 宽的带子上敌人吃 DataA 伤害。
/// 物编：Cool=10 Cost=110 Rng=700 Area=100 DataA=75/125/200 DataC=800（直线长度）。
/// </summary>
public sealed class RuntimeCarrionSwarmAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float distance = FlatDistance(Owner.Position, point);
        return distance <= Mathf.Max(1f, CastRange) + 120f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 from = Owner.Position;
        Vector3 landing = Flatten(point);
        float length = Data('C') > 0f ? Data('C') : 800f;
        float distance = FlatDistance(from, landing);
        Vector3 direction = distance < 1f
            ? new Vector3(Mathf.Sin(Owner.Facing), 0f, Mathf.Cos(Owner.Facing))
            : new Vector3(landing.X - from.X, 0f, landing.Z - from.Z) / distance;

        Vector3 end = from + direction * Mathf.Min(length, Mathf.Max(240f, distance));
        foreach (Unit victim in world.EnemiesAlongLine(Owner, from, end, Area))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
        }
        return true;
    }

    /// <summary>AI：爆发，朝当前目标/射程内最近的敌人发一束。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
