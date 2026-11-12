using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed class Ensnare : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        if (seconds <= 0f) seconds = 12f;

        target.State.Add(StateMod.Snare, seconds);
        target.ApplySlow(1f, 0f, seconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        bool anyTarget = string.Equals(Id, "Aent", System.StringComparison.OrdinalIgnoreCase);
        Unit target = FindEnemyInRange(world, accept: other =>
            (anyTarget || (other.Type != null && other.Type.Flying)) && !other.State.Snared);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
