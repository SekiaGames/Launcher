using Godot;

namespace Launcher;

/// <summary>
/// 暗影突袭 AEsh（守望者）。物编 ubertip：最初 DataE(75/150/225) 伤害，
/// 之后每 Cast(3) 秒造成 DataA(10/30/45)，持续 Dur(15.1) 秒；期间减速 DataB(0.5) 持续 DataD(3) 秒。
/// 物编：Cool=8 Cost=75 Rng=300。
/// </summary>
public sealed class ShadowStrike : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

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

        world.DealDamage(World.MakeAbilityDamage(Owner, target, Data('E'), AttackType.Spells, DamageType.Magic));
        if (!target.Alive) return true;

        float slowSeconds = Data('D') > 0f ? Data('D') : 3f;
        target.ApplySlow(Data('B'), Data('B'), slowSeconds);

        float interval = CastInterval > 0f ? CastInterval : 3f;
        target.Buffs.Add(new PeriodicDamageBuff
        {
            Owner = target,
            Source = this,
            Name = Name,
            Remaining = Duration,
            DamageSource = Owner,
            AmountPerTick = Data('A'),
            Interval = interval,
        });
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = Owner?.Target;
        if (target == null || !target.Alive) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
