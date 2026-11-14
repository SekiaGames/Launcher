using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class MapVoidRenderer
{
    private const float Extend = 32768f;
    private const float FadeDistance = 192f;
    private const float SampleStep = 128f;
    private static readonly Vector3 VoidColor = new(7f / 255f, 9f / 255f, 8f / 255f);
    private const float EdgeBoost = 5.5f;

    public MeshInstance3D Node { get; private set; }

    public static MapVoidRenderer TryCreate(Node3D parent, TerrainData terrain,
        Func<float, float, float> surfaceHeightAt)
    {
        if (parent == null || terrain == null || surfaceHeightAt == null) return null;
        Shader shader = GD.Load<Shader>("res://assets/shaders/void_map.gdshader");
        if (shader == null)
        {
            GD.PushWarning("[VOID] 缺 void_map.gdshader，需要重新导出 extract.pck");
            return null;
        }

        float x0 = 0f, y0 = 0f;
        float x1 = (terrain.Width - 1) * MapWorldBuilder.CellSize;
        float y1 = (terrain.Height - 1) * MapWorldBuilder.CellSize;

        float lowZ = float.MaxValue;
        void SampleEdge(float ax, float ay, float bx, float by)
        {
            float length = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / SampleStep));
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float z = surfaceHeightAt(ax + (bx - ax) * t, ay + (by - ay) * t) - 1f;
                if (z < lowZ) lowZ = z;
            }
        }
        SampleEdge(x0, y0, x0, y1);
        SampleEdge(x0, y1, x1, y1);
        SampleEdge(x1, y1, x1, y0);
        SampleEdge(x1, y0, x0, y0);

        var points = new List<Vector3>();
        var gradients = new List<float>();

        void Add(float x, float y, float z, float t)
        {
            points.Add(new Vector3(0f - x, z, y));
            gradients.Add(t);
        }

        void Strip(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
        {
            Add(ax, ay, lowZ, 1f);
            Add(bx, by, lowZ, 1f);
            Add(cx, cy, lowZ, 1f);
            Add(ax, ay, lowZ, 1f);
            Add(cx, cy, lowZ, 1f);
            Add(dx, dy, lowZ, 1f);
        }

        Strip(x0, y1, x1, y1, x1, y1 + Extend, x0, y1 + Extend);
        Strip(x1, y0, x0, y0, x0, y0 - Extend, x1, y0 - Extend);
        Strip(x1, y1, x1, y0, x1 + Extend, y0, x1 + Extend, y1);
        Strip(x0, y0, x0, y1, x0 - Extend, y1, x0 - Extend, y0);
        Strip(x0 - Extend, y1, x0, y1, x0, y1 + Extend, x0 - Extend, y1 + Extend);
        Strip(x1, y1, x1 + Extend, y1, x1 + Extend, y1 + Extend, x1, y1 + Extend);
        Strip(x0, y0, x0 - Extend, y0, x0 - Extend, y0 - Extend, x0, y0 - Extend);
        Strip(x1 + Extend, y0, x1, y0, x1, y0 - Extend, x1 + Extend, y0 - Extend);

        void Edge(float ax, float ay, float bx, float by, float nx, float ny)
        {
            float length = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / SampleStep));
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                float ix0 = ax + (bx - ax) * t0, iy0 = ay + (by - ay) * t0;
                float ix1 = ax + (bx - ax) * t1, iy1 = ay + (by - ay) * t1;
                float z0 = surfaceHeightAt(ix0, iy0) - 1f;
                float z1 = surfaceHeightAt(ix1, iy1) - 1f;
                float ox0 = ix0 + nx * FadeDistance, oy0 = iy0 + ny * FadeDistance;
                float ox1 = ix1 + nx * FadeDistance, oy1 = iy1 + ny * FadeDistance;
                Add(ix0, iy0, z0, 0f);
                Add(ix1, iy1, z1, 0f);
                Add(ox1, oy1, lowZ, 1f);
                Add(ix0, iy0, z0, 0f);
                Add(ox1, oy1, lowZ, 1f);
                Add(ox0, oy0, lowZ, 1f);
            }
        }
        Edge(x0, y0, x0, y1, -1f, 0f);
        Edge(x0, y1, x1, y1, 0f, 1f);
        Edge(x1, y1, x1, y0, 1f, 0f);
        Edge(x1, y0, x0, y0, 0f, -1f);

        var uvs = new Vector2[gradients.Count];
        for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(gradients[i], 0f);

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("void_color", VoidColor);
        material.SetShaderParameter("edge_boost", EdgeBoost);

        var node = new MeshInstance3D
        {
            Name = "MapVoid",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(node);
        GD.Print($"[VOID] 裙边 {points.Count} 顶点，lowZ={lowZ:0}，矩形 ({x0:0},{y0:0})-({x1:0},{y1:0})");
        return new MapVoidRenderer { Node = node };
    }
}
