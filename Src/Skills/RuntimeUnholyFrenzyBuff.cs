using Godot;

namespace Launcher;

public sealed class UnholyFrenzyBuff : StatBuff
{
    public Unit DamageSource;
    public float DrainPerSecond;

    private float _accumulator;

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (world == null || Owner == null || !Owner.Alive || DrainPerSecond <= 0f) return alive;

        _accumulator += delta;
        while (_accumulator >= 1f && Owner.Alive)
        {
            _accumulator -= 1f;
            world.DealDamage(World.MakeAbilityDamage(DamageSource, Owner, DrainPerSecond,
                AttackType.Spells, DamageType.Magic));
        }
        return alive && Owner.Alive;
    }
}
