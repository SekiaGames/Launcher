using Godot;

namespace Launcher;

public sealed class Bloodlust : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float AttackSpeedPct => Data('A');
    public float MoveSpeedPct => Data('B');

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

        world.ApplyBuff(target, new StatBuff
        {
            AttackSpeedPct = AttackSpeedPct,
            MoveSpeedPct = MoveSpeedPct,
        }, Duration > 0f ? Duration : 60f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.IsBuilding) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (other.FindBuff<StatBuff>() != null || !InRange(other)) continue;

            float distance = Owner.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}

public sealed class LightningShield : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float DamagePerSecond => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.State.MagicImmune) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new LightningShieldBuff
        {
            DamageSource = Owner,
            Radius = Area > 0f ? Area : 160f,
            DamagePerSecond = DamagePerSecond,
        }, Duration > 0f ? Duration : 20f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => !other.IsBuilding && !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class Purge : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float SlowFactor => Data('A');
    public float SummonDamage => Data('C');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding && !world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        float slow = SlowFactor > 1f ? 1f - 1f / SlowFactor : Mathf.Clamp(SlowFactor, 0f, 1f);
        target.ApplySlow(slow, 0f, seconds);

        if (SummonDamage > 0f && target.IsSummon)
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, target, SummonDamage,
                AttackType.Spells, DamageType.Magic));
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => other.IsSummon || other.Slowed == false);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class Pulverize : Ability
{
    public override bool IsPassive => true;

    public float ChancePercent => Data('A');
    public float Damage => Data('B');
    public float Radius => Data('C') > 0f ? Data('C') : 250f;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (Damage <= 0f || !world.RollPercent(ChancePercent)) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage, AttackType.Spells, DamageType.Magic));
        }
    }
}

public sealed class Berserk : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float MoveSpeedPct => Data('A');
    public float AttackSpeedPct => Data('B');
    public float DamageTakenPct => Data('C');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new StatBuff
        {
            MoveSpeedPct = MoveSpeedPct,
            AttackSpeedPct = AttackSpeedPct,
            DamageTakenPct = DamageTakenPct,
        }, Duration > 0f ? Duration : 12f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 700f)) return false;
        if (Owner.FindBuff<StatBuff>() != null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class LiquidFire : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');
    public float SlowPct => Data('C');
    public float Seconds => Duration > 0f ? Duration : 3f;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (!info.Target.IsBuilding) return;

        Unit target = info.Target;
        if (SlowPct > 0f) target.ApplySlow(Mathf.Clamp(SlowPct, 0f, 1f), Mathf.Clamp(SlowPct, 0f, 1f), Seconds);
        if (DamagePerSecond <= 0f) return;

        world.ApplyBuff(target, new PeriodicDamageBuff
        {
            DamageSource = Owner,
            AmountPerTick = DamagePerSecond,
            Interval = 1f,
            AttackType = AttackType.Spells,
            DamageType = DamageType.Magic,
        }, Seconds);
    }
}

public sealed class UnstableConcoction : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('B');
    public float Radius => Data('C') > 0f ? Data('C') : 200f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        foreach (Unit victim in world.EnemiesInRange(Owner, target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage, AttackType.Spells, DamageType.Magic));
        }
        world.KillUnit(Owner);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;

        int nearby = 0;
        foreach (Unit other in world.EnemiesInRange(Owner, target.Position, Radius))
        {
            if (other.IsBuilding) continue;
            nearby++;
        }
        if (nearby < 2) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class VenomSpears : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');
    public float SlowPct => Data('B');

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (DamagePerSecond <= 0f) return;

        float seconds = info.Target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(info.Target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
            MoveSlowPct = SlowPct,
        }, seconds > 0f ? seconds : 25f);
    }
}

public sealed class Pillage : Ability
{
    public override bool IsPassive => true;

    public float Ratio => Data('A');

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (!info.Target.IsBuilding || Ratio <= 0f || info.Amount <= 0f) return;

        int gold = Mathf.Max(1, Mathf.RoundToInt(info.Amount * Ratio * 0.5f));
        world.AwardGold(Owner.Player, gold, info.Target);
    }
}

public sealed class SpikedBarricades : Ability
{
    public override bool IsPassive => true;
    public override float ReflectFlat => Mathf.Max(0f, Data('A'));
}

public sealed class WarDrums : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.DamagePct = Mathf.Max(bonus.DamagePct, Data('A'));
    }
}