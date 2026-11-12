using Godot;

namespace OpenWC3;

public sealed class Devour : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public int MaxUnitLevel => Mathf.Max(1, Mathf.RoundToInt(Data('A')));
    public float ChewSeconds => 5f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Type != null && target.Type.Level > MaxUnitLevel) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.KillUnit(target, Owner);
        Owner.State.Add(StateMod.Pause, ChewSeconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && (other.Type == null || other.Type.Level <= MaxUnitLevel));
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
