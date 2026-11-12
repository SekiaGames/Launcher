using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

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
