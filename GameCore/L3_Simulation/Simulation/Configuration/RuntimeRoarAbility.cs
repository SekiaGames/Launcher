using Godot;

namespace Launcher;

public sealed class Roar : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float DamagePct => Data('A');
    public float Radius => Area > 0f ? Area : 500f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (ally.IsBuilding) continue;
            world.ApplyBuff(ally, new StatBuff { DamagePct = DamagePct }, Duration > 0f ? Duration : 45f);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Radius)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
