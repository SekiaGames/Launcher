using Godot;

namespace Launcher;

public sealed class DevourMagic : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 200f;
    public float ManaPerDispel => Data('E');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        int count = world.DispelArea(Owner, Owner.Position, Radius, hostile: true, summonDamage: 0f);
        if (count > 0 && ManaPerDispel > 0f)
        {
            Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + count * ManaPerDispel);
        }
        return count > 0;
    }

    public override bool TryAutoCast(World world)
    {
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius))
        {
            if (enemy.IsBuilding) continue;
            if (enemy.HasBuff(harmful: false) || enemy.IsSummon)
            {
                return world.TryCast(Owner, this, null, Owner.Position);
            }
        }
        return false;
    }
}

public sealed class OrbOfAnnihilation : Ability
{
    public override bool IsPassive => true;

    public float BonusDamage => Data('A');
    public float SplashRatio => Data('B');
    public float Radius => Area > 0f ? Area : 150f;

    public override float ModifyOutgoingAttack(World world, Unit target, float raw) => raw + BonusDamage;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (SplashRatio <= 0f) return;

        float damage = BonusDamage * SplashRatio;
        if (damage <= 0f) return;

        foreach (Unit other in world.EnemiesInRange(Owner, info.Target.Position, Radius, false))
        {
            if (other == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, other, damage, AttackType.Spells, DamageType.Magic));
        }
    }
}

public sealed class AbsorbMana : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float MaxTransfer => Data('B') > 0f ? Data('B') : float.MaxValue;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Mana <= 0f) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float transfer = Mathf.Min(target.Mana, MaxTransfer);
        target.Mana -= transfer;
        Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + transfer);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => other.Mana >= 10f);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class Web : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Type == null || !target.Type.Flying) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        target.State.Add(StateMod.Snare, seconds > 0f ? seconds : 12f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => other.Type != null && other.Type.Flying);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class FrostBreath : Ability
{
    public override bool IsPassive => true;

    public float MoveSlowPct => 0.5f;
    public float AttackSlowPct => 0.25f;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;

        float seconds = info.Target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        info.Target.ApplySlow(MoveSlowPct, AttackSlowPct, seconds > 0f ? seconds : 10f);

        float radius = Area;
        if (radius <= 0f) return;

        foreach (Unit other in world.EnemiesInRange(Owner, info.Target.Position, radius, false))
        {
            if (other == info.Target) continue;
            other.ApplySlow(MoveSlowPct, AttackSlowPct, seconds > 0f ? seconds : 10f);
        }
    }
}

public sealed class Ghost : Ability
{
    public override bool IsPassive => true;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive) return;
        if (Owner.State.Invisible) return;
        Owner.State.Add(StateMod.Invisible, 0.5f);
    }
}

public sealed class Replenish : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 700f;
    public float HpPerSecond => Data('A');
    public float ManaPerSecond => Data('B');
    public float AmountPerTick => Data('E') > 0f ? Data('E') : 1f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        bool did = false;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (ally == Owner || ally.IsBuilding || !ally.Alive) continue;

            if (HpPerSecond > 0f && ally.Hp < ally.MaxHp)
            {
                ally.Heal(AmountPerTick);
                did = true;
            }
            if (ManaPerSecond > 0f && ally.MaxMana > 0f && ally.Mana < ally.MaxMana)
            {
                ally.Mana = Mathf.Min(ally.MaxMana, ally.Mana + ManaPerSecond);
                did = true;
            }
        }
        return did;
    }

    public override bool TryAutoCast(World world)
    {
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (ally == Owner || ally.IsBuilding || !ally.Alive) continue;
            if (HpPerSecond > 0f && ally.Hp < ally.MaxHp) return world.TryCast(Owner, this, null, Owner.Position);
            if (ManaPerSecond > 0f && ally.Mana < ally.MaxMana) return world.TryCast(Owner, this, null, Owner.Position);
        }
        return false;
    }
}

public sealed class PlagueAura : Ability
{
    public override bool IsPassive => true;

    public float Radius => Area > 0f ? Area : 250f;
    public float InfectionSeconds => Data('A') > 0f ? Data('A') : 120f;
    public float DamagePerSecond => Data('B') > 0f ? Data('B') : 1f;

    private float _timer;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        _timer += delta;
        if (_timer < 1f) return;
        _timer = 0f;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.FindBuff<VenomBuff>() != null) continue;
            world.ApplyBuff(enemy, new VenomBuff
            {
                DamageSource = Owner,
                DamagePerTick = DamagePerSecond,
                Interval = 1f,
            }, InfectionSeconds);
        }
    }
}