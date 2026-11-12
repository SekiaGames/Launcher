using Godot;

namespace Launcher;

public sealed class RocketAttack : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('A');
    public float Radius => 250f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (info.Target.Type == null || !info.Target.Type.Flying) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target || victim.Type == null || !victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Siege, DamageType.Normal));
        }
    }
}
