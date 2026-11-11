using Godot;

namespace Launcher;

/// <summary>
/// 纠缠根须 AEer（丛林守护者）。目标 Dur(9/24/36) 秒被定身，每秒吃 DataA(15/15/25) 伤害。
/// 物编：Cool=8 Cost=75 Rng=800；定身走 StateMod.Snare，持续伤害走 PeriodicDamageBuff。
/// </summary>
public sealed class EntanglingRoots : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Type != null && target.Type.Flying) return false;
        if (target.State.MagicImmune || target.State.Invulnerable) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        target.MoveTarget = null;
        target.Path = null;
        target.State.Add(StateMod.Snare, Duration);

        target.Buffs.Add(new PeriodicDamageBuff
        {
            Owner = target,
            Source = this,
            Name = Name,
            Remaining = Duration,
            DamageSource = Owner,
            AmountPerTick = Data('A'),
            Interval = 1f,
        });
        return true;
    }

    /// <summary>AI：控制优先，优先缠敌方英雄（地面单位），英雄打不到就缠最近的。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true,
            accept: other => !other.IsBuilding && (other.Type == null || !other.Type.Flying));
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
