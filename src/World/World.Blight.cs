using Godot;
using System;

namespace Launcher;

public sealed partial class World
{
    public const float BlightHealPerSecond = 2f;

    public bool IsBlighted(Vector3 point)
    {
        float radius = MeleeRules.BlightRadius;
        float radiusSq = radius * radius;

        for (int index = 0; index < Units.Count; index++)
        {
            Unit unit = Units[index];
            if (unit == null || !unit.Alive || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            if (unit.Type == null || !string.Equals(unit.Type.Race, "undead", StringComparison.OrdinalIgnoreCase)) continue;

            float dx = point.X - unit.Position.X;
            float dz = point.Z - unit.Position.Z;
            if (dx * dx + dz * dz <= radiusSq) return true;
        }
        return false;
    }

    public bool IsUndeadUnit(Unit unit) =>
        unit?.Type != null && string.Equals(unit.Type.Race, "undead", StringComparison.OrdinalIgnoreCase);
}