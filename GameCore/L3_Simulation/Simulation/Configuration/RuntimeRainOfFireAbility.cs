using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 火焰雨 ANrf（深渊魔王）。对标 Wiki RuntimeRainOfFireAbility：
/// 点地面引导 DataA(6/8/10) 波、每波间隔 Cast(1s)，每波 Area(200) 内吃 DataB(25/30/35)，
/// 建筑乘 DataD(0.5)；中招的还会挂 DataE(5/10/15)/s 的灼烧，持续 Dur(3s)。
/// 物编 Cool=8 Cost=85 Rng=800。
/// </summary>
public sealed class RuntimeRainOfFireAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public int Waves => Mathf.Max(1, Mathf.RoundToInt(Data('A')));
    public float WaveDamage => Data('B');
    public float WaveInterval => CastInterval > 0f ? CastInterval : 1f;
    public float BuildingFactor => Data('D') > 0f ? Data('D') : 0.5f;
    public float BurnPerSecond => Data('E');
    public float BurnSeconds => Duration > 0f ? Duration : 3f;
    public float Radius => Area > 0f ? Area : 200f;

    public override float ChannelWaveInterval => WaveInterval;
    public override float ChannelTotal => Waves * WaveInterval;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 800f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelTotal, WaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            float amount = WaveDamage * (victim.IsBuilding ? BuildingFactor : 1f);
            if (amount > 0f)
            {
                world.DealDamage(World.MakeAbilityDamage(Owner, victim, amount, AttackType.Spells, DamageType.Magic));
            }
            if (BurnPerSecond > 0f) ApplyBurn(world, victim);
        }
    }

    private void ApplyBurn(World world, Unit victim)
    {
        for (int index = 0; index < victim.Buffs.Count; index++)
        {
            if (victim.Buffs[index] is not PeriodicDamageBuff burn) continue;
            if (!ReferenceEquals(burn.SourceAbility, this)) continue;
            burn.Remaining = Mathf.Max(burn.Remaining, BurnSeconds);
            return;
        }

        victim.Buffs.Add(new PeriodicDamageBuff
        {
            Owner = victim,
            SourceAbility = this,
            Name = Name,
            Remaining = BurnSeconds,
            DamageSource = Owner,
            AmountPerTick = BurnPerSecond,
            Interval = 1f,
            AttackType = AttackType.Spells,
            DamageType = DamageType.Magic,
        });
    }

    /// <summary>AI：朝敌人最密的地方砸（点目标引导，敌人走了也照打落点）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, AiTactics.LeadPoint(target));
    }
}
