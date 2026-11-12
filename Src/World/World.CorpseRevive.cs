using Godot;

namespace OpenWC3;

public sealed partial class World
{
    public int ReviveCorpsesAround(Vector3 center, float radius, int max, int player, float lifetime, float lifeFactor)
    {
        if (max <= 0 || radius <= 0f) return 0;

        int revived = 0;
        for (int index = 0; index < Units.Count; index++)
        {
            Unit unit = Units[index];
            if (unit == null || unit.Alive || !unit.IsCorpse || unit.IsHero) continue;

            float dx = center.X - unit.Position.X;
            float dz = center.Z - unit.Position.Z;
            if (dx * dx + dz * dz > radius * radius) continue;

            unit.ReviveFromCorpse(unit.Position, lifeFactor);
            unit.TransferTo(player);
            unit.Summoner = null;
            unit.SummonRemaining = lifetime;

            revived++;
            if (revived >= max) break;
        }
        return revived;
    }
}

public sealed class CloakOfFlames : Ability
{
    public override bool IsPassive => true;

    public float Radius => Area > 0f ? Area : 160f;
    public float DamagePerSecond => Data('A');

    private float _accumulator;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive || DamagePerSecond <= 0f) return;

        _accumulator += delta;
        while (_accumulator >= 1f)
        {
            _accumulator -= 1f;
            foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
            {
                world.DealDamage(World.MakeAbilityDamage(Owner, victim, DamagePerSecond,
                    AttackType.Spells, DamageType.Magic));
            }
        }
    }
}