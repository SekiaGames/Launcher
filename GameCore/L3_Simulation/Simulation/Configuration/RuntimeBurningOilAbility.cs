using Godot;

namespace Launcher;

public sealed class RuntimeBurningOilAbility : Ability
{
    public override bool IsPassive => true;

    public float HitDamage => Data('A');
    public float BurnPerSecond => Data('C');
    public float BurnSeconds => Duration > 0f ? Duration : 2.5f;
    public float Radius => Area > 0f ? Area : 150f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (HitDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, HitDamage, AttackType.Spells, DamageType.Magic));
            if (BurnPerSecond <= 0f) continue;

            world.ApplyBuff(victim, new PeriodicDamageBuff
            {
                DamageSource = Owner,
                AmountPerTick = BurnPerSecond,
                Interval = 1f,
                AttackType = AttackType.Spells,
                DamageType = DamageType.Magic,
            }, BurnSeconds);
        }
    }
}
