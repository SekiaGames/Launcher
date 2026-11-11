using Godot;

namespace Launcher;

/// <summary>
/// 风暴之锤 AHtb。对标客户端 Src/Skills/ThunderBolt.cs：
/// 起手即抛出弹道（StormBoltMissile），**飞到目标身上才结算**伤害与眩晕。
/// 物编：Cool=9 Cost=75 Rng=600 DataA=100 Dur=5。
/// </summary>
public sealed class ThunderBolt : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

    public override bool UsesMissile => true;
    public override float MissileSpeed => 900f;
    public override string MissileArt =>
        @"Abilities\Spells\Human\StormBolt\StormBoltMissile.mdl";

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

        float travel = Owner.DistanceTo(target) / MissileSpeed;
        return world.ScheduleAbilityHit(Owner, this, target, travel);
    }

    /// <summary>弹道命中：魔法伤害 + 眩晕。飞行途中目标死了就不会走到这里。</summary>
    public override void OnMissileHit(World world, Unit target)
    {
        if (target == null || !target.Alive || world == null) return;
        if (target.State.MagicImmune || target.State.Invulnerable) return;

        world.DealDamage(World.MakeAbilityDamage(Owner, target, Damage, AttackType.Spells, DamageType.Magic));
        if (target.Alive) target.State.Add(StateMod.Stun, Duration);
    }
}
