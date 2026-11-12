using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeSentinelAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public float SightRadius => Data('B') > 0f ? Data('B') : 900f;
    public float Lifetime => Data('A') > 0f ? Data('A') : 100f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        world.AddFogReveal(Owner.Player, Flatten(point), SightRadius, Lifetime);
        return true;
    }
}
