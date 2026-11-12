using Godot;

namespace OpenWC3;

public sealed class RuntimePlagueAuraAbility : Ability
{
    public override bool IsPassive => true;

    public float Radius => Area > 0f ? Area : 250f;
    public float InfectionSeconds => Data('A') > 0f ? Data('A') : 120f;
    public float DamagePerSecond => Data('B') > 0f ? Data('B') : 1f;

    private float _timer;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        _timer += delta;
        if (_timer < 1f) return;
        _timer = 0f;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius, false))
        {
            if (enemy.FindBuff<VenomBuff>() != null) continue;
            world.ApplyBuff(enemy, new VenomBuff
            {
                DamageSource = Owner,
                DamagePerTick = DamagePerSecond,
                Interval = 1f,
            }, InfectionSeconds);
        }
    }
}
