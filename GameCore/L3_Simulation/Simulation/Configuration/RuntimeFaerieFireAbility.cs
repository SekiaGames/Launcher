using Godot;

namespace Launcher;

public sealed class RuntimeFaerieFireAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float ArmorPenalty => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(target, new FaerieFireBuff { ArmorPenalty = ArmorPenalty },
            seconds > 0f ? seconds : 90f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => other.FindBuff<FaerieFireBuff>() == null);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
