using Godot;

namespace Launcher;

public sealed class MassDispel : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 200f;
    public float SummonDamage => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        Vector3 spot = Flatten(point);
        world.DispelArea(Owner, spot, Radius, hostile: true, SummonDamage);
        world.DispelArea(Owner, spot, Radius, hostile: false, 0f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 500f)) return false;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.HasBuff(harmful: true)) return world.TryCast(Owner, this, null, ally.Position);
        }
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (enemy.IsSummon || enemy.HasBuff(harmful: false)) return world.TryCast(Owner, this, null, enemy.Position);
        }
        return false;
    }
}
