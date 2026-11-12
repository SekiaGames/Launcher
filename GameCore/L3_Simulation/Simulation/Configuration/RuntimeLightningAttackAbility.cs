using Godot;

namespace Launcher;

public sealed class LightningAttack : Ability
{
    public override bool IsPassive => true;

    public float FirstRatio => Data('A') > 0f ? Data('A') : 0.25f;
    public float NextRatio => Data('B') > 0f ? Data('B') : 0.25f;
    public float Radius => 500f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null) return;

        foreach (Unit other in world.EnemiesInRange(Owner, info.Target.Position, Radius))
        {
            if (other == info.Target || other.IsBuilding) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, other, info.Amount * FirstRatio,
                AttackType.Spells, DamageType.Magic));
        }
    }
}
