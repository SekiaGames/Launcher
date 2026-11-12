using Godot;

namespace OpenWC3;

public sealed class AntiMagicShield : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Absorb => Data('C');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.FindBuff<AntiMagicShieldBuff>() != null) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new AntiMagicShieldBuff
        {
            Caster = Owner,
            Absorb = Absorb,
        }, Duration > 0f ? Duration : 90f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit best = null;
        float lowest = 1f;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 500f))
        {
            if (ally.IsBuilding || !ally.Alive || ally.FindBuff<AntiMagicShieldBuff>() != null) continue;
            float ratio = ally.MaxHp > 0f ? ally.Hp / ally.MaxHp : 1f;
            if (ratio >= lowest) continue;
            lowest = ratio;
            best = ally;
        }
        if (best == null) return false;
        return world.TryCast(Owner, this, best, best.Position);
    }
}
