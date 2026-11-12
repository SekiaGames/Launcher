using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class Volcano : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public int Waves => Mathf.Max(1, Mathf.RoundToInt(Data('B')));
    public float WaveInterval => Data('C') > 0f ? Data('C') : 5f;
    public float Damage => Data('E');
    public float BuildingFactor => Data('D') > 0f ? Data('D') : 2f;
    public float Radius => Area > 0f ? Area : 500f;

    public override float ChannelWaveInterval => WaveInterval;
    public override float ChannelTotal => Waves * WaveInterval;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelTotal, WaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            float damage = victim.IsBuilding ? Damage * BuildingFactor : Damage;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, damage, AttackType.Spells, DamageType.Magic));
        }
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, target.Position);
    }
}

public sealed class StormEarthFire : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        string list = Type?.DataTextAt(Level, 'A') ?? "";
        int created = 0;
        foreach (string raw in list.Split(',', StringSplitOptions.TrimEntries))
        {
            if (raw.Length < 4) continue;
            if (world.Summon(Owner, raw[..4], Owner.Position, Duration) != null) created++;
        }
        return created > 0;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class BreathOfFire : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('A');
    public float BurnPerSecond => Data('E');
    public float Length => Data('C') > 0f ? Data('C') : 800f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        return FlatDistance(Owner.Position, point) <= Mathf.Max(1f, CastRange) + 120f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 from = Owner.Position;
        Vector3 landing = Flatten(point);
        float distance = FlatDistance(from, landing);
        Vector3 direction = distance < 1f
            ? new Vector3(Mathf.Sin(Owner.Facing), 0f, Mathf.Cos(Owner.Facing))
            : new Vector3(landing.X - from.X, 0f, landing.Z - from.Z) / distance;
        Vector3 end = from + direction * Mathf.Min(Length, Mathf.Max(240f, distance));

        foreach (Unit victim in world.EnemiesAlongLine(Owner, from, end, Area))
        {
            HazeBuff haze = victim.FindBuff<HazeBuff>();
            float fireBonus = haze != null ? haze.FireDamageIncrease : 0f;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage + fireBonus,
                AttackType.Spells, DamageType.Magic));

            if (haze == null || BurnPerSecond <= 0f || !victim.Alive) continue;
            world.ApplyBuff(victim, new PeriodicDamageBuff
            {
                DamageSource = Owner,
                AmountPerTick = BurnPerSecond,
                Interval = 1f,
                AttackType = AttackType.Spells,
                DamageType = DamageType.Magic,
            }, Duration > 0f ? Duration : 5f);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class DrunkenHaze : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float FireDamageIncrease => Data('A');
    public float MissChance => Mathf.Clamp(Data('B'), 0f, 0.95f);
    public float MoveSlowPct => Mathf.Clamp(Data('C'), 0f, 0.95f);
    public float Radius => Area > 0f ? Area : 200f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        foreach (Unit victim in world.EnemiesInRange(Owner, target.Position, Radius, false))
        {
            float seconds = victim.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
            world.ApplyBuff(victim, new HazeBuff
            {
                MissChance = MissChance,
                MoveSpeedPct = -MoveSlowPct,
                FireDamageIncrease = FireDamageIncrease,
            }, seconds > 0f ? seconds : 12f);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => other.FindBuff<HazeBuff>() == null);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class DrunkenBrawler : Ability
{
    public override bool IsPassive => true;

    public float CriticalPercent => Data('A');
    public float CriticalMultiplier => Data('B') > 0f ? Data('B') : 2f;
    public float EvasionChance => Mathf.Clamp(Data('D'), 0f, 0.95f);

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (world == null || CriticalPercent <= 0f) return raw;
        if (!world.RollPercent(CriticalPercent)) return raw;
        return raw * CriticalMultiplier;
    }

    public override bool CanBeHitBy(World world, DamageData info)
    {
        if (info == null || !info.IsAttack || EvasionChance <= 0f) return true;
        return !world.RollPercent(EvasionChance * 100f);
    }
}

public sealed class HowlOfTerror : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float DamageReduction => Mathf.Clamp(Data('A'), 0f, 1f);
    public float ArmorReduction => Data('B');
    public float Radius => Area > 0f ? Area : 500f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            float seconds = enemy.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
            world.ApplyBuff(enemy, new CurseBuff
            {
                DamagePct = -DamageReduction,
                Armor = -ArmorReduction,
            }, seconds > 0f ? seconds : 15f);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Radius)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class CleavingAttack : Ability
{
    public override bool IsPassive => true;

    public float Ratio => Data('A');
    public float Radius => Area > 0f ? Area : 200f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (Ratio <= 0f || info.Amount <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, info.Amount * Ratio,
                AttackType.Normal, DamageType.Universal));
        }
    }
}

public sealed class Doom : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float DamagePerSecond => Data('A');
    public int SummonCount => Mathf.Max(1, Mathf.RoundToInt(Data('B')));
    public float SummonSeconds => Data('C') > 0f ? Data('C') : 120f;
    public string SummonUnitId => Type?.SummonAt(Level) ?? "nba2";

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

        world.ApplyBuff(target, new DoomBuff
        {
            Caster = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
            SummonUnitId = SummonUnitId,
            SummonCount = SummonCount,
            SummonSeconds = SummonSeconds,
        }, Duration > 0f ? Duration : 60f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true,
            accept: other => !other.IsHero && !other.IsBuilding && other.FindBuff<DoomBuff>() == null);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class Stampede : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public float Damage => Data('B');
    public float Radius => Data('D') > 0f ? Data('D') : 275f;
    public float WaveInterval => Data('E') > 0f ? Data('E') : 0.2f;

    private int _wave;

    public override float ChannelWaveInterval => WaveInterval;
    public override float ChannelTotal => Duration > 0f ? Duration : 30f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        _wave = 0;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelTotal, WaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        _wave++;
        if (_wave % 5 != 0) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius, false))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage, AttackType.Siege, DamageType.Magic));
        }
    }

    public override void OnChannelEnded(World world, bool interrupted)
    {
        _wave = 0;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, target.Position);
    }
}