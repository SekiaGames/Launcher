using Godot;

namespace OpenWC3;

public sealed class CloudOfFogSpell : IGroundSpell
{
    public Unit Caster;
    public Vector3 Position;
    public float Radius = 300f;
    public float Remaining = 30f;
    public float MissChance = 0.7f;

    private float _accumulator;

    public bool Step(World world, float delta)
    {
        Remaining -= delta;
        if (Remaining <= 0f || world == null || Caster == null || !Caster.Alive) return false;

        _accumulator += delta;
        if (_accumulator < 0.5f) return true;
        _accumulator = 0f;

        foreach (Unit victim in world.EnemiesInRange(Caster, Position, Radius))
        {
            if (victim.IsBuilding) continue;
            world.ApplyBuff(victim, new CurseBuff { MissChance = MissChance }, 1.5f);
        }
        return true;
    }
}
