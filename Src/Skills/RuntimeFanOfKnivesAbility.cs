using Godot;

namespace Launcher;

/// <summary>
/// 刀阵旋风 AEfk（守望者）。周围 Area(400/450/475) 内敌人各吃 DataA(75/125/190) 伤害（一把刀只扎一次）。
/// 物编：Cool=9 Cost=100。
/// </summary>
public sealed class RuntimeFanOfKnivesAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Area))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        int nearby = 0;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (Owner.DistanceTo(other) > Area) continue;
            nearby++;
        }
        return nearby >= 2 && world.TryCast(Owner, this, null, Owner.Position);
    }
}
