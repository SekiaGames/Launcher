using Godot;

namespace Launcher;

/// <summary>
/// 地震 AOeq（先知终极）。对标 Wiki RuntimeEarthquakeAbility：
/// 引导 Dur(25) 秒，每秒结算一次——半径内建筑吃 DataB(50) 固定伤害，
/// 地面单位吃 DataC(75%) 减速 2 秒（空中不掉）。物编 Cool=90 Cost=150 Rng=1000 Area=250。
/// </summary>
public sealed class Earthquake : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public override float ChannelWaveInterval => 1f;
    public override float ChannelTotal => Duration > 0f ? Duration : 25f;

    public float MoveSlowPct => Mathf.Clamp(Data('C'), 0f, 1f);
    public float BuildingDamage => Data('B');
    public float Radius => Area > 0f ? Area : (Data('D') > 0f ? Data('D') : 250f);

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 1000f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            if (victim.IsBuilding)
            {
                if (BuildingDamage > 0f)
                {
                    world.DealDamage(World.MakeAbilityDamage(Owner, victim, BuildingDamage,
                        AttackType.Spells, DamageType.Universal));
                }
                continue;
            }

            if (victim.Type != null && victim.Type.Flying) continue;
            if (MoveSlowPct > 0f) victim.ApplySlow(MoveSlowPct, 0f, 2f);
        }
    }

    /// <summary>AI：身边有敌人（尤其是有人压塔）才放，引导 25 秒不能动。</summary>
    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 1000f)) return false;

        Unit target = FindEnemyInRange(world);
        Vector3 point = target?.Position ?? Owner.Position;
        return world.TryCast(Owner, this, null, point);
    }
}
