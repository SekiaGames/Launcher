using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 龙卷风自身伤害（Atdg）：每秒一波，取「包含目标且半径最小」的那一圈结算，
/// 多圈不叠加（客户端 RuntimeTornadoDamageAbility 同口径）。
/// </summary>
public sealed class RuntimeTornadoDamageAbility : Ability
{
    public override bool IsPassive => true;

    private float _pulseTimer = 1f;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        _pulseTimer -= delta;
        if (_pulseTimer > 0f) return;
        _pulseTimer += 1f;

        float outerRadius = Area > 0f ? Area : 650f;
        float outerDamage = Data('A');
        float innerRadius = Data('B');
        float innerDamage = Data('C');

        float query = Mathf.Max(outerRadius, innerRadius);
        if (query <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, query))
        {
            // 物编 targs = structure,enemy：龙卷风只磨建筑，不特殊处理单位
            if (!victim.IsBuilding) continue;

            float distance = FlatDistance(Owner.Position, victim.Position);
            float bestRadius = float.MaxValue;
            float amount = 0f;

            PickRing(outerRadius, outerDamage, distance, ref bestRadius, ref amount);
            PickRing(innerRadius, innerDamage, distance, ref bestRadius, ref amount);
            if (amount <= 0f) continue;

            world.DealDamage(World.MakeAbilityDamage(Owner, victim, amount, AttackType.Spells, DamageType.Magic));
        }
    }

    private static void PickRing(float radius, float damage, float distance, ref float bestRadius, ref float amount)
    {
        if (radius <= 0f || damage <= 0f || distance > radius || radius >= bestRadius) return;
        bestRadius = radius;
        amount = damage;
    }
}
