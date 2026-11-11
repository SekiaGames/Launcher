using Godot;

namespace Launcher;

/// <summary>
/// 睡眠 AUsl（恐惧魔王）。目标 Dur(20/40/60) 秒不能动不能打，被攻击就醒
/// （唤醒逻辑在 World.SustainAfterHit：掉血即清 Sleep 状态）。
/// 物编 Cost 随等级下降 100/75/50，Cool=6 Rng=800。
/// </summary>
public sealed class Sleep : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.IsHero) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.State.MagicImmune || target.State.Invulnerable) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive) return false;
        target.CancelAttack();
        target.MoveTarget = null;
        target.Path = null;
        target.State.Add(StateMod.Sleep, Duration);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = Owner?.Target;
        if (target == null || !target.Alive) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
