using Godot;

namespace OpenWC3;

public sealed class RevealArea : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        float radius = Area > 0f ? Area : 900f;
        float seconds = Duration > 0f ? Duration : 15f;
        world.AddFogReveal(Owner.Player, Owner.Position, radius, seconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Area > 0f ? Area : 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
