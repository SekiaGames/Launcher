using Godot;
using System.Collections.Generic;

namespace OpenWC3;

public sealed class Frenzy : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new StatBuff
        {
            AttackSpeedPct = Data('A'),
            MoveSpeedPct = Data('B'),
        }, Duration > 0f ? Duration : 20f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 800f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
