using Godot;

namespace Launcher;

public sealed class InvisibilityBuff : Buff, IAttackLaunchBuff
{
    public override bool IsHarmful => false;

    public override void OnApply(World world)
    {
        Owner?.State.Add(StateMod.Invisible, Remaining);
    }

    public override void OnRemove(World world)
    {
        Owner?.State.RemoveAll(StateMod.Invisible);
    }

    public void OnAttackLaunched(World world, Unit target, ref float raw)
    {
        Owner?.State.RemoveAll(StateMod.Invisible);
        Remaining = 0f;
    }
}

public sealed class StatBuff : Buff
{
    public float AttackSpeedPct;
    public float MoveSpeedPct;
    public float DamagePct;
    public float DamageTakenPct;
    public float Armor;
    public float MoveSpeedFlat;

    public override bool IsHarmful => false;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        Owner.BonusAttackSpeedPct += AttackSpeedPct;
        Owner.BonusDamagePct += DamagePct;
        Owner.BonusDamageTakenPct += DamageTakenPct;
        Owner.BonusArmor += Armor;
        Owner.BonusMoveSpeedFlat += MoveSpeedFlat + (Owner.Type != null ? Owner.Type.Speed * MoveSpeedPct : 0f);
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;
        Owner.BonusAttackSpeedPct -= AttackSpeedPct;
        Owner.BonusDamagePct -= DamagePct;
        Owner.BonusDamageTakenPct -= DamageTakenPct;
        Owner.BonusArmor -= Armor;
        Owner.BonusMoveSpeedFlat -= MoveSpeedFlat + (Owner.Type != null ? Owner.Type.Speed * MoveSpeedPct : 0f);
    }
}

public sealed class FaerieFireBuff : Buff
{
    public float ArmorPenalty;

    public override void OnApply(World world)
    {
        if (Owner != null) Owner.BonusArmor -= ArmorPenalty;
    }

    public override void OnRemove(World world)
    {
        if (Owner != null) Owner.BonusArmor += ArmorPenalty;
    }
}

public sealed class HealOverTimeBuff : Buff
{
    public float AmountPerTick;
    public float Interval = 1f;

    private float _accumulator;

    public override bool IsHarmful => false;

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (Owner == null || !Owner.Alive) return false;
        if (Interval <= 0f) return alive;

        _accumulator += delta;
        while (_accumulator >= Interval && Owner.Alive)
        {
            _accumulator -= Interval;
            Owner.Heal(AmountPerTick);
        }
        return alive;
    }

    public override void OnRemove(World world)
    {
        if (Owner != null) Owner.View?.SetHp(Owner.Hp / Mathf.Max(1f, Owner.MaxHp), Owner.Alive);
    }
}

public sealed class LightningShieldBuff : Buff
{
    public Unit DamageSource;
    public float Radius = 160f;
    public float DamagePerSecond = 20f;

    private float _accumulator;

    public override bool IsHarmful => false;

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (world == null || Owner == null || !Owner.Alive) return false;

        _accumulator += delta;
        while (_accumulator >= 1f && Owner.Alive)
        {
            _accumulator -= 1f;
            foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
            {
                world.DealDamage(World.MakeAbilityDamage(DamageSource ?? Owner, victim, DamagePerSecond,
                    AttackType.Spells, DamageType.Magic));
            }
        }
        return alive;
    }
}

public sealed class ManaFlareBuff : Buff
{
    public Unit DamageSource;
    public float Radius = 750f;
    public float ManaCostPct = 0.3f;

    public override bool IsHarmful => false;

    public override void OnAttacked(World world, Unit attacker, DamageInfo info)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        float burn = Owner.Mana * ManaCostPct;
        if (burn <= 0f) return;

        Owner.Mana = Mathf.Max(0f, Owner.Mana - burn);
        float damage = Mathf.Max(1f, burn);
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            world.DealDamage(World.MakeAbilityDamage(DamageSource ?? Owner, victim, damage,
                AttackType.Spells, DamageType.Magic));
        }
    }
}

public sealed class VenomBuff : Buff
{
    public Unit DamageSource;
    public float DamagePerTick;
    public float Interval = 1f;
    public float MoveSlowPct;
    public float AttackSlowPct;

    private float _accumulator;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        if (MoveSlowPct > 0f || AttackSlowPct > 0f) Owner.ApplySlow(MoveSlowPct, AttackSlowPct, Remaining);
    }

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (world == null || Owner == null || !Owner.Alive) return false;

        _accumulator += delta;
        while (_accumulator >= Interval && Owner.Alive)
        {
            _accumulator -= Interval;
            world.DealDamage(World.MakeAbilityDamage(DamageSource, Owner, DamagePerTick,
                AttackType.Spells, DamageType.Magic));
        }
        return alive;
    }
}