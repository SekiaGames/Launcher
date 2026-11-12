using Godot;
using System.Collections.Generic;

namespace OpenWC3;

public sealed class RuntimeSpiritLinkAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float SplitRatio => Mathf.Clamp(Data('A'), 0f, 0.9f);
    public int MaxTargets => Mathf.Clamp(Mathf.RoundToInt(Data('B')), 1, 8);
    public float Radius => Area > 0f ? Area : 500f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        var members = new List<Unit> { Owner };
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (members.Count >= MaxTargets) break;
            if (ally == Owner || !ally.Alive || ally.IsBuilding) continue;
            if (ally.FindBuff<RuntimeSpiritLinkBuff>() != null) continue;
            members.Add(ally);
        }
        if (members.Count < 2) return false;

        var group = new SpiritLinkGroup { Members = members, Ratio = SplitRatio };
        float seconds = Duration > 0f ? Duration : 75f;
        foreach (Unit member in members)
        {
            world.ApplyBuff(member, new RuntimeSpiritLinkBuff { Group = group }, seconds);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;
        if (Owner.FindBuff<RuntimeSpiritLinkBuff>() != null) return false;
        if (world.AlliesInRange(Owner, Owner.Position, Radius).Count < 2) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
