using Godot;

namespace Launcher;

/// <summary>
/// 法力燃烧 AEmb（恶魔猎手）。烧掉目标 DataA(50/100/150) 点法力，并造成等量的魔法伤害。
/// 物编：Cool=7/6/5 Cost=50 Rng=300。
/// </summary>
public sealed class ManaBurn : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

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

        float burned = Mathf.Min(target.Mana, Data('A'));
        if (burned <= 0f) return false;

        target.Mana -= burned;
        world.DealDamage(World.MakeAbilityDamage(Owner, target, burned, AttackType.Spells, DamageType.Magic));
        world.ReportManaBurned(target, burned);
        return true;
    }

    /// <summary>AI：只烧有法力的目标（英雄/法师），没有就留着不放。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => other.Mana >= 10f);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
