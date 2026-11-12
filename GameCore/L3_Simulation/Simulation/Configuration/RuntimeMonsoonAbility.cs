using Godot;

namespace Launcher;

public sealed class Monsoon : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public float Damage => Data('A');
    public float SlowPct => Mathf.Clamp(Data('C'), 0f, 0.9f);
    public float Radius => Area > 0f ? Area : 300f;

    public override float ChannelWaveInterval => Data('B') > 0.2f ? Data('B') : 1.5f;
    public override float ChannelTotal => Duration > 0f ? Duration : 15f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage,
                AttackType.Spells, DamageType.Magic));
            if (victim.Alive && SlowPct > 0f) victim.ApplySlow(SlowPct, 0f, 2f);
        }
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 500f)) return false;
        return world.TryCast(Owner, this, null, Owner.Target?.Position ?? Owner.Position);
    }
}
