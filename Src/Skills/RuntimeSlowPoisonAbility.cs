using Godot;

namespace OpenWC3;

public sealed class RuntimeSlowPoisonAbility : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');
    public float MoveSlowPct => Data('B');
    public float AttackSlowPct => Data('C');

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;

        float seconds = info.Target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(info.Target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
            MoveSlowPct = MoveSlowPct,
            AttackSlowPct = AttackSlowPct,
        }, seconds > 0f ? seconds : 5f);
    }
}
