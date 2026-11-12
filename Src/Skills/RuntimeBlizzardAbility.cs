using Godot;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 暴风雪 AHbz。对标客户端 Src/Skills/Bloodstorm.cs（引导 + 按波 AoE 结算）：
/// 点地面 → 引导 N 波，每波对半径内敌人结算伤害，引导中不能移动。
/// 物编：Cool=6 Cost=75 Rng=800 Area=200 DataA=波数(6/8/10) DataB=每波伤害(30/40/50) DataD=波间隔(0.5)。
/// </summary>
public sealed class RuntimeBlizzardAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override bool IsChanneled => true;

    public int Waves => Mathf.Max(1, Mathf.RoundToInt(Data('A')));
    public float WaveDamage => Data('B');
    /// <summary>物编 Cast 列是每波间隔（客户端 RuntimeAbilityFactory.cs:540 读 "Cast"）；原实现误用 DataD。</summary>
    public float WaveInterval => CastInterval > 0f ? CastInterval : 0.5f;
    /// <summary>DataD 是建筑减伤比例（客户端 RefreshBlizzardData 的 BuildingReduction）。</summary>
    public float BuildingReduction => Mathf.Clamp(Data('D'), 0f, 1f);
    /// <summary>DataF 是每波总伤害上限，0 = 不限（客户端 4A8640）。</summary>
    public float MaxDamagePerWave => Data('F');
    public override float ChannelWaveInterval => WaveInterval;
    public override float ChannelTotal => Waves * WaveInterval;
    public float ChannelLength => ChannelTotal;
    public float Radius => Area > 0f ? Area : 200f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 800f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelLength, WaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        List<Unit> victims = world.EnemiesInRange(Owner, point, Radius);
        float dealt = 0f;
        foreach (Unit victim in victims)
        {
            float damage = WaveDamage;
            if (victim.IsBuilding) damage *= Mathf.Max(0f, 1f - BuildingReduction);
            // DataF：每波总伤害上限，0 = 不限。
            if (MaxDamagePerWave > 0f)
            {
                if (dealt >= MaxDamagePerWave) break;
                damage = Mathf.Min(damage, MaxDamagePerWave - dealt);
            }
            dealt += damage;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, damage, AttackType.Spells, DamageType.Magic));
        }
    }
}
