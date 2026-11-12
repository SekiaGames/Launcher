using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 战争践踏 AOws（牛头人酋长）。周围 Area(250/300/350) 内地面单位吃 DataA 伤害并眩晕 Dur(3/4/5) 秒。
/// 物编：Cool=6 Cost=90。
/// </summary>
public sealed class RuntimeWarStompAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Area))
        {
            if (victim.Type != null && victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
            if (victim.Alive) victim.State.Add(StateMod.Stun, Duration);
        }
        return true;
    }

    /// <summary>AI：身边有敌人就踩（一次至少两个目标才不会亏）。</summary>
    public override bool TryAutoCast(World world)
    {
        int nearby = 0;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.Type == null || other.Type.Flying) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (Owner.DistanceTo(other) > Area) continue;
            nearby++;
        }
        return nearby >= 2 && world.TryCast(Owner, this, null, Owner.Position);
    }
}
