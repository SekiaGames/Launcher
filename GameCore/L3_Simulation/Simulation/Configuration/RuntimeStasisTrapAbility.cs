using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed class StasisTrap : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.AddGroundSpell(new StasisTrapSpell
        {
            Caster = Owner,
            Position = Flatten(point),
            TriggerRadius = Data('B') > 0f ? Data('B') : 250f,
            StunRadius = Data('C') > 0f ? Data('C') : 400f,
            StunSeconds = Data('D') > 0f ? Data('D') : 6f,
            Remaining = Duration > 0f ? Duration : 150f,
        });
        return true;
    }
}
