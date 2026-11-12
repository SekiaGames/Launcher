using Godot;

namespace Launcher;

public sealed class Pulverize : Ability
{
    public override bool IsPassive => true;

    public float ChancePercent => Data('A');
    public float Damage => Data('B');
    public float Radius => Data('C') > 0f ? Data('C') : 250f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null) return;
        if (Damage <= 0f || !world.RollPercent(ChancePercent)) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage, AttackType.Spells, DamageType.Magic));
        }
    }
}
