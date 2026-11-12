using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

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
