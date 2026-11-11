using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed class Ensnare : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        if (seconds <= 0f) seconds = 12f;

        target.State.Add(StateMod.Snare, seconds);
        target.ApplySlow(1f, 0f, seconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            other.Type != null && other.Type.Flying && !other.State.Snared);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}

public sealed class HealingWard : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public string WardUnitId => Type?.SummonAt(Level) ?? "ohwd";
    public float Lifetime => Duration > 0f ? Duration : 30f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        return world.Summon(Owner, WardUnitId, Flatten(point), Lifetime) != null;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class StasisTrap : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.AddGroundSpell(new StasisTrapSpell
        {
            Caster = Owner,
            Position = Flatten(point),
            TriggerRadius = Data('B') > 0f ? Data('B') : 250f,
            StunRadius = Data('C') > 0f ? Data('C') : 400f,
            StunSeconds = Data('D') > 0f ? Data('D') : 6f,
            Remaining = Duration > 0f ? Duration : 150f,
        });
        return true;
    }
}

public sealed class StasisTrapSpell : IGroundSpell
{
    public Unit Caster;
    public Vector3 Position;
    public float TriggerRadius = 250f;
    public float StunRadius = 400f;
    public float StunSeconds = 6f;
    public float Remaining = 150f;

    public bool Step(World world, float delta)
    {
        Remaining -= delta;
        if (Remaining <= 0f || world == null || Caster == null || !Caster.Alive) return false;

        bool triggered = false;
        foreach (Unit enemy in world.EnemiesInRange(Caster, Position, TriggerRadius, false))
        {
            triggered = true;
            break;
        }
        if (!triggered) return true;

        foreach (Unit victim in world.EnemiesInRange(Caster, Position, StunRadius))
        {
            if (victim.IsBuilding) continue;
            victim.State.Add(StateMod.Stun, StunSeconds);
        }
        return false;
    }
}

public sealed class SpiritLink : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float SplitRatio => Mathf.Clamp(Data('A'), 0f, 0.9f);
    public int MaxTargets => Mathf.Clamp(Mathf.RoundToInt(Data('B')), 1, 8);
    public float Radius => Area > 0f ? Area : 500f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        var members = new List<Unit> { Owner };
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (members.Count >= MaxTargets) break;
            if (ally == Owner || !ally.Alive || ally.IsBuilding) continue;
            if (ally.FindBuff<SpiritLinkBuff>() != null) continue;
            members.Add(ally);
        }
        if (members.Count < 2) return false;

        var group = new SpiritLinkGroup { Members = members, Ratio = SplitRatio };
        float seconds = Duration > 0f ? Duration : 75f;
        foreach (Unit member in members)
        {
            world.ApplyBuff(member, new SpiritLinkBuff { Group = group }, seconds);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;
        if (Owner.FindBuff<SpiritLinkBuff>() != null) return false;
        if (world.AlliesInRange(Owner, Owner.Position, Radius).Count < 2) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

public sealed class SpiritLinkGroup
{
    public List<Unit> Members = new();
    public float Ratio;
}

public sealed class SpiritLinkBuff : Buff
{
    public SpiritLinkGroup Group;

    public override bool IsHarmful => false;

    public override void ModifyIncomingDamage(World world, DamageInfo info)
    {
        if (world == null || info == null || info.Amount <= 0f || Group?.Members == null) return;

        var receivers = new List<Unit>();
        foreach (Unit member in Group.Members)
        {
            if (member == null || member == Owner || !member.Alive) continue;
            receivers.Add(member);
        }
        if (receivers.Count == 0) return;

        float share = info.Amount * Group.Ratio / receivers.Count;
        info.Amount -= share * receivers.Count;

        foreach (Unit receiver in receivers)
        {
            receiver.Hp -= share;
            receiver.View?.SetHp(receiver.Hp / Mathf.Max(1f, receiver.MaxHp), receiver.Alive);
            if (receiver.Hp <= 0f) world.KillUnit(receiver, info.Source);
        }
    }
}

public sealed class FreezingBreath : Ability
{
    public override bool IsPassive => true;

    public float MoveSlowPct => 0.5f;
    public float AttackSlowPct => 0.5f;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (!info.Target.IsBuilding) return;

        float seconds = Duration > 0f ? Duration : 5f;
        info.Target.ApplySlow(MoveSlowPct, AttackSlowPct, seconds);
    }
}