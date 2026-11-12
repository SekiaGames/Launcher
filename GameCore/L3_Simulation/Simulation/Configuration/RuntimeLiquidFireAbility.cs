using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeLiquidFireAbility : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');
    public float SlowPct => Data('C');
    public float Seconds => Duration > 0f ? Duration : 3f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (!info.Target.IsBuilding) return;

        Unit target = info.Target;
        if (SlowPct > 0f) target.ApplySlow(Mathf.Clamp(SlowPct, 0f, 1f), Mathf.Clamp(SlowPct, 0f, 1f), Seconds);
        if (DamagePerSecond <= 0f) return;

        world.ApplyBuff(target, new PeriodicDamageBuff
        {
            DamageSource = Owner,
            AmountPerTick = DamagePerSecond,
            Interval = 1f,
            AttackType = AttackType.Spells,
            DamageType = DamageType.Magic,
        }, Seconds);
    }
}
