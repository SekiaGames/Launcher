using Godot;

namespace OpenWC3;

public sealed class RuntimeCorrosiveBreathAbility : Ability
{
    public override bool IsPassive => true;

    public float DamagePerSecond => Data('A');

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (!info.Target.IsBuilding || DamagePerSecond <= 0f) return;

        world.ApplyBuff(info.Target, new PeriodicDamageBuff
        {
            DamageSource = Owner,
            AmountPerTick = DamagePerSecond,
            Interval = 1f,
            AttackType = AttackType.Spells,
            DamageType = DamageType.Magic,
        }, Duration > 0f ? Duration : 5f);
    }
}
