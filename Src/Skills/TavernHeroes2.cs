using Godot;

namespace Launcher;

public sealed class AcidBomb : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Radius => Area > 0f ? Area : 200f;
    public float ImpactDamage => Data('C');
    public float DamagePerSecond => Data('D');
    public float ArmorPenalty => Data('E');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = Duration > 0f ? Duration : 15f;
        foreach (Unit victim in world.EnemiesInRange(Owner, target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, ImpactDamage,
                AttackType.Spells, DamageType.Magic));
            if (!victim.Alive) continue;

            world.ApplyBuff(victim, new VenomBuff
            {
                DamageSource = Owner,
                DamagePerTick = DamagePerSecond,
                Interval = 1f,
            }, seconds);

            if (ArmorPenalty > 0f)
            {
                world.ApplyBuff(victim, new CurseBuff { Armor = -ArmorPenalty }, seconds);
            }
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class HealingSpray : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 250f;
    public float HealAmount => Data('D');

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive || HealAmount <= 0f) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Flatten(point), Radius))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            ally.Heal(HealAmount);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit wounded = null;
        float lowest = 0.9f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 800f))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            float ratio = ally.Hp / Mathf.Max(1f, ally.MaxHp);
            if (ratio >= lowest) continue;
            lowest = ratio;
            wounded = ally;
        }
        return wounded != null && world.TryCast(Owner, this, null, wounded.Position);
    }
}

public sealed class Transmute : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float GoldFactor => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        int gold = Mathf.Max(1, Mathf.RoundToInt(target.Type != null ? target.Type.GoldCost * GoldFactor : 0f));
        world.KillUnit(target, Owner);
        world.AwardGold(Owner.Player, gold, target);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && other.Type != null && other.Type.GoldCost >= 100);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class ClusterRockets : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Radius => Area > 0f ? Area : 200f;
    public int Rockets => Mathf.Max(1, Mathf.RoundToInt(Data('C')));
    public float DamagePerRocket => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        Vector3 spot = Flatten(point);
        for (int index = 0; index < Rockets; index++)
        {
            foreach (Unit victim in world.EnemiesInRange(Owner, spot, Radius))
            {
                world.DealDamage(World.MakeAbilityDamage(Owner, victim, DamagePerRocket,
                    AttackType.Siege, DamageType.Magic));
            }
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, target.Position);
    }
}

public sealed class Incinerate : Ability
{
    public override bool IsPassive => true;

    public float BonusDamage => Data('A');
    public float DamagePerSecond => Data('D');

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;

        if (BonusDamage > 0f)
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, info.Target, BonusDamage,
                AttackType.Spells, DamageType.Magic));
        }
        if (DamagePerSecond <= 0f || !info.Target.Alive) return;

        world.ApplyBuff(info.Target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
        }, Duration > 0f ? Duration : 2f);
    }
}

public sealed class SoulBurn : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float DamagePerSecond => Data('A');
    public float SilenceSeconds => Data('C') > 0f ? Data('C') : 0.5f;

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
        if (seconds <= 0f) seconds = 14f;

        world.ApplyBuff(target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
        }, seconds);
        target.State.Add(StateMod.Silence, seconds * SilenceSeconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}