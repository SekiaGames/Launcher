using Godot;

namespace OpenWC3;

public sealed class FragShards : Ability
{
    public override bool IsPassive => true;

    public float SplashDamage => Data('C');
    public float Radius => Area > 0f ? Area : 100f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (SplashDamage <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (victim == info.Target) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, SplashDamage,
                AttackType.Siege, DamageType.Normal));
        }
    }
}
