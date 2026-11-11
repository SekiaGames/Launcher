using Godot;

namespace Launcher;

public sealed class Roar : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float DamagePct => Data('A');
    public float Radius => Area > 0f ? Area : 500f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (ally.IsBuilding) continue;
            world.ApplyBuff(ally, new StatBuff { DamagePct = DamagePct }, Duration > 0f ? Duration : 45f);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Radius)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class Rejuvenation : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float TotalHeal => Data('A');
    public float TotalSeconds => Duration > 0f ? Duration : 12f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.IsBuilding) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new HealOverTimeBuff
        {
            AmountPerTick = TotalHeal / Mathf.Max(1f, TotalSeconds),
            Interval = 1f,
        }, TotalSeconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit best = null;
        float bestRatio = 0.85f;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.IsBuilding) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (!InRange(other) || other.FindBuff<HealOverTimeBuff>() != null) continue;

            float ratio = other.Hp / Mathf.Max(1f, other.MaxHp);
            if (ratio >= bestRatio) continue;
            bestRatio = ratio;
            best = other;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}

public sealed class Cyclone : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.IsBuilding || target.Type.Flying) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        target.State.Add(StateMod.Pause, seconds);
        target.State.Add(StateMod.Invulnerable, seconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true,
            accept: other => !other.IsBuilding && !other.Type.Flying);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class FaerieFire : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float ArmorPenalty => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(target, new FaerieFireBuff { ArmorPenalty = ArmorPenalty },
            seconds > 0f ? seconds : 90f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => other.FindBuff<FaerieFireBuff>() == null);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class AbolishMagic : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float SummonDamage => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        bool enemy = !world.AreAllied(Owner.Player, target.Player);
        world.DispelBuffs(target, Owner, harmful: !enemy, helpful: enemy, SummonDamage);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner.Mana < ManaCost) return false;
        float range = CastRange > 0f ? CastRange : 500f;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, range))
        {
            if (ally == Owner || ally.IsBuilding || !ally.Alive) continue;
            if (!ally.HasBuff(harmful: true)) continue;
            return world.TryCast(Owner, this, ally, ally.Position);
        }

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, range))
        {
            if (enemy.IsBuilding) continue;
            if (!enemy.HasBuff(harmful: false) && !enemy.IsSummon) continue;
            return world.TryCast(Owner, this, enemy, enemy.Position);
        }
        return false;
    }
}

public sealed class SlowPoison : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');
    public float MoveSlowPct => Data('B');
    public float AttackSlowPct => Data('C');

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;

        float seconds = info.Target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(info.Target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
            MoveSlowPct = MoveSlowPct,
            AttackSlowPct = AttackSlowPct,
        }, seconds > 0f ? seconds : 5f);
    }
}

public sealed class ManaFlare : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 750f;
    public float ManaCostPct => Data('A') > 0f ? Mathf.Clamp(Data('A') * 0.1f, 0f, 1f) : 0.3f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new ManaFlareBuff
        {
            DamageSource = Owner,
            Radius = Radius,
            ManaCostPct = ManaCostPct,
        }, Duration > 0f ? Duration : 30f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner.Mana < ManaCost) return false;
        if (!HasEnemyNearby(world, Radius)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class PhaseShift : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        float seconds = Duration > 0f ? Duration : 1.5f;
        Owner.State.Add(StateMod.Invisible, seconds);
        Owner.State.Add(StateMod.Pause, seconds);
        return true;
    }
}

public sealed class Taunt : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float Radius => Area > 0f ? Area : 450f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.IsBuilding) continue;
            enemy.Target = Owner;
            enemy.MoveTarget = null;
            enemy.Path = null;
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        int nearby = 0;
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.IsBuilding || enemy.Target == Owner) continue;
            nearby++;
        }
        if (nearby < 2) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class HardenedSkin : Ability
{
    public override bool IsPassive => true;

    public float MinDamage => Data('B');
    public float Reduction => Data('C');
    /// <summary>物编 DataA：触发概率百分比（山岭巨人 / 龙龟都是 100，即必发）。</summary>
    public float ChancePercent => Mathf.Clamp(Data('A'), 0f, 100f);

    public override void ModifyIncomingDamage(World world, DamageInfo info)
    {
        if (Owner == null || info == null || info.DamageType != DamageType.Normal) return;
        if (info.Source == null) return;

        bool allowed = info.Ranged ? Data('D') > 0f : Data('E') > 0f;
        if (!allowed) return;
        if (ChancePercent < 100f && (world == null || !world.RollPercent(ChancePercent))) return;

        info.Amount = Reduction < info.Amount ? info.Amount - Reduction : MinDamage;
    }
}

public sealed class ElunesGrace : Ability
{
    public override bool IsPassive => true;

    public float Reduction => Mathf.Clamp(Data('A'), 0f, 1f);

    public override void ModifyIncomingDamage(World world, DamageInfo info)
    {
        if (Owner == null || info == null || !info.Ranged) return;
        if (info.DamageType != DamageType.Normal) return;

        info.Amount *= 1f - Reduction;
    }
}

public sealed class Sentinel : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public float SightRadius => Data('B') > 0f ? Data('B') : 900f;
    public float Lifetime => Data('A') > 0f ? Data('A') : 100f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        world.AddFogReveal(Owner.Player, Flatten(point), SightRadius, Lifetime);
        return true;
    }
}

public sealed class MoonGlaive : Ability
{
    public override bool IsPassive => true;

    public int Bounces => 2;
    public float Radius => 400f;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null) return;

        Unit previous = info.Target;
        float ratio = 0.5f;
        for (int bounce = 0; bounce < Bounces; bounce++)
        {
            Unit next = null;
            float bestDistance = float.MaxValue;
            foreach (Unit other in world.EnemiesInRange(Owner, previous.Position, Radius))
            {
                if (other == previous || other.IsBuilding) continue;
                if (other == info.Target) continue;

                float distance = previous.DistanceTo(other);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                next = other;
            }
            if (next == null) break;

            world.DealDamage(World.MakeAbilityDamage(Owner, next, info.Amount * ratio,
                AttackType.Pierce, DamageType.Normal));
            previous = next;
            ratio *= 0.5f;
        }
    }
}

public sealed class LightningAttack : Ability
{
    public override bool IsPassive => true;

    public float FirstRatio => Data('A') > 0f ? Data('A') : 0.25f;
    public float NextRatio => Data('B') > 0f ? Data('B') : 0.25f;
    public float Radius => 500f;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null) return;

        foreach (Unit other in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (other == info.Target || other.IsBuilding) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, other, info.Amount * FirstRatio,
                AttackType.Spells, DamageType.Magic));
        }
    }
}

public sealed class CorrosiveBreath : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (!info.Target.IsBuilding || DamagePerSecond <= 0f) return;

        world.ApplyBuff(info.Target, new PeriodicDamageBuff
        {
            DamageSource = Owner,
            AmountPerTick = DamagePerSecond,
            Interval = 1f,
            AttackType = AttackType.Spells,
            DamageType = DamageType.Magic,
        }, Duration > 0f ? Duration : 5f);
    }
}