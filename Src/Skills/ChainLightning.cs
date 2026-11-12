using Godot;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 闪电链 AOcl。对标客户端 Src/Skills/ChainLightning.cs：
/// DataA=首跳伤害、DataB=目标数、DataC=每跳衰减、Area=跳跃范围。
/// 物编：Cool=9 Cost=120 Rng=700 Area=500 DataA=85/125/180 DataB=4/6/8 DataC=0.15。
/// 跳跃是「从上一个受害者出发找最近的没打过的敌人」，不是从施法者重新索敌。
/// </summary>
public sealed class ChainLightning : Ability
{
    /// <summary>连锁闪电的默认目标掩码（客户端 ChainLightning.AllowedTargets）。</summary>
    public const TargetFlags AllowedTargets = TargetFlags.Ground | TargetFlags.Air
        | TargetFlags.Enemy | TargetFlags.Alive;

    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('A');
    public int TargetCount => Mathf.Max(1, Mathf.RoundToInt(Data('B')));
    public float DamageReduction => Mathf.Clamp(Data('C'), 0f, 1f);
    public float JumpRange => Area > 0f ? Area : 500f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        var hit = new List<Unit>();
        var chain = new List<Unit>();
        Unit current = target;
        float amount = Damage;

        for (int jump = 0; jump < TargetCount; jump++)
        {
            if (current == null || !current.Alive) break;

            hit.Add(current);
            chain.Add(current);
            world.DealDamage(World.MakeAbilityDamage(Owner, current, amount, AttackType.Spells, DamageType.Magic));

            amount *= 1f - DamageReduction;
            if (jump + 1 >= TargetCount) break;

            current = NearestNext(world, hit, chain[chain.Count - 1].Position);
        }

        LastChain = chain;
        LastChainDamage = Damage;
        return chain.Count > 0;
    }

    /// <summary>最近一跳的落点链，探针与场景表现用。</summary>
    public List<Unit> LastChain = new();
    public float LastChainDamage;

    /// <summary>离 anchor 最近、且还没被这次施法打过的敌人。</summary>
    private Unit NearestNext(World world, List<Unit> hit, Vector3 anchor)
    {
        Unit best = null;
        float bestDistance = JumpRange;

        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive) continue;
            if (ReferenceEquals(other, Owner) || hit.Contains(other)) continue;
            // AOcl 的 targs 是 air,ground（不含 structure）：建筑与金矿不吃跳
            if (other.IsBuilding) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (other.State.MagicImmune || other.State.Invulnerable) continue;

            float dx = other.Position.X - anchor.X;
            float dz = other.Position.Z - anchor.Z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz) - other.Radius;
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        return best;
    }

    /// <summary>AI：爆发中的群体伤害，目标在当前目标/射程内挑一个不是魔免的。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
