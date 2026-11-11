using Godot;
using System;

namespace Launcher;

public sealed partial class World
{
    public const float BlightHealPerSecond = 2f;

    public bool IsBlighted(Vector3 point)
    {
        for (int index = 0; index < Units.Count; index++)
        {
            Unit unit = Units[index];
            if (unit == null || !unit.Alive || !unit.IsBuilding) continue;
            float suppress = unit.BlightSuppressRadius;
            if (suppress <= 0f) continue;
            float dx0 = point.X - unit.Position.X;
            float dz0 = point.Z - unit.Position.Z;
            if (dx0 * dx0 + dz0 * dz0 <= suppress * suppress) return false;
        }

        for (int index = 0; index < Units.Count; index++)
        {
            Unit unit = Units[index];
            if (unit == null || !unit.Alive || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            if (unit.Type == null || !string.Equals(unit.Type.Race, "undead", StringComparison.OrdinalIgnoreCase)) continue;

            float radius = unit.BlightRadius > 0f ? unit.BlightRadius : MeleeRules.BlightRadius;
            float dx = point.X - unit.Position.X;
            float dz = point.Z - unit.Position.Z;
            if (dx * dx + dz * dz <= radius * radius) return true;
        }
        return false;
    }

    public bool IsUndeadUnit(Unit unit) =>
        unit?.Type != null && string.Equals(unit.Type.Race, "undead", StringComparison.OrdinalIgnoreCase);
}