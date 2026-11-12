using Godot;

namespace Launcher;

public sealed class CloudOfFog : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float Radius => Area > 0f ? Area : 300f;
    public float Lifetime => Duration > 0f ? Duration : 30f;
    public float MissChance => 0.7f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.AddGroundSpell(new CloudOfFogSpell
        {
            Caster = Owner,
            Position = Flatten(point),
            Radius = Radius,
            Remaining = Lifetime,
            MissChance = MissChance,
        });
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 1000f)) return false;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 1000f))
        {
            if (enemy.Type == null || !enemy.Type.Flying) continue;
            return world.TryCast(Owner, this, null, enemy.Position);
        }
        return false;
    }
}

public sealed class CloudOfFogSpell : IGroundSpell
{
    public Unit Caster;
    public Vector3 Position;
    public float Radius = 300f;
    public float Remaining = 30f;
    public float MissChance = 0.7f;

    private float _accumulator;

    public bool Step(World world, float delta)
    {
        Remaining -= delta;
        if (Remaining <= 0f || world == null || Caster == null || !Caster.Alive) return false;

        _accumulator += delta;
        if (_accumulator < 0.5f) return true;
        _accumulator = 0f;

        foreach (Unit victim in world.EnemiesInRange(Caster, Position, Radius))
        {
            if (victim.IsBuilding) continue;
            world.ApplyBuff(victim, new CurseBuff { MissChance = MissChance }, 1.5f);
        }
        return true;
    }
}

public sealed class Detonate : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 300f;
    public float Damage => Data('A');
    public float SummonDamage => Data('B');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.DispelArea(Owner, Owner.Position, Radius, hostile: true, SummonDamage);
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage,
                AttackType.Spells, DamageType.Magic));
        }
        world.KillUnit(Owner);
        return true;
    }
}

public sealed class SelfDestruct : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Radius => Area > 0f ? Area : 250f;
    public float Damage => Data('B');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage,
                AttackType.Spells, DamageType.Magic));
        }
        world.KillUnit(Owner);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        int nearby = 0;
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.IsBuilding) continue;
            nearby++;
        }
        if (nearby < 2) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class ManaBattery : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.ManaRegenPerSecond = Mathf.Max(bonus.ManaRegenPerSecond, Data('B'));
    }
}

public sealed class Unsummon : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive || target == Owner) return false;
        if (!target.IsSummon) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;
        world.KillUnit(target);
        return true;
    }
}

public sealed class NeutralRegen : Ability
{
    public override bool IsPassive => true;

    public float RegenRate => Data('A') > 0f ? Data('A') : 0.01f;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive) return;

        bool manaOnly = string.Equals(Type?.Id, "ANre", System.StringComparison.OrdinalIgnoreCase);
        if (!manaOnly && Owner.Hp < Owner.MaxHp)
        {
            Owner.Hp = Mathf.Min(Owner.MaxHp, Owner.Hp + Owner.MaxHp * RegenRate * delta);
        }
        if (Owner.MaxMana > 0f && Owner.Mana < Owner.MaxMana)
        {
            Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + Owner.MaxMana * RegenRate * delta);
        }
    }
}

public sealed class AttributeModifier : Ability
{
    public override bool IsPassive => true;

    private bool _applied;

    public override void OnWorldTick(World world, float delta)
    {
        if (_applied || Owner == null) return;
        _applied = true;

        Owner.BonusStrength += Mathf.RoundToInt(Data('A'));
        Owner.BonusAgility += Mathf.RoundToInt(Data('B'));
        Owner.BonusIntelligence += Mathf.RoundToInt(Data('C'));
        world?.RefreshHeroAttributes(Owner);
    }
}

public sealed class InventoryPassive : Ability
{
    public override bool IsPassive => true;
}

public sealed class CallToArms : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 2000f;
    public float MilitiaSeconds => 45f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        UnitType militia = UnitFactory.Get("hmil");
        if (militia == null) return false;

        int converted = 0;
        foreach (Unit worker in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (!worker.Alive || worker.IsBuilding || worker.Type == null) continue;
            if (!string.Equals(worker.Type.Id, "hpea", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (world.BeginMorph(worker, militia, MilitiaSeconds, disable: false)) converted++;
        }
        return converted > 0;
    }
}

public sealed class FrostBolt : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('A');
    public float SlowPct => 0.5f;

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
        if (target.Alive) target.ApplySlow(SlowPct, 0f, Duration > 0f ? Duration : 5f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => !other.IsBuilding && !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class ManaShield : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float ManaPerDamage => Data('A') > 0f ? Data('A') : 2f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new ManaShieldBuff { ManaPerDamage = ManaPerDamage }, Duration > 0f ? Duration : 30f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.Mana <= 0f) return false;
        if (!HasEnemyNearby(world, 700f)) return false;
        if (Owner.FindBuff<ManaShieldBuff>() != null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class ManaShieldBuff : Buff
{
    public float ManaPerDamage = 2f;

    public override bool IsHarmful => false;

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (info == null || info.Amount <= 0f || Owner == null || ManaPerDamage <= 0f) return;
        if (Owner.Mana <= 0f) return;

        float absorbed = Mathf.Min(info.Amount, Owner.Mana / ManaPerDamage);
        Owner.Mana = Mathf.Max(0f, Owner.Mana - absorbed * ManaPerDamage);
        info.Amount -= absorbed;
        if (Owner.Mana <= 0f) Remaining = 0f;
    }
}

public sealed class Monsoon : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public float Damage => Data('A');
    public float SlowPct => Mathf.Clamp(Data('C'), 0f, 0.9f);
    public float Radius => Area > 0f ? Area : 300f;

    public override float ChannelWaveInterval => Data('B') > 0.2f ? Data('B') : 1.5f;
    public override float ChannelTotal => Duration > 0f ? Duration : 15f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage,
                AttackType.Spells, DamageType.Magic));
            if (victim.Alive && SlowPct > 0f) victim.ApplySlow(SlowPct, 0f, 2f);
        }
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 500f)) return false;
        return world.TryCast(Owner, this, null, Owner.Target?.Position ?? Owner.Position);
    }
}