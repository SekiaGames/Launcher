using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 碎裂闪电 ANfl（海巫）。对标 Wiki RuntimeForkedLightningAbility：
/// 以主目标方向为轴取一个扇形（起点宽 Area 125、末端宽 DataD 300、长度 DataC 900），
/// 扇形内按「离主目标近」排序取 DataB(3) 个，每个吃 DataA(85/160/250)。物编 Cool=11 Cost=110 Rng=600。
/// </summary>
public sealed class RuntimeForkedLightningAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('A');
    public int TargetCount => Mathf.Max(1, Mathf.RoundToInt(Data('B')));
    public float Length => Data('C') > 0f ? Data('C') : 900f;
    public float StartWidth => Area > 0f ? Area : 125f;
    public float EndWidth => Data('D') > 0f ? Data('D') : 300f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.State.MagicImmune) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null) return false;

        Vector3 direction = new(target.Position.X - Owner.Position.X, 0f, target.Position.Z - Owner.Position.Z);
        float length = direction.Length();
        if (length < 1f) length = 1f;
        direction /= length;
        float reach = Mathf.Max(length, Length);

        var candidates = new List<Unit> { target };
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || ReferenceEquals(other, target)) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (other.State.MagicImmune || other.State.Invulnerable) continue;

            Vector3 offset = new(other.Position.X - Owner.Position.X, 0f, other.Position.Z - Owner.Position.Z);
            float projection = offset.X * direction.X + offset.Z * direction.Z;
            if (projection < 0f || projection > reach) continue;

            float perpendicular = Mathf.Abs(offset.X * direction.Z - offset.Z * direction.X);
            float width = StartWidth + (EndWidth - StartWidth) * (reach <= 0f ? 0f : projection / reach);
            if (perpendicular > width + other.Radius) continue;
            candidates.Add(other);
        }

        candidates.Sort((first, second) =>
            FlatDistance(first.Position, target.Position).CompareTo(FlatDistance(second.Position, target.Position)));

        int hits = Mathf.Min(TargetCount, candidates.Count);
        for (int index = 0; index < hits; index++)
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, candidates[index], Damage,
                AttackType.Spells, DamageType.Magic));
        }
        return true;
    }

    /// <summary>AI：射程内有敌人就劈（优先英雄，扇形会顺带扫到旁边的人）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
