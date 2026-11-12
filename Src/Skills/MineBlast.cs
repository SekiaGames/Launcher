using Godot;
using System;

namespace OpenWC3;

/// <summary>地雷爆炸（Amnx）：死亡时对 Area/DataA 内敌人结算 DataB 伤害。</summary>
public sealed class MineBlast : Ability
{
    public override bool IsPassive => true;

    public override void OnOwnerDeath(World world)
    {
        if (world == null || Owner == null) return;

        float radius = Data('A') > 0f ? Data('A') : 150f;
        float damage = Data('B') > 0f ? Data('B') : 400f;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, damage, AttackType.Spells, DamageType.Universal));
        }
    }
}
