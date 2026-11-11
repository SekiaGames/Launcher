using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class RallyFlagView
{
    private sealed class Flag
    {
        public Node3D Root;
        public Vector3 Point;
    }

    private readonly Dictionary<Unit, Flag> _flags = new();

    public void Tick(Node3D parent, IReadOnlyList<Unit> units, Func<Unit, bool> show, Func<int, Color> colorOf)
    {
        var alive = new HashSet<Unit>();
        foreach (Unit unit in units)
        {
            if (unit == null || !unit.Alive || !unit.IsBuilding) continue;
            if (unit.RallyPoint == null) continue;
            if (show != null && !show(unit)) continue;

            alive.Add(unit);
            Vector3 point = unit.RallyPoint.Value;
            if (_flags.TryGetValue(unit, out Flag existing))
            {
                if (existing.Point.DistanceTo(point) < 1f) continue;
                existing.Root.QueueFree();
                _flags.Remove(unit);
            }

            Node3D flag = Build(parent, point, colorOf != null ? colorOf(unit.Player) : Colors.White);
            _flags[unit] = new Flag { Root = flag, Point = point };
        }

        var stale = new List<Unit>();
        foreach (KeyValuePair<Unit, Flag> pair in _flags)
        {
            if (!alive.Contains(pair.Key)) stale.Add(pair.Key);
        }
        foreach (Unit unit in stale)
        {
            Flag flag = _flags[unit];
            if (flag.Root != null && GodotObject.IsInstanceValid(flag.Root)) flag.Root.QueueFree();
            _flags.Remove(unit);
        }
    }

    private static Node3D Build(Node3D parent, Vector3 point, Color color)
    {
        var root = new Node3D { Name = "RallyFlag", Position = point };

        var poleMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.45f, 0.3f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 3f, BottomRadius = 3f, Height = 90f, RadialSegments = 6 },
            MaterialOverride = poleMaterial,
            Position = new Vector3(0f, 45f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        var flagMaterial = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(44f, 26f) },
            MaterialOverride = flagMaterial,
            Position = new Vector3(24f, 76f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        parent.AddChild(root);
        return root;
    }
}