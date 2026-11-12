using Godot;

namespace Launcher;

public sealed class AbolishMagic : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float SummonDamage => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        bool enemy = !world.AreAllied(Owner.Player, target.Player);
        world.DispelBuffs(target, Owner, harmful: !enemy, helpful: enemy, SummonDamage);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner.Mana < ManaCost) return false;
        float range = CastRange > 0f ? CastRange : 500f;

        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, range))
        {
            if (ally == Owner || ally.IsBuilding || !ally.Alive) continue;
            if (!ally.HasBuff(harmful: true)) continue;
            return world.TryCast(Owner, this, ally, ally.Position);
        }

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, range))
        {
            if (enemy.IsBuilding) continue;
            if (!enemy.HasBuff(harmful: false) && !enemy.IsSummon) continue;
            return world.TryCast(Owner, this, enemy, enemy.Position);
        }
        return false;
    }
}
