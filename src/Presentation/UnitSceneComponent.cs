using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class UnitSceneComponent
{
    private static readonly Color[] PlayerColors =
    {
        Color.FromHtml("c8322b"), Color.FromHtml("0042ff"), Color.FromHtml("1ce6b9"),
        Color.FromHtml("540081"), Color.FromHtml("fffc01"), Color.FromHtml("fe8a0e"),
        Color.FromHtml("20c000"), Color.FromHtml("e55bb0"), Color.FromHtml("959697"),
        Color.FromHtml("7ebff1"), Color.FromHtml("106246"), Color.FromHtml("4e2a04"),
    };

    private static ArrayMesh _ringMesh;

    public Node3D Root { get; private set; }
    public AnimationPlayer Player { get; private set; }
    public float Height { get; private set; } = 100f;
    public MeshInstance3D BarFill { get; private set; }
    public Node3D BarRoot { get; private set; }
    public MeshInstance3D Ring { get; private set; }
    public string CurrentAnimation { get; private set; }

    public MeshInstance3D ProgressNode { get; private set; }
    private ImmediateMesh _progressMesh;
    private float _progressRadius;
    private float _progressDrawn = -1f;

    public static UnitSceneComponent Create(PackedScene scene, Color teamColor, float scale, bool isBuilding, float footprintRadius = 0f)
    {
        var view = new UnitSceneComponent();
        var root = scene.Instantiate<Node3D>();
        root.Scale = Vector3.One * scale;
        view.Root = root;

        var tree = root.FindChild("AnimationTree", true, false) as AnimationTree;
        if (tree != null) tree.Active = false;
        view.Player = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;

        view.ApplyTeamColor(teamColor);
        MapWorldBuilder.ApplyUnitTextureFilter(root);

        view.Height = MeasureHeight(root, isBuilding);
        view._progressRadius = Mathf.Max(40f, footprintRadius);
        view.BuildBar(isBuilding);
        view.BuildRing(isBuilding);
        if (isBuilding) view.BuildProgressRing();
        return view;
    }

    public static Color ColorOf(int player)
    {
        return PlayerColors[Mathf.Abs(player) % PlayerColors.Length];
    }

    /// <summary>换阵营时重刷模型上的队伍色。</summary>
    public void SetTeamColor(Color color) => ApplyTeamColor(color);

    private void ApplyTeamColor(Color color)
    {
        foreach (Node node in Root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;

            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = mesh.MaterialOverride
                    ?? mesh.GetSurfaceOverrideMaterial(surface)
                    ?? mesh.Mesh.SurfaceGetMaterial(surface);
                if (material is not ShaderMaterial shader) continue;
                if (!shader.GetShaderParameter("use_team_color").AsBool()) continue;

                ShaderMaterial copy = (ShaderMaterial)shader.Duplicate();
                copy.SetShaderParameter("team_color", color);

                if (mesh.MaterialOverride != null && mesh.MaterialOverride == material)
                {
                    mesh.MaterialOverride = copy;
                }
                else
                {
                    mesh.SetSurfaceOverrideMaterial(surface, copy);
                }
            }
        }
    }

    private static float MeasureHeight(Node3D root, bool isBuilding)
    {
        float top = 0f;
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;

            Aabb box = mesh.Mesh.GetAabb();
            Transform3D local = RelativeTransform(root, mesh);
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? box.Position.X : box.End.X,
                    (corner & 2) == 0 ? box.Position.Y : box.End.Y,
                    (corner & 4) == 0 ? box.Position.Z : box.End.Z);
                top = Mathf.Max(top, (local * point).Y);
            }
        }

        if (top <= 1f) top = isBuilding ? 300f : 120f;
        return top * root.Scale.Y;
    }

    private static Transform3D RelativeTransform(Node3D root, Node3D child)
    {
        Transform3D transform = child.Transform;
        Node parent = child.GetParent();
        while (parent != null && !ReferenceEquals(parent, root))
        {
            if (parent is Node3D parent3D) transform = parent3D.Transform * transform;
            parent = parent.GetParent();
        }
        return transform;
    }

    private void BuildBar(bool isBuilding)
    {
        float width = isBuilding ? 90f : 56f;
        BarRoot = new Node3D { Name = "HpBar", Position = new Vector3(0f, Height + 24f, 0f), Visible = false };
        Root.AddChild(BarRoot);

        var background = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(width + 3f, 9f) },
            MaterialOverride = BarMaterial(Color.FromHtml("141414")),
            Name = "Bg",
        };
        BarRoot.AddChild(background);

        BarFill = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(width, 5f) },
            MaterialOverride = BarMaterial(Color.FromHtml("3ce03c")),
            Name = "Fill",
        };
        BarFill.Position = new Vector3(0f, 0f, 0.5f);
        BarRoot.AddChild(BarFill);
    }

    private static StandardMaterial3D BarMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };
    }

    private void BuildRing(bool isBuilding)
    {
        _ringMesh ??= BuildRingMesh();
        Ring = new MeshInstance3D
        {
            Mesh = _ringMesh,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = Color.FromHtml("3ce03c"),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
            Scale = Vector3.One * (isBuilding ? 3.2f : 0.9f),
            Visible = false,
            Name = "SelectionRing",
        };
        Ring.Position = new Vector3(0f, 6f, 0f);
        Root.AddChild(Ring);
    }

    private static ArrayMesh BuildRingMesh()
    {
        const int segments = 32;
        const float inner = 40f;
        const float outer = 52f;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.Tau * i / segments;
            float a1 = Mathf.Tau * (i + 1) / segments;
            var p0 = new Vector3(Mathf.Cos(a0) * outer, 0f, Mathf.Sin(a0) * outer);
            var p1 = new Vector3(Mathf.Cos(a1) * outer, 0f, Mathf.Sin(a1) * outer);
            var q0 = new Vector3(Mathf.Cos(a0) * inner, 0f, Mathf.Sin(a0) * inner);
            var q1 = new Vector3(Mathf.Cos(a1) * inner, 0f, Mathf.Sin(a1) * inner);
            st.AddVertex(p0);
            st.AddVertex(p1);
            st.AddVertex(q1);
            st.AddVertex(p0);
            st.AddVertex(q1);
            st.AddVertex(q0);
        }
        return st.Commit();
    }

    public void Play(string kind)
    {
        if (Player == null) return;

        string found = PickAnimation(kind);
        if (found == null || found == CurrentAnimation) return;

        CurrentAnimation = found;
        Player.Play(found);
        if (kind is "Stand" or "Walk")
        {
            Player.GetAnimation(found).LoopMode = Animation.LoopModeEnum.Linear;
        }
    }

    private string PickAnimation(string kind)
    {
        string[] names = Player.GetAnimationList();
        foreach (string name in names)
        {
            if (string.Equals(name, kind, StringComparison.OrdinalIgnoreCase)) return name;
        }
        foreach (string name in names)
        {
            if (name.StartsWith(kind, StringComparison.OrdinalIgnoreCase)) return name;
        }
        return null;
    }

    private void BuildProgressRing()
    {
        _progressMesh = new ImmediateMesh();
        ProgressNode = new MeshInstance3D
        {
            Mesh = _progressMesh,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.35f, 1f, 0.45f, 0.55f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = false,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            },
            Position = new Vector3(0f, 12f, 0f),
            Visible = false,
            Name = "BuildProgress",
        };
        Root.AddChild(ProgressNode);
    }

    private void UpdateProgressRing(float progress)
    {
        if (_progressMesh == null) return;

        if (progress >= 1f)
        {
            ProgressNode.Visible = false;
            _progressDrawn = -1f;
            return;
        }

        ProgressNode.Visible = true;
        if (Mathf.Abs(progress - _progressDrawn) < 0.02f) return;
        _progressDrawn = progress;

        // 客户端是贴着建筑底边的一圈细环，不是实心圆盘
        const int segments = 48;
        int filled = Mathf.Max(1, Mathf.CeilToInt(segments * Mathf.Clamp(progress, 0f, 1f)));
        float outer = _progressRadius * 1.02f;
        float inner = outer * 0.90f;

        _progressMesh.ClearSurfaces();
        _progressMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        _progressMesh.SurfaceSetColor(new Color(0.35f, 1f, 0.45f, 0.55f));
        for (int i = 0; i < filled; i++)
        {
            float a0 = Mathf.Tau * i / segments - Mathf.Pi * 0.5f;
            float a1 = Mathf.Tau * (i + 1) / segments - Mathf.Pi * 0.5f;

            var i0 = new Vector3(Mathf.Cos(a0) * inner, 0f, Mathf.Sin(a0) * inner);
            var o0 = new Vector3(Mathf.Cos(a0) * outer, 0f, Mathf.Sin(a0) * outer);
            var i1 = new Vector3(Mathf.Cos(a1) * inner, 0f, Mathf.Sin(a1) * inner);
            var o1 = new Vector3(Mathf.Cos(a1) * outer, 0f, Mathf.Sin(a1) * outer);

            _progressMesh.SurfaceAddVertex(i0);
            _progressMesh.SurfaceAddVertex(o0);
            _progressMesh.SurfaceAddVertex(o1);

            _progressMesh.SurfaceAddVertex(i0);
            _progressMesh.SurfaceAddVertex(o1);
            _progressMesh.SurfaceAddVertex(i1);
        }
        _progressMesh.SurfaceEnd();
    }

    public void SetBuildProgress(float progress)
    {
        UpdateProgressRing(progress);
        if (Player == null) return;

        string found = PickAnimation("Birth");
        if (found == null) return;

        if (CurrentAnimation != found)
        {
            CurrentAnimation = found;
            Player.Play(found);
        }

        Animation animation = Player.GetAnimation(found);
        if (animation == null) return;

        Player.Seek((double)(Mathf.Clamp(progress, 0f, 1f) * (float)animation.Length), true);
        Player.Pause();
    }

    public void SetHp(float ratio, bool alive)
    {
        if (BarFill == null) return;

        float clamped = Mathf.Clamp(ratio, 0f, 1f);
        BarFill.Scale = new Vector3(Mathf.Max(0.01f, clamped), 1f, 1f);
        BarFill.Position = new Vector3(-(1f - clamped) * 28f, 0f, 0.5f);
        ((StandardMaterial3D)BarFill.MaterialOverride).AlbedoColor = clamped > 0.6f
            ? Color.FromHtml("3ce03c")
            : clamped > 0.3f ? Color.FromHtml("e0d03c") : Color.FromHtml("e03c3c");
    }

    public void SetBarVisible(bool visible)
    {
        if (BarRoot != null) BarRoot.Visible = visible;
    }

    public void SetSelected(bool selected)
    {
        if (Ring != null) Ring.Visible = selected;
    }
}