using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeFlareAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        float radius = Area > 0f ? Area : 1800f;
        float seconds = Duration > 0f ? Duration : 15f;
        world.AddFogReveal(Owner.Player, Flatten(point), radius, seconds);
        return true;
    }
}
