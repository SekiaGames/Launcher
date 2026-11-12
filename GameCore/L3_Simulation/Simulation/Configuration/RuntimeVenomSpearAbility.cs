using Godot;

namespace Launcher;

public sealed class RuntimeVenomSpearAbility : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');
    public float SlowPct => Data('B');

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (DamagePerSecond <= 0f) return;

        float seconds = info.Target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(info.Target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
            MoveSlowPct = SlowPct,
        }, seconds > 0f ? seconds : 25f);
    }
}
