using Godot;

namespace OpenWC3.Game;

public sealed class RuntimePlagueTossAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;
    public override bool TargetsCorpse => true;

    public string PlagueUnitId => Type?.SummonAt(Level) ?? "uplg";
    public float Lifetime => Duration > 0f ? Duration : 10f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || target.Alive || !target.IsCorpse) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        Vector3 spot = target.Position;
        world.ConsumeCorpse(target);
        world.Summon(Owner, PlagueUnitId, spot, Lifetime);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit corpse = null;
        float best = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse || other.IsHero) continue;
            if (!InRange(other)) continue;
            float distance = Owner.DistanceTo(other);
            if (distance >= best) continue;
            best = distance;
            corpse = other;
        }
        if (corpse == null) return false;
        return world.TryCast(Owner, this, corpse, corpse.Position);
    }
}
