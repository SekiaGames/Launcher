using Godot;

namespace Launcher;

/// <summary>
/// 风暴之锤 AHtb。对标客户端 Src/Skills/ThunderBolt.cs：
/// 单体魔法伤害 DataA + 眩晕 Dur 秒。
/// 物编：Cool=9 Cost=75 Rng=600 DataA=100 Dur=5。
/// </summary>
public sealed class ThunderBolt : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

    public float Damage => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive || Owner == null) return false;
        bool enemy = world != null ? !world.AreAllied(Owner.Player, target.Player) : Owner.Player != target.Player;
        if (!enemy) return false;
        if (target.State.MagicImmune) return false;
        return InRange(target);
    }

    /// <summary>自动施法：打当前目标（在射程内且不是魔免）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = Owner?.Target;
        if (target == null || !target.Alive) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive || world == null) return false;

        world.DealDamage(World.MakeAbilityDamage(Owner, target, Damage, AttackType.Spells, DamageType.Magic));
        if (target.Alive) target.State.Add(StateMod.Stun, Duration);
        return true;
    }
}
