using Godot;

namespace OpenWC3;

public sealed class FlakCannon : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('A');
    public float Radius => 300f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (info.Target.Type == null || !info.Target.Type.Flying) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target || victim.Type == null || !victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Spells, DamageType.Magic));
        }
    }
}
