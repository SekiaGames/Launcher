using Godot;

namespace Launcher;

public sealed class RuntimeIncinerateAbility : Ability
{
    public override bool IsPassive => true;

    public float BonusDamage => Data('A');
    public float DamagePerSecond => Data('D');

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;

        if (BonusDamage > 0f)
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, info.Target, BonusDamage,
                AttackType.Spells, DamageType.Magic));
        }
        if (DamagePerSecond <= 0f || !info.Target.Alive) return;

        world.ApplyBuff(info.Target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
        }, Duration > 0f ? Duration : 2f);
    }
}
