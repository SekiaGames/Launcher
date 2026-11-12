using Godot;

namespace OpenWC3;

/// <summary>
/// 单位推挤：按 UnitBalance.collision 半径分离重叠单位。对标客户端 Src/Core/World.RepulseMembers.cs。
/// 采集/施工中的单位作为锚点不动，由对方承担全部位移；建筑不参与（它们已经是路径阻挡）。
/// </summary>
public sealed partial class World
{
    public void TickSeparation()
    {
        foreach (Unit unit in Units)
        {
            if (!unit.Alive || unit.IsBuilding || unit.IsCargoed) continue;
            if (unit.SepCooldown > 0)
            {
                unit.SepCooldown--;
                continue;
            }

            if (HasMovementOrder(unit) || IsRepulseAnchored(unit))
            {
                unit.SepVelocityX = 0f;
                unit.SepVelocityZ = 0f;
                unit.SepCooldown = SeparationCooldownTicks;
                continue;
            }

            int profile = SeparationProfileOf(unit);
            float radius = SeparationRadiusCells(profile);
            float sumX = unit.SepVelocityX / 32f;
            float sumZ = unit.SepVelocityZ / 32f;

            foreach (Unit other in Units)
            {
                if (other == unit || !other.Alive || other.IsCargoed) continue;
                if (other.Type.Flying != unit.Type.Flying) continue;
                if ((other.Type.MoveType == "float") != (unit.Type.MoveType == "float")) continue;

                float dx = (unit.Position.X - other.Position.X) / 32f;
                float dz = (unit.Position.Z - other.Position.Z) / 32f;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance >= radius || distance <= 0.001f) continue;

                float t = 1f - distance / radius;
                float magnitude = SeparationStrength * t * t;
                sumX += dx * (magnitude / distance);
                sumZ += dz * (magnitude / distance);
            }

            float scaled = SeparationHistoryGain * Mathf.Sqrt(sumX * sumX + sumZ * sumZ);
            if (scaled <= SeparationDeadZone)
            {
                unit.SepVelocityX = 0f;
                unit.SepVelocityZ = 0f;
                continue;
            }

            float length = Mathf.Sqrt(sumX * sumX + sumZ * sumZ);
            float cap = profile == 0 ? 0.5f : 0.2f;
            if (scaled > cap) scaled = cap;
            unit.SepVelocityX = sumX * (scaled / length) * 32f;
            unit.SepVelocityZ = sumZ * (scaled / length) * 32f;

            float x = unit.Position.X + unit.SepVelocityX;
            float z = unit.Position.Z + unit.SepVelocityZ;
            PathLayer layer = unit.Type?.PathingLayer ?? PathLayer.Ground;
            if (_pathGrid != null && _pathGrid.IsBlockedWorld(x, z, layer)) continue;

            float y = SurfaceY(unit.Type, x, z);
            unit.Position = new Vector3(x, y, z);
            if (unit.View != null) unit.View.Root.Position = unit.Position;
            unit.SepCooldown = SeparationCooldownTicks;
        }
    }

    private const int SeparationCooldownTicks = 7;
    private const float SeparationStrength = 0.4f;
    private const float SeparationHistoryGain = 0.7f;
    private const float SeparationDeadZone = 0.01f;

    private static bool HasMovementOrder(Unit unit)
    {
        if (unit.MoveTarget.HasValue || unit.Target != null) return true;
        if (unit.Path != null && unit.PathIndex < unit.Path.Count) return true;
        return unit.BuildingNow || unit.RepairTarget != null;
    }

    private static int SeparationProfileOf(Unit unit)
    {
        if (unit.Radius <= 20f) return 0;
        if (unit.Radius <= 40f) return 1;
        return 4;
    }

    private static float SeparationRadiusCells(int profile) => profile switch
    {
        0 => 5f,
        1 => 7f,
        _ => 10f,
    };

    public void ResolveRepulse()
    {
        for (int i = 0; i < Units.Count; i++)
        {
            Unit a = Units[i];
            if (!a.Alive || a.IsBuilding) continue;

            for (int j = i + 1; j < Units.Count; j++)
            {
                Unit b = Units[j];
                if (!b.Alive || b.IsBuilding) continue;
                if (a.Type.Flying != b.Type.Flying) continue;
                if ((a.Type.MoveType == "float") != (b.Type.MoveType == "float")) continue;

                float min = a.Radius + b.Radius;
                float dx = b.Position.X - a.Position.X;
                float dz = b.Position.Z - a.Position.Z;
                float distanceSquared = dx * dx + dz * dz;
                if (distanceSquared >= min * min) continue;

                float distance = Mathf.Sqrt(distanceSquared);
                if (distance < 0.01f)
                {
                    dx = 1f;
                    dz = 0f;
                    distance = 1f;
                }

                bool aAnchored = IsRepulseAnchored(a);
                bool bAnchored = IsRepulseAnchored(b);
                if (aAnchored && bAnchored) continue;

                float overlap = min - distance;
                float nx = dx / distance;
                float nz = dz / distance;
                float aShare = bAnchored ? 1f : aAnchored ? 0f : 0.5f;

                Push(a, -nx * overlap * aShare, -nz * overlap * aShare);
                Push(b, nx * overlap * (1f - aShare), nz * overlap * (1f - aShare));
            }
        }
    }

    private static bool IsRepulseAnchored(Unit unit)
    {
        return unit.GoldMine != null || unit.Tree != null || unit.BuildSite != null || unit.IsUnderConstruction;
    }

    private void Push(Unit unit, float deltaX, float deltaZ)
    {
        if (deltaX == 0f && deltaZ == 0f) return;

        float x = unit.Position.X + deltaX;
        float z = unit.Position.Z + deltaZ;
        PathLayer layer = unit.Type?.PathingLayer ?? PathLayer.Ground;
        if (_pathGrid != null && _pathGrid.IsBlockedWorld(x, z, layer)) return;

        float y = SurfaceY(unit.Type, x, z);
        unit.Position = new Vector3(x, y, z);
        if (unit.View != null) unit.View.Root.Position = unit.Position;
    }
}
