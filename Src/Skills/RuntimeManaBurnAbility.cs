using Godot;

namespace OpenWC3;

/// <summary>
/// 法力燃烧 AEmb（恶魔猎手）。烧掉目标 DataA(50/100/150) 点法力，并造成等量的魔法伤害。
/// 物编：Cool=7/6/5 Cost=50 Rng=300。
/// </summary>
public sealed class RuntimeManaBurnAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    /// <summary>箭矢/光束延迟（物编 DataB，实测 0.25s）：原版先出束，到点才扣蓝结算。</summary>
    public float LaunchDelay => Mathf.Max(0f, Data('B'));

    private float _pendingBurn;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.State.MagicImmune || target.State.Invulnerable) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive) return false;

        _pendingBurn = Data('A');
        return world.ScheduleAbilityImpact(Owner, this, target, LaunchDelay);
    }

    public override void OnMissileHit(World world, Unit target)
    {
        if (world == null || target == null || !target.Alive) return;

        float limit = _pendingBurn > 0f ? _pendingBurn : Data('A');
        float burned = Mathf.Min(target.Mana, limit);
        if (burned <= 0f) return;

        target.Mana -= burned;
        world.DealDamage(World.MakeAbilityDamage(Owner, target, burned, AttackType.Spells, DamageType.Magic));
        world.ReportManaBurned(target, burned);
    }

    /// <summary>AI：只烧有法力的目标（英雄/法师），没有就留着不放。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => other.Mana >= 10f);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
