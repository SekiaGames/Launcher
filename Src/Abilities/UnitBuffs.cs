using Godot;

namespace OpenWC3;

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

public class StatBuff : Buff
{
    public float AttackSpeedPct;
    public float MoveSpeedPct;
    public float DamagePct;
    public float DamageTakenPct;
    public float Armor;
    public float MoveSpeedFlat;
    public float MissChance;
    /// <summary>固定攻击力加成（吸血药水这类「+12 伤害」的物品增益）。</summary>
    public float FlatDamage;
    /// <summary>普攻吸血比例（吸血药水同款的另一项）。</summary>
    public float LifestealPct;
    /// <summary>醉酒云雾的火焰易伤（喷火对它额外加伤并点燃）。</summary>
    public float FireDamageIncrease;

    public override bool IsHarmful => false;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        Owner.BonusAttackSpeedPct += AttackSpeedPct;
        Owner.BonusDamagePct += DamagePct;
        Owner.BonusDamageTakenPct += DamageTakenPct;
        Owner.BonusArmor += Armor;
        Owner.BonusDamageFlat += FlatDamage;
        Owner.BonusLifestealPct += LifestealPct;
        Owner.BonusMoveSpeedFlat += MoveSpeedFlat + (Owner.Type != null ? Owner.Type.Speed * MoveSpeedPct : 0f);
        Owner.AttackMissChance = Mathf.Clamp(Owner.AttackMissChance + MissChance, 0f, 0.95f);
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;
        Owner.BonusAttackSpeedPct -= AttackSpeedPct;
        Owner.BonusDamagePct -= DamagePct;
        Owner.BonusDamageTakenPct -= DamageTakenPct;
        Owner.BonusArmor -= Armor;
        Owner.BonusDamageFlat -= FlatDamage;
        Owner.BonusLifestealPct -= LifestealPct;
        Owner.BonusMoveSpeedFlat -= MoveSpeedFlat + (Owner.Type != null ? Owner.Type.Speed * MoveSpeedPct : 0f);
        Owner.AttackMissChance = Mathf.Clamp(Owner.AttackMissChance - MissChance, 0f, 0.95f);
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

    public override void OnAttacked(World world, Unit attacker, DamageData info)
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

public sealed class HazeBuff : StatBuff
{
    public override bool IsHarmful => true;
}

public sealed class CurseBuff : StatBuff
{
    public override bool IsHarmful => true;
}

public sealed class DoomBuff : Buff
{
    public Unit Caster;
    public string SummonUnitId = "";
    public int SummonCount;
    public float SummonSeconds;
    public float DamagePerTick;
    public float Interval = 1f;

    private float _accumulator;

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (world == null || Owner == null || !Owner.Alive) return false;

        _accumulator += delta;
        while (_accumulator >= Interval && Owner.Alive)
        {
            _accumulator -= Interval;
            world.DealDamage(World.MakeAbilityDamage(Caster, Owner, DamagePerTick,
                AttackType.Spells, DamageType.Magic));
        }
        return alive && Owner.Alive;
    }

    public override void OnDeath(World world)
    {
        if (world == null || Caster == null || string.IsNullOrEmpty(SummonUnitId)) return;
        for (int index = 0; index < SummonCount; index++)
        {
            world.Summon(Caster, SummonUnitId, Owner.Position, SummonSeconds);
        }
    }
}
