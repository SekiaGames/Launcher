using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 死亡腐烂 AUdd（巫妖终极）。点地面引导 35 秒，按目标最大生命的百分比持续掉血。
/// 物编：Cool=150 Cost=250 Rng=1000 Area=300/400 Dur=35 DataA=0.04（单位每秒 4%）DataB=1（建筑每秒 1%）。
/// 客户端没有这份实现，字段语义按物编「百分比」用法取（DataA 是 0.04 而不是 4）。
/// </summary>
public sealed class RuntimeDeathAndDecayAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override bool IsChanneled => true;

    /// <summary>单位每秒损失的最大生命比例。</summary>
    public float UnitFraction => Data('A');
    /// <summary>建筑每秒损失的百分比（物编里是 1，即 1%）。</summary>
    public float BuildingPercent => Data('B');
    public override float ChannelWaveInterval => 0.5f;
    public float Radius => Area > 0f ? Area : 300f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 1000f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        float duration = Duration > 0f ? Duration : 35f;
        return world.BeginChannel(Owner, this, Flatten(point), duration, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            float fraction = victim.IsBuilding ? BuildingPercent / 100f : UnitFraction;
            float damage = victim.MaxHp * fraction * ChannelWaveInterval;
            if (damage <= 0f) continue;

            // 百分比伤害用 Universal：不受护甲与类型矩阵影响，只看免疫
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, damage, AttackType.Spells, DamageType.Universal));
        }
    }
}
