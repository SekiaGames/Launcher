using Godot;

namespace Launcher;

/// <summary>
/// 单位推挤：按 UnitBalance.collision 半径分离重叠单位。对标客户端 Src/Core/World.RepulseMembers.cs。
/// 采集/施工中的单位作为锚点不动，由对方承担全部位移；建筑不参与（它们已经是路径阻挡）。
/// </summary>
public sealed partial class World
{
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
        if (_pathGrid != null && _pathGrid.IsBlockedWorld(x, z)) return;

        float y = _terrain != null ? MapWorldBuilder.HeightAt(_terrain, x, z) : unit.Position.Y;
        unit.Position = new Vector3(x, y, z);
        if (unit.View != null) unit.View.Root.Position = unit.Position;
    }
}
