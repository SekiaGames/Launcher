using Godot;

namespace Launcher;

/// <summary>
/// 生命汲取 ANdr（黑暗游侠）。对标 Wiki RuntimeDrainAbility 的生命那一支：
/// 引导 Dur(8) 秒，每 DataC(1) 秒抽 DataA(25/40/55) 点生命，实际抽到多少就回自己多少。
/// 物编 Cool=8 Cost=75 Rng=500 Area=800（Area 是拉扯上限）。
/// </summary>
public sealed class LifeDrain : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public float DrainInterval => Data('C') > 0f ? Data('C') : 1f;
    public float DrainPerTick => Data('A');
    public float TetherRange => Area > 0f ? Area : 800f;
    public override float ChannelWaveInterval => DrainInterval;
    public override float ChannelTotal => Duration > 0f ? Duration : 8f;

    private Unit _drainTarget;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.State.MagicImmune) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null) return false;

        _drainTarget = target;
        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : ChannelTotal;
        return world.BeginChannel(Owner, this, target.Position, seconds, DrainInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        Unit target = _drainTarget;
        if (target == null || !target.Alive || FlatDistance(Owner.Position, target.Position) > TetherRange + target.Radius)
        {
            _drainTarget = null;
            world.InterruptChannel(Owner);
            return;
        }

        float before = target.Hp;
        world.DealDamage(World.MakeAbilityDamage(Owner, target, DrainPerTick, AttackType.Spells, DamageType.Magic));
        float drained = Mathf.Max(0f, before - target.Hp);
        if (drained > 0f) Owner.Heal(drained);
    }

    /// <summary>AI：挑射程内的敌人（优先英雄），吸血当半个续航技能。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}