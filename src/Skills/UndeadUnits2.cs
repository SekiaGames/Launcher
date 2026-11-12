using Godot;

namespace Launcher;

public sealed class AntiMagicShield : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Absorb => Data('C');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.FindBuff<AntiMagicShieldBuff>() != null) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new AntiMagicShieldBuff
        {
            Caster = Owner,
            Absorb = Absorb,
        }, Duration > 0f ? Duration : 90f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit best = null;
        float lowest = 1f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.IsBuilding || !ally.Alive || ally.FindBuff<AntiMagicShieldBuff>() != null) continue;
            float ratio = ally.MaxHp > 0f ? ally.Hp / ally.MaxHp : 1f;
            if (ratio >= lowest) continue;
            lowest = ratio;
            best = ally;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}

public sealed class AntiMagicShieldBuff : Buff
{
    public Unit Caster;
    public float Absorb;

    public override bool IsHarmful => false;

    public override void ModifyIncomingDamage(World world, DamageInfo info)
    {
        if (info == null || info.Amount <= 0f || Absorb <= 0f) return;
        if (info.DamageType != DamageType.Magic) return;

        float taken = Mathf.Min(Absorb, info.Amount);
        info.Amount -= taken;
        Absorb -= taken;
        if (Absorb <= 0f) Remaining = 0f;
    }
}

public sealed class Curse : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float MissChance => Mathf.Clamp(Data('A'), 0f, 0.9f);

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

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(target, new CurseBuff { MissChance = MissChance }, seconds > 0f ? seconds : 60f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true,
            accept: other => !other.IsBuilding && !other.State.MagicImmune && !other.HasBuff(harmful: true));
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class Cripple : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float MoveSlowPct => Data('A');
    public float AttackSlowPct => Data('B');
    public float DamageReduction => Data('C');

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

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(target, new CurseBuff
        {
            MoveSpeedPct = -Mathf.Clamp(MoveSlowPct, 0f, 0.9f),
            AttackSpeedPct = -Mathf.Clamp(AttackSlowPct, 0f, 0.9f),
            DamagePct = -Mathf.Clamp(DamageReduction, 0f, 0.9f),
        }, seconds > 0f ? seconds : 60f);
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

public sealed class Devour : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public int MaxUnitLevel => Mathf.Max(1, Mathf.RoundToInt(Data('A')));
    public float ChewSeconds => 5f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Type != null && target.Type.Level > MaxUnitLevel) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.KillUnit(target, Owner);
        Owner.State.Add(StateMod.Pause, ChewSeconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && (other.Type == null || other.Type.Level <= MaxUnitLevel));
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class TwoWayForm : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public string FormAId => (Type?.DataTextAt(Level, 'A') ?? "").Trim();

    public string FormBId
    {
        get
        {
            string unit = Type?.SummonAt(Level) ?? "";
            if (!string.IsNullOrWhiteSpace(unit)) return unit.Trim();
            return (Type?.DataTextAt(Level, 'B') ?? "").Trim();
        }
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner?.Type == null || !Owner.Alive) return false;

        bool inA = string.Equals(Owner.Type.Id, FormAId, System.StringComparison.OrdinalIgnoreCase);
        UnitType form = UnitCatalog.Get(inA ? FormBId : FormAId);
        return form != null && world.BeginMorph(Owner, form, 0f, disable: false);
    }

    public override bool TryAutoCast(World world) => false;
}

public sealed class Burrow : Ability
{
    public override bool IsToggle => true;

    public override void OnToggle(World world, bool on)
    {
        if (Owner == null) return;

        if (on)
        {
            Owner.State.Add(StateMod.Invisible);
            Owner.State.Add(StateMod.Disarm);
            return;
        }

        Owner.State.Remove(StateMod.Invisible);
        Owner.State.Remove(StateMod.Disarm);
    }
}

public sealed class PlagueToss : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;
    public override bool TargetsCorpse => true;

    public string PlagueUnitId => Type?.SummonAt(Level) ?? "uplg";
    public float Lifetime => Duration > 0f ? Duration : 10f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || target.Alive || !target.IsCorpse) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        Vector3 spot = target.Position;
        world.ConsumeCorpse(target);
        world.Summon(Owner, PlagueUnitId, spot, Lifetime);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit corpse = null;
        float best = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse || other.IsHero) continue;
            if (!InRange(other)) continue;
            float distance = Owner.DistanceTo(other);
            if (distance >= best) continue;
            best = distance;
            corpse = other;
        }
        if (corpse == null) return false;
        return world.TryCast(Owner, this, corpse, corpse.Position);
    }
}

public sealed class StoneForm : Ability
{
    public override bool IsToggle => true;

    public float ArmorBonus => Data('B');

    public override void OnToggle(World world, bool on)
    {
        if (Owner == null) return;

        if (on)
        {
            Owner.BonusArmor += ArmorBonus;
            Owner.State.Add(StateMod.Snare);
            Owner.State.Add(StateMod.Disarm);
            return;
        }

        Owner.BonusArmor -= ArmorBonus;
        Owner.State.Remove(StateMod.Snare);
        Owner.State.Remove(StateMod.Disarm);
    }
}

public sealed class UnholyFrenzy : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float AttackSpeedPct => Data('A');
    public float DrainPerSecond => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.FindBuff<UnholyFrenzyBuff>() != null) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new UnholyFrenzyBuff
        {
            AttackSpeedPct = AttackSpeedPct,
            DrainPerSecond = DrainPerSecond,
            DamageSource = Owner,
        }, Duration > 0f ? Duration : 45f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.IsBuilding || !ally.Alive || ally.IsHero) continue;
            if (ally.FindBuff<UnholyFrenzyBuff>() != null) continue;
            return world.TryCast(Owner, this, ally, ally.Position);
        }
        return false;
    }
}

public sealed class UnholyFrenzyBuff : StatBuff
{
    public Unit DamageSource;
    public float DrainPerSecond;

    private float _accumulator;

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (world == null || Owner == null || !Owner.Alive || DrainPerSecond <= 0f) return alive;

        _accumulator += delta;
        while (_accumulator >= 1f && Owner.Alive)
        {
            _accumulator -= 1f;
            world.DealDamage(World.MakeAbilityDamage(DamageSource, Owner, DrainPerSecond,
                AttackType.Spells, DamageType.Magic));
        }
        return alive && Owner.Alive;
    }
}
/// <summary>
/// 反魔法外壳（Aams）：物编 1.27 的 DataC=0，走「对所有的魔法免疫」这条，Dur/HeroDur 都是 90 秒；
/// 对召唤物不是加盾而是按 DataA 造成伤害并扣 DataD 法力（客户端 RuntimeAntiMagicShellAbility 同口径）。
/// </summary>
public sealed class AntiMagicShell : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float SummonDamage => Data('A');
    public float ManaLoss => Data('D');

    private float SecondsFor(Unit target) =>
        target != null && target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (!target.IsSummon && target.FindBuff<AntiMagicShellBuff>() != null) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        if (target.IsSummon)
        {
            target.Mana = Mathf.Max(0f, target.Mana - ManaLoss);
            if (SummonDamage > 0f)
            {
                world.DealDamage(World.MakeAbilityDamage(Owner, target, SummonDamage,
                    AttackType.Spells, DamageType.Magic));
            }
            return true;
        }

        float seconds = SecondsFor(target);
        world.ApplyBuff(target, new AntiMagicShellBuff(), seconds > 0f ? seconds : 90f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit best = null;
        float lowest = 1f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.IsBuilding || !ally.Alive || ally.FindBuff<AntiMagicShellBuff>() != null) continue;
            float ratio = ally.MaxHp > 0f ? ally.Hp / ally.MaxHp : 1f;
            if (ratio >= lowest) continue;
            lowest = ratio;
            best = ally;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}

/// <summary>反魔法外壳的载体：期间魔法免疫，到期摘掉。</summary>
public sealed class AntiMagicShellBuff : Buff
{
    public override bool IsHarmful => false;

    public override void OnApply(World world)
    {
        Owner?.State.Add(StateMod.MagicImmune, Remaining);
    }

    public override void OnRemove(World world)
    {
        Owner?.State.RemoveAll(StateMod.MagicImmune);
    }
}

/// <summary>
/// 占据（Apos）：抢一个非英雄、非飞行、等级不超过 DataA 的敌方单位，代价是女妖自己。
/// 转属在施法当帧完成，自毁延后一帧，避免施法管线在扣法力/进冷却时对着已死单位收尾。
/// </summary>
public sealed class Possession : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int MaxUnitLevel => Mathf.Max(0, Mathf.RoundToInt(Data('A')));

    private bool _consumeSelf;
    private float _consumeDelay;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding || target.IsUnderConstruction) return false;
        if (target.Type != null && (target.Type.Flying || target.Type.Level > MaxUnitLevel)) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        target.TransferTo(Owner.Player);
        target.AutoCast = Owner.AutoCast;
        _consumeSelf = true;
        _consumeDelay = World.Tick * 2f;
        return true;
    }

    public override void OnWorldTick(World world, float delta)
    {
        if (!_consumeSelf) return;

        _consumeDelay -= delta;
        if (_consumeDelay > 0f) return;

        _consumeSelf = false;
        if (Owner != null && Owner.Alive) world.KillUnit(Owner);
    }

    public override void OnOwnerDeath(World world)
    {
        _consumeSelf = false;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && other.Type != null
            && !other.Type.Flying && other.Type.Level <= MaxUnitLevel && other.Type.Level >= 2);
        return target != null && world.TryCast(Owner, this, target, target.Position);
    }
}
