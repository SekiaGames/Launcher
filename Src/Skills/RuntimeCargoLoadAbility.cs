using Godot;

namespace Launcher;

/// <summary>装载（Aloa 运输船 / Slo3 战船）：点一个友军把它装进来。</summary>
public sealed class LoadCargoOrder : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.CanEnterCargo(Owner, target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null) return false;
        if (!world.CanEnterCargo(Owner, target)) return false;
        return world.LoadCargo(Owner, target);
    }

    public override bool TryAutoCast(World world) => false;
}
