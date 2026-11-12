using Godot;

namespace Launcher;

public sealed class RuntimePhoenixFireAbility : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('A');
    public float Radius => Area > 0f ? Area : 600f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Magic, DamageType.Magic));
        }
    }
}
