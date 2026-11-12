using Godot;

namespace Launcher;

public sealed class RuntimeOrbOfAnnihilationAbility : Ability
{
    public override bool IsPassive => true;

    public float BonusDamage => Data('A');
    public float SplashRatio => Data('B');
    public float Radius => Area > 0f ? Area : 150f;

    public override float ModifyOutgoingAttack(World world, Unit target, float raw) => raw + BonusDamage;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (SplashRatio <= 0f) return;

        float damage = BonusDamage * SplashRatio;
        if (damage <= 0f) return;

        foreach (Unit other in world.EnemiesInRange(Owner, info.Target.Position, Radius, false))
        {
            if (other == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, other, damage, AttackType.Spells, DamageType.Magic));
        }
    }
}
