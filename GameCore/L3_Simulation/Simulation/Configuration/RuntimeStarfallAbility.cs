using Godot;

namespace Launcher;

/// <summary>
/// 群星坠落 AEsf（月之女祭司终极）。引导 HeroDur(30) 秒，每 DataB(1.5) 秒一波，
/// 每波对 Area(1000) 内敌人造成 DataA(50) 伤害。
/// 物编：Cool=180 Cost=200。
/// </summary>
public sealed class RuntimeStarfallAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public override bool IsChanneled => true;
    public override float ChannelWaveInterval => Data('B') > 0f ? Data('B') : 1.5f;
    public override float ChannelTotal => EffectiveDuration;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Owner.Position, ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Area))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
        }
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !IsFighting) return false;
        int enemies = NearbyEnemyCount(world, Area, out bool anyHero);
        if (enemies < 3 && !anyHero) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
