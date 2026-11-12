using Godot;

namespace Launcher;

public sealed class RuntimeTransmuteAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float GoldFactor => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        int gold = Mathf.Max(1, Mathf.RoundToInt(target.Type != null ? target.Type.GoldCost * GoldFactor : 0f));
        world.KillUnit(target, Owner);
        world.AwardGold(Owner.Player, gold, target);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && other.Type != null && other.Type.GoldCost >= 100);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
