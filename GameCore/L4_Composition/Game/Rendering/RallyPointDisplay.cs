using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class RallyPointDisplay
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

            Node3D flag = Build(parent, unit, point, colorOf != null ? colorOf(unit.Player) : Colors.White);
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

    /// <summary>原版集结点是种族旗帜模型（Human 用 RallyPoint，兽/亡灵/暗夜各一面），换阵营时只换队伍色。</summary>
    private static Node3D Build(Node3D parent, Unit unit, Vector3 point, Color color)
    {
        Node3D root = null;
        PackedScene scene = Assets.LoadScene(SceneFor(unit?.Type?.Race));
        if (scene != null)
        {
            root = scene.Instantiate<Node3D>();
            if (root != null)
            {
                var tree = root.FindChild("AnimationTree", true, false) as AnimationTree;
                if (tree != null) tree.Active = false;
                UnitSceneComponent.ApplyTeamColorTo(root, color);
                MapWorldBuilder.ApplyUnitTextureFilter(root);
            }
        }

        if (root == null)
        {
            string race = unit?.Type?.Race ?? "?";
            if (MissingSceneLogged.Add(race)) GD.Print($"[RALLY] 缺旗帜模型（种族 {race}），用兜底旗杆");
            root = FallbackFlag(color);
        }
        root.Name = "RallyFlag";
        root.Position = point;
        parent.AddChild(root);
        return root;
    }

    private static readonly HashSet<string> MissingSceneLogged = new(StringComparer.OrdinalIgnoreCase);

    private static string SceneFor(string race)
    {
        string name = race?.Trim().ToLowerInvariant() switch
        {
            "orc" => "OrcRallyFlag",
            "undead" => "UndeadRallyFlag",
            "nightelf" => "NightElfRallyFlag",
            _ => "RallyPoint",
        };
        return "res://extract/UI/Feedback/RallyPoint/" + name + ".tscn";
    }

    /// <summary>模型缺失时的兜底：一根杆子加一面队伍色方块。</summary>
    private static Node3D FallbackFlag(Color color)
    {
        var root = new Node3D();
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 3f, BottomRadius = 3f, Height = 90f, RadialSegments = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.55f, 0.45f, 0.3f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
            Position = new Vector3(0f, 45f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(44f, 26f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            Position = new Vector3(24f, 76f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return root;
    }
}
