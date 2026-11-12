using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

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
