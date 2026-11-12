using Godot;

namespace Launcher;

public sealed class RuntimePhaseShiftAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        float seconds = Duration > 0f ? Duration : 1.5f;
        Owner.State.Add(StateMod.Invisible, seconds);
        Owner.State.Add(StateMod.Pause, seconds);
        return true;
    }
}
