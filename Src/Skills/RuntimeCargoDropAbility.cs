using Godot;

namespace Launcher;

/// <summary>卸载（Adro 全部卸下 / Sdro 战船卸下 / Adri 瞬时卸下）：把人摆回船周围。</summary>
public sealed class RuntimeCargoDropAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.Cargo.UnloadAllCargo(Owner);
    }

    public override bool TryAutoCast(World world) => false;
}
