using Godot;

namespace Launcher;

public sealed class Defend : Ability
{
    public override bool IsToggle => true;

    public float RangedDamageReduction => Data('A');
    public float MoveSlowPct => Data('C');

    private float _appliedSlow;

    public override void OnToggle(World world, bool on)
    {
        if (Owner?.Type == null) return;

        if (on)
        {
            _appliedSlow = Owner.Type.Speed * Mathf.Clamp(MoveSlowPct, 0f, 0.9f);
            Owner.BonusMoveSpeedFlat -= _appliedSlow;
        }
        else
        {
            Owner.BonusMoveSpeedFlat += _appliedSlow;
            _appliedSlow = 0f;
        }
    }

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (!ToggledOn || info == null || !info.Ranged || info.Amount <= 0f) return;
        info.Amount *= 1f - Mathf.Clamp(RangedDamageReduction, 0f, 0.95f);
    }
}

public sealed class InnerFire : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float DamagePct => Data('A');
    public float ArmorBonus => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new StatBuff
        {
            DamagePct = DamagePct,
            Armor = ArmorBonus,
        }, Duration > 0f ? Duration : 60f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit best = null;
        float lowest = 1f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.IsBuilding || !ally.Alive || ally.FindBuff<StatBuff>() != null) continue;
            float ratio = ally.MaxHp > 0f ? ally.Hp / ally.MaxHp : 1f;
            if (ratio >= lowest) continue;
            lowest = ratio;
            best = ally;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}

public sealed class MassDispel : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 200f;
    public float SummonDamage => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        Vector3 spot = Flatten(point);
        world.DispelArea(Owner, spot, Radius, hostile: true, SummonDamage);
        world.DispelArea(Owner, spot, Radius, hostile: false, 0f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 500f)) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.HasBuff(harmful: true)) return world.TryCast(Owner, this, null, ally.Position);
        }
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (enemy.IsSummon || enemy.HasBuff(harmful: false)) return world.TryCast(Owner, this, null, enemy.Position);
        }
        return false;
    }
}

public sealed class AerialShackles : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;
    public override bool IsChanneled => true;

    public float DamagePerSecond => Data('A');
    public override float ChannelWaveInterval => 1f;
    public override float ChannelTotal => EffectiveDuration > 0f ? EffectiveDuration : 40f;

    private Unit _target;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.Type == null || !target.Type.Flying) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        _target = target;
        return world.BeginChannel(Owner, this, target.Position, ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null || _target == null || !_target.Alive) return;
        if (Owner.DistanceTo(_target) > 800f) return;

        world.DealDamage(World.MakeAbilityDamage(Owner, _target, DamagePerSecond,
            AttackType.Spells, DamageType.Magic));
    }

    public override void OnChannelEnded(World world, bool interrupted) => _target = null;
}

public sealed class FlakCannon : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('A');
    public float Radius => 300f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (info.Target.Type == null || !info.Target.Type.Flying) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target || victim.Type == null || !victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Spells, DamageType.Magic));
        }
    }
}

public sealed class FragShards : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('C');
    public float Radius => Area > 0f ? Area : 100f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Siege, DamageType.Normal));
        }
    }
}

public sealed class RocketAttack : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('A');
    public float Radius => 250f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (info.Target.Type == null || !info.Target.Type.Flying) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target || victim.Type == null || !victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Siege, DamageType.Normal));
        }
    }
}

public sealed class SpellSteal : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive || target == Owner) return false;
        if (target.IsBuilding) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        Buff stolen = null;
        for (int index = 0; index < target.Buffs.Count; index++)
        {
            Buff candidate = target.Buffs[index];
            if (candidate.IsHarmful) continue;
            stolen = candidate;
            target.Buffs.RemoveAt(index);
            break;
        }
        if (stolen == null) return false;

        stolen.OnRemove(world);
        stolen.Owner = Owner;
        Owner.Buffs.Add(stolen);
        stolen.OnApply(world);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => other.HasBuff(harmful: false));
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class StormHammers : Ability
{
    public override bool IsPassive => true;

    public float Ratio => 0.5f;
    public float Radius => 500f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || info.Amount <= 0f) return;

        Unit second = null;
        float best = float.MaxValue;
        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target || victim.IsBuilding) continue;
            float distance = info.Target.DistanceTo(victim);
            if (distance >= best) continue;
            best = distance;
            second = victim;
        }
        if (second == null) return;

        world.DealDamage(World.MakeAbilityDamage(Owner, second, info.Amount * Ratio,
            AttackType.Spells, DamageType.Magic));
    }
}

public sealed class FireBolt : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('A');
    public float StunSeconds => Duration > 0f ? Duration : 2f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.State.MagicImmune) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.DealDamage(World.MakeAbilityDamage(Owner, target, Damage,
            AttackType.Spells, DamageType.Magic));
        if (target.Alive && StunSeconds > 0f) target.State.Add(StateMod.Stun, StunSeconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true,
            accept: other => !other.IsBuilding && !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class PhoenixFireAbility : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('A');
    public float Radius => Area > 0f ? Area : 600f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Magic, DamageType.Magic));
        }
    }
}