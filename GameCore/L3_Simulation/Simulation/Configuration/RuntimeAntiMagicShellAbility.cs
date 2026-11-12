using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 反魔法外壳（Aams）：物编 1.27 的 DataC=0，走「对所有的魔法免疫」这条，Dur/HeroDur 都是 90 秒；
/// 对召唤物不是加盾而是按 DataA 造成伤害并扣 DataD 法力（客户端 RuntimeAntiMagicShellAbility 同口径）。
/// </summary>
public sealed class RuntimeAntiMagicShellAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float SummonDamage => Data('A');
    public float ManaLoss => Data('D');

    private float SecondsFor(Unit target) =>
        target != null && target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (!target.IsSummon && target.FindBuff<RuntimeAntiMagicShellBuff>() != null) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        if (target.IsSummon)
        {
            target.Mana = Mathf.Max(0f, target.Mana - ManaLoss);
            if (SummonDamage > 0f)
            {
                world.DealDamage(World.MakeAbilityDamage(Owner, target, SummonDamage,
                    AttackType.Spells, DamageType.Magic));
            }
            return true;
        }

        float seconds = SecondsFor(target);
        world.ApplyBuff(target, new RuntimeAntiMagicShellBuff(), seconds > 0f ? seconds : 90f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit best = null;
        float lowest = 1f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.IsBuilding || !ally.Alive || ally.FindBuff<RuntimeAntiMagicShellBuff>() != null) continue;
            float ratio = ally.MaxHp > 0f ? ally.Hp / ally.MaxHp : 1f;
            if (ratio >= lowest) continue;
            lowest = ratio;
            best = ally;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}
