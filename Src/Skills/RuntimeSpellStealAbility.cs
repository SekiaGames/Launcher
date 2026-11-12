using Godot;

namespace Launcher;

public sealed class SpellSteal : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive || target == Owner) return false;
        if (target.IsBuilding) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        Buff stolen = null;
        for (int index = 0; index < target.Buffs.Count; index++)
        {
            Buff candidate = target.Buffs[index];
            if (candidate.IsHarmful) continue;
            stolen = candidate;
            target.Buffs.RemoveAt(index);
            break;
        }
        if (stolen == null) return false;

        stolen.OnRemove(world);
        stolen.Owner = Owner;
        Owner.Buffs.Add(stolen);
        stolen.OnApply(world);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => other.HasBuff(harmful: false));
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
