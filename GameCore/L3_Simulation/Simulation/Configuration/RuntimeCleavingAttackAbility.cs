using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class RuntimeCleavingAttackAbility : Ability
{
    public override bool IsPassive => true;

    public float Ratio => Data('A');
    public float Radius => Area > 0f ? Area : 200f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (Ratio <= 0f || info.Amount <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, info.Amount * Ratio,
                AttackType.Normal, DamageType.Universal));
        }
    }
}
