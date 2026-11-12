using Godot;

namespace Launcher;

public sealed class SlowUnit : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float MoveSlowPct => Data('A');
    public float AttackSlowPct => Data('B');
    public bool AlwaysAutoCast => Data('C') > 0f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.IsBuilding) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        target.ApplySlow(MoveSlowPct, AttackSlowPct, seconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.IsBuilding && !other.Slowed);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class InvisibilitySpell : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

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

        world.ApplyBuff(target, new InvisibilityBuff(), Duration > 0f ? Duration : 120f);
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
            if (other.State.Invisible || !InRange(other)) continue;

            float distance = Owner.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}

public sealed class Flare : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        float radius = Area > 0f ? Area : 1800f;
        float seconds = Duration > 0f ? Duration : 15f;
        world.AddFogReveal(Owner.Player, Flatten(point), radius, seconds);
        return true;
    }
}

public sealed class RevealArea : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        float radius = Area > 0f ? Area : 900f;
        float seconds = Duration > 0f ? Duration : 15f;
        world.AddFogReveal(Owner.Player, Owner.Position, radius, seconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Area > 0f ? Area : 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class DetectPassive : Ability
{
    public override bool IsPassive => true;
    public override float PassiveDetectRadius => CastRange > 0f ? CastRange : 900f;
}

public sealed class Feedback : Ability
{
    public override bool IsPassive => true;

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (Owner == null || target == null || !target.Alive) return raw;
        if (target.IsBuilding || target.Mana <= 0f) return raw;

        float limit = target.IsHero ? Data('C') : Data('A');
        float ratio = target.IsHero ? Data('D') : Data('B');
        float drained = Mathf.Min(target.Mana, limit);
        if (drained <= 0f) return raw;

        target.Mana -= drained;
        float bonus = drained * Mathf.Max(0f, ratio);
        if (target.IsSummon) bonus += Data('E');
        return raw + bonus;
    }
}

public sealed class BallsOfFire : Ability
{
    public override bool IsPassive => true;

    public float HitDamage => Data('A');
    public float BurnPerSecond => Data('C');
    public float BurnSeconds => Duration > 0f ? Duration : 2.5f;
    public float Radius => Area > 0f ? Area : 150f;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (HitDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, HitDamage, AttackType.Spells, DamageType.Magic));
            if (BurnPerSecond <= 0f) continue;

            world.ApplyBuff(victim, new PeriodicDamageBuff
            {
                DamageSource = Owner,
                AmountPerTick = BurnPerSecond,
                Interval = 1f,
                AttackType = AttackType.Spells,
                DamageType = DamageType.Magic,
            }, BurnSeconds);
        }
    }
}