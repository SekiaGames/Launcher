using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>模型自带的 RIBB 丝带：每个发射器一条点链，逐帧在世界空间重建网格。</summary>
public sealed class MdxRibbonRenderer
{
    private sealed class Emitter
    {
        public MeshInstance3D Node;
        public MeshInstance3D Draw;
        public ImmediateMesh Geometry;
        public float Above;
        public float Below;
        public float LifeSpan;
        public float EmissionRate;
        public float Gravity;
        public int Rows;
        public int Columns;
        public readonly List<Point> Points = new();
        public float Carry;
    }

    private sealed class Point
    {
        public Vector3 Above;
        public Vector3 Below;
        public Color Color;
        public float Age;
        public float Life;
        public float FallSpeed;
        public float Slot;
    }

    private const int MaxPoints = 32768;
    private const int MaxSpawnPerFrame = 64;

    private readonly Node3D _root;
    private readonly List<Emitter> _emitters = new();

    public int EmitterCount => _emitters.Count;

    public int PointCount { get; private set; }

    private MdxRibbonRenderer(Node3D root)
    {
        _root = root;
    }

    public static MdxRibbonRenderer Create(Node3D root, int teamColorIndex, War3FogConfig fog)
    {
        if (root == null || !GodotObject.IsInstanceValid(root)) return null;
        var renderer = new MdxRibbonRenderer(root);
        Texture2D teamTexture = teamColorIndex >= 0 ? TeamColorTextures.Get(teamColorIndex) : null;
        foreach (Node child in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is not MeshInstance3D mesh || !mesh.HasMeta("war3_ribbon_life_span")) continue;
            renderer.AddEmitter(mesh, teamTexture, fog);
        }
        return renderer._emitters.Count == 0 ? null : renderer;
    }

    private void AddEmitter(MeshInstance3D mesh, Texture2D teamTexture, War3FogConfig fog)
    {
        float life = mesh.GetMeta("war3_ribbon_life_span").AsSingle();
        float rate = mesh.GetMeta("war3_ribbon_emission_rate").AsSingle();
        if (life <= 0f || rate <= 0f) return;

        ShaderMaterial material = mesh.MaterialOverride as ShaderMaterial;
        string texture = mesh.HasMeta("war3_ribbon_texture")
            ? mesh.GetMeta("war3_ribbon_texture").AsString() : "";
        if (material != null)
        {
            material = (ShaderMaterial)material.Duplicate();
            if (teamTexture != null && texture.Contains("TeamColor", StringComparison.OrdinalIgnoreCase))
            {
                material.SetShaderParameter("ribbon_texture", teamTexture);
            }
            mesh.MaterialOverride = material;
            fog.Apply(material);
        }

        var geometry = new ImmediateMesh();
        var draw = new MeshInstance3D
        {
            Name = "ribbon",
            Mesh = geometry,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
        };
        draw.Transform = Transform3D.Identity;
        mesh.AddChild(draw);

        _emitters.Add(new Emitter
        {
            Node = mesh,
            Draw = draw,
            Geometry = geometry,
            Above = mesh.GetMeta("war3_ribbon_height_above").AsSingle(),
            Below = mesh.GetMeta("war3_ribbon_height_below").AsSingle(),
            LifeSpan = life,
            EmissionRate = rate,
            Gravity = mesh.HasMeta("war3_ribbon_gravity") ? mesh.GetMeta("war3_ribbon_gravity").AsSingle() : 0f,
            Rows = mesh.HasMeta("war3_ribbon_rows") ? Mathf.Max(1, mesh.GetMeta("war3_ribbon_rows").AsInt32()) : 1,
            Columns = mesh.HasMeta("war3_ribbon_columns")
                ? Mathf.Max(1, mesh.GetMeta("war3_ribbon_columns").AsInt32()) : 1,
        });
    }

    public void Tick(float delta, bool emitting)
    {
        if (_root == null || !GodotObject.IsInstanceValid(_root)) return;
        float step = Mathf.Clamp(delta, 0f, 0.1f);
        int total = 0;
        foreach (Emitter emitter in _emitters)
        {
            if (emitter.Node == null || !GodotObject.IsInstanceValid(emitter.Node)) continue;
            bool live = emitting && emitter.Node.IsVisibleInTree();
            if (!live) emitter.Carry = 0f;
            Advance(emitter, step);
            if (live && step > 0f) Emit(emitter, step);
            Rebuild(emitter);
            total += emitter.Points.Count;
        }
        PointCount = total;
    }

    public void Clear()
    {
        foreach (Emitter emitter in _emitters)
        {
            emitter.Points.Clear();
            emitter.Carry = 0f;
            if (emitter.Geometry != null && GodotObject.IsInstanceValid(emitter.Geometry))
            {
                emitter.Geometry.ClearSurfaces();
            }
        }
        PointCount = 0;
    }

    private static void Advance(Emitter emitter, float step)
    {
        for (int index = emitter.Points.Count - 1; index >= 0; index--)
        {
            Point point = emitter.Points[index];
            point.Age += step;
            if (point.Age >= point.Life)
            {
                emitter.Points.RemoveAt(index);
                continue;
            }
            if (emitter.Gravity == 0f || step <= 0f) continue;
            point.FallSpeed += emitter.Gravity * step;
            float fall = point.FallSpeed * step;
            point.Above.Y -= fall;
            point.Below.Y -= fall;
        }
    }

    private static void Emit(Emitter emitter, float step)
    {
        emitter.Carry += emitter.EmissionRate * step;
        int count = Math.Min(MaxSpawnPerFrame, (int)emitter.Carry);
        emitter.Carry -= count;
        if (count <= 0) return;

        Transform3D transform = emitter.Node.GlobalTransform;
        Vector3 up = transform.Basis.Y.Normalized();
        Vector3 origin = transform.Origin;
        Vector3 above = origin + up * emitter.Above;
        Vector3 below = origin - up * emitter.Below;
        var color = new Color(1f, 1f, 1f, 1f);
        for (int index = 0; index < count && emitter.Points.Count < MaxPoints; index++)
        {
            emitter.Points.Add(new Point
            {
                Above = above,
                Below = below,
                Color = color,
                Life = emitter.LifeSpan,
                Slot = 0f,
            });
        }
    }

    private static void Rebuild(Emitter emitter)
    {
        ImmediateMesh geometry = emitter.Geometry;
        if (geometry == null || !GodotObject.IsInstanceValid(geometry)) return;
        geometry.ClearSurfaces();
        if (emitter.Points.Count < 2) return;

        float columns = Math.Max(1, emitter.Columns);
        float rows = Math.Max(1, emitter.Rows);
        geometry.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int index = 1; index < emitter.Points.Count; index++)
        {
            Point a = emitter.Points[index - 1];
            Point b = emitter.Points[index];
            int slot = Math.Max(0, Mathf.RoundToInt(b.Slot));
            float cellU0 = (slot % (int)columns) / columns;
            float cellV0 = (slot / (int)columns) / rows;
            float cellV1 = cellV0 + 1f / rows;
            float ua = 1f - a.Age / a.Life;
            float ub = 1f - b.Age / b.Life;
            AddVertex(geometry, a.Above, Mathf.Lerp(cellU0, cellU0 + 1f / columns, ua), cellV0, a.Color);
            AddVertex(geometry, a.Below, Mathf.Lerp(cellU0, cellU0 + 1f / columns, ua), cellV1, a.Color);
            AddVertex(geometry, b.Below, Mathf.Lerp(cellU0, cellU0 + 1f / columns, ub), cellV1, b.Color);
            AddVertex(geometry, a.Above, Mathf.Lerp(cellU0, cellU0 + 1f / columns, ua), cellV0, a.Color);
            AddVertex(geometry, b.Below, Mathf.Lerp(cellU0, cellU0 + 1f / columns, ub), cellV1, b.Color);
            AddVertex(geometry, b.Above, Mathf.Lerp(cellU0, cellU0 + 1f / columns, ub), cellV0, b.Color);
        }
        geometry.SurfaceEnd();
    }

    private static void AddVertex(ImmediateMesh geometry, Vector3 position, float u, float v, Color color)
    {
        geometry.SurfaceSetUV(new Vector2(u, v));
        geometry.SurfaceSetColor(color);
        geometry.SurfaceAddVertex(position);
    }
}
