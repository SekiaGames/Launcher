using Godot;

namespace Launcher;

public sealed class Detonate : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 300f;
    public float Damage => Data('A');
    public float SummonDamage => Data('B');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.DispelArea(Owner, Owner.Position, Radius, hostile: true, SummonDamage);
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage,
                AttackType.Spells, DamageType.Magic));
        }
        world.KillUnit(Owner);
        return true;
    }
}
