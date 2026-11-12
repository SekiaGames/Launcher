using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>永久献祭（地狱火 ANpi）：每 Dur 秒对 Area 内敌方地面单位结算 DataA。</summary>
public sealed class RuntimePermanentImmolationAbility : Ability
{
    public override bool IsPassive => true;

    private float _pulseTimer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        float interval = Duration > 0f ? Duration : 1f;
        _pulseTimer += delta;
        if (_pulseTimer < interval) return;
        _pulseTimer -= interval;

        float damage = Data('A');
        if (damage <= 0f) return;

        float radius = Area > 0f ? Area : 220f;
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, radius, hitBuildings: false))
        {
            if (victim.Type != null && victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, damage, AttackType.Spells, DamageType.Magic));
        }
    }
}
