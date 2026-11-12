using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeAcidBombAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Radius => Area > 0f ? Area : 200f;
    public float ImpactDamage => Data('C');
    public float DamagePerSecond => Data('D');
    public float ArmorPenalty => Data('E');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = Duration > 0f ? Duration : 15f;
        foreach (Unit victim in world.EnemiesInRange(Owner, target.Position, Radius))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, ImpactDamage,
                AttackType.Spells, DamageType.Magic));
            if (!victim.Alive) continue;

            world.ApplyBuff(victim, new VenomBuff
            {
                DamageSource = Owner,
                DamagePerTick = DamagePerSecond,
                Interval = 1f,
            }, seconds);

            if (ArmorPenalty > 0f)
            {
                world.ApplyBuff(victim, new CurseBuff { Armor = -ArmorPenalty }, seconds);
            }
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
