using Godot;

namespace Launcher;

/// <summary>
/// 死亡缠绕 AUdc（死亡骑士）。对标客户端口径：亡灵友军治疗、活物敌人伤害，走弹道飞行后结算。
/// 物编：Cool=6 Cost=75 Rng=800 DataA=200/400/600（1/2/3 级）。弹道速度表里没导出，按同族锤子取 900。
/// </summary>
public sealed class DeathCoil : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

    public override bool UsesMissile => true;
    public override float MissileSpeed => 900f;
    public override string MissileArt =>
        @"Abilities\Spells\Undead\DeathCoil\DeathCoilMissile.mdl";

    public float Amount => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.State.MagicImmune || target.State.Invulnerable) return false;
        if (!InRange(target)) return false;

        bool ally = world != null ? world.AreAllied(Owner.Player, target.Player) : Owner.Player == target.Player;
        return ally ? IsUndead(target) : IsLiving(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || target == null || !target.Alive || Owner == null) return false;
        return world.ScheduleAbilityHit(Owner, this, target, Owner.DistanceTo(target) / MissileSpeed);
    }

    /// <summary>弹道命中：亡灵友军回血，活物敌人吃魔法伤害。</summary>
    public override void OnMissileHit(World world, Unit target)
    {
        if (world == null || target == null || !target.Alive) return;

        bool ally = world.AreAllied(Owner.Player, target.Player);
        if (ally && IsUndead(target))
        {
            target.Heal(Amount);
            return;
        }

        world.DealDamage(World.MakeAbilityDamage(Owner, target, Amount, AttackType.Spells, DamageType.Magic));
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = Owner?.Target;
        if (target == null || !target.Alive || !IsLiving(target)) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
