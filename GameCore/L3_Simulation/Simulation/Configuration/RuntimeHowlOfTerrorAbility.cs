using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class RuntimeHowlOfTerrorAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float DamageReduction => Mathf.Clamp(Data('A'), 0f, 1f);
    public float ArmorReduction => Data('B');
    public float Radius => Area > 0f ? Area : 500f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            float seconds = enemy.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
            world.ApplyBuff(enemy, new CurseBuff
            {
                DamagePct = -DamageReduction,
                Armor = -ArmorReduction,
            }, seconds > 0f ? seconds : 15f);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Radius)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
