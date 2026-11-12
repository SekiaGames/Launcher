using Godot;

namespace Launcher;

/// <summary>Stand Down（Astd）：把地洞里的人全放出来。</summary>
public sealed class RuntimeStandDownAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.UnloadAllCargo(Owner);
    }

    public override bool TryAutoCast(World world) => false;
}
