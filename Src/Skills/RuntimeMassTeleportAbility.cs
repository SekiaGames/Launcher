using Godot;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 群体传送 AHmt（大法师终极）。点全图任意地面：把施法者 Area（700）内最多 DataA（24）个友军
/// 一起挪到落点（自己站中心，其余围一圈）。物编 Cool=20 Cost=100 Rng=99999。
/// </summary>
public sealed class RuntimeMassTeleportAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int MaxUnits => Mathf.Clamp(Mathf.RoundToInt(Data('A')), 1, 24);
    public float GatherRadius => Area > 0f ? Area : 700f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        var group = new List<Unit> { Owner };
        foreach (Unit other in world.Units)
        {
            if (group.Count >= MaxUnits) break;
            if (other == null || !other.Alive || ReferenceEquals(other, Owner)) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (other.IsBuilding) continue;
            if (Owner.DistanceTo(other) > GatherRadius) continue;
            group.Add(other);
        }

        Vector3 landing = Flatten(point);
        for (int index = 0; index < group.Count; index++)
        {
            float ring = index == 0 ? 0f : 96f + ((index - 1) / 6) * 64f;
            float angle = index == 0 ? 0f : Mathf.Tau * (index - 1) / 6f;
            var spot = new Vector3(
                landing.X + Mathf.Cos(angle) * ring,
                landing.Y,
                landing.Z + Mathf.Sin(angle) * ring);

            Unit unit = group[index];
            unit.CancelAttack();
            unit.Target = null;
            unit.MoveTarget = null;
            unit.Path = null;
            world.MoveUnitTo(unit, spot);
        }
        return true;
    }
}
