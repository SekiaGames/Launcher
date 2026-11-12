using Godot;

namespace OpenWC3;

public sealed class RuntimeAbsorbManaAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float MaxTransfer => Data('B') > 0f ? Data('B') : float.MaxValue;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Mana <= 0f) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float transfer = Mathf.Min(target.Mana, MaxTransfer);
        target.Mana -= transfer;
        Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + transfer);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => other.Mana >= 10f);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
