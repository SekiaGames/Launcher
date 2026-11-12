using Godot;

namespace Launcher;

public sealed class CloudOfFog : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float Radius => Area > 0f ? Area : 300f;
    public float Lifetime => Duration > 0f ? Duration : 30f;
    public float MissChance => 0.7f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.AddGroundSpell(new CloudOfFogSpell
        {
            Caster = Owner,
            Position = Flatten(point),
            Radius = Radius,
            Remaining = Lifetime,
            MissChance = MissChance,
        });
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 1000f)) return false;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 1000f))
        {
            if (enemy.Type == null || !enemy.Type.Flying) continue;
            return world.TryCast(Owner, this, null, enemy.Position);
        }
        return false;
    }
}
