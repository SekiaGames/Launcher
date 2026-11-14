using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 可通行面（桥/平台）的垂直高度查询。
///
/// 对应客户端 <c>DoodadRenderer.TryGetWalkableHeight</c>：地图里 <c>walkable=1</c> 的
/// 装饰物/可破坏物（桥面板、地精跳板…）的**模型表面**才是那里的通行高度，
/// 地形层高度只是桥下的地面。客户端用垂直射线打模型三角面取最高命中点；
/// 这里在进树后把三角面烘成世界坐标（桥是静态的，不会动），查询时做一次
/// 2D 点内测试 + 重心插值 —— 结果与客户端逐点同口径，但不必每帧遍历网格。
/// </summary>
public sealed class WalkableSurfaceProbe
{
    private sealed class Surface
    {
        public Vector3[] World;      // 世界坐标顶点（三个一组：三角形汤）
        public float MinX, MaxX, MinZ, MaxZ;
    }

    private readonly List<Node3D> _pending = new();
    private readonly List<Surface> _surfaces = new();
    private bool _built;

    public int SurfaceCount => _surfaces.Count;

    public void Add(Node3D node)
    {
        if (node != null) _pending.Add(node);
    }

    public bool TryGetHeight(float worldX, float worldZ, out float height)
    {
        height = float.NegativeInfinity;
        EnsureBuilt();
        for (int i = 0; i < _surfaces.Count; i++)
        {
            Surface surface = _surfaces[i];
            if (worldX < surface.MinX - 1f || worldX > surface.MaxX + 1f
                || worldZ < surface.MinZ - 1f || worldZ > surface.MaxZ + 1f) continue;
            Vector3[] v = surface.World;
            for (int t = 0; t + 2 < v.Length; t += 3)
            {
                if (!TryTriangleHeight(v[t], v[t + 1], v[t + 2], worldX, worldZ, out float y)) continue;
                if (y > height) height = y;
            }
        }
        return float.IsFinite(height);
    }

    /// <summary>三角形上 (x,z) 处的高度：2D 重心坐标 + 线性插值 Y。</summary>
    private static bool TryTriangleHeight(Vector3 a, Vector3 b, Vector3 c,
        float x, float z, out float y)
    {
        y = 0f;
        float area = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
        if (MathF.Abs(area) < 1e-6f) return false;
        float w1 = ((b.X - x) * (c.Z - z) - (c.X - x) * (b.Z - z)) / area;
        float w2 = ((c.X - x) * (a.Z - z) - (a.X - x) * (c.Z - z)) / area;
        float w0 = 1f - w1 - w2;
        // 顶部封盖的三角形朝上；用极小的负容差吃掉边缘浮点误差。
        if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) return false;
        y = w0 * a.Y + w1 * b.Y + w2 * c.Y;
        return true;
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        foreach (Node3D node in _pending)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            Bake(node);
        }
        _pending.Clear();
        if (_surfaces.Count > 0)
            GD.Print($"[BRIDGE] 可通行面 {_surfaces.Count} 块（walkable 装饰物）");
    }

    private void Bake(Node3D node)
    {
        var meshes = new List<MeshInstance3D>();
        if (node is MeshInstance3D self) meshes.Add(self);
        foreach (Node child in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is MeshInstance3D mesh && mesh.Mesh != null) meshes.Add(mesh);
        }

        foreach (MeshInstance3D mesh in meshes)
        {
            Mesh source = mesh.Mesh;
            if (source == null) continue;
            Transform3D transform = mesh.GlobalTransform;
            for (int surfaceIndex = 0; surfaceIndex < source.GetSurfaceCount(); surfaceIndex++)
            {
                Godot.Collections.Array arrays = source.SurfaceGetArrays(surfaceIndex);
                if (arrays.Count <= (int)Mesh.ArrayType.Vertex) continue;
                if (arrays[(int)Mesh.ArrayType.Vertex].VariantType != Variant.Type.PackedVector3Array) continue;
                Vector3[] local = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                if (local.Length < 3) continue;

                int[] indices = null;
                if (arrays.Count > (int)Mesh.ArrayType.Index
                    && arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.PackedInt32Array)
                {
                    indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                }

                var world = new Vector3[indices != null ? indices.Length - indices.Length % 3 : local.Length - local.Length % 3];
                if (world.Length < 3) continue;
                for (int i = 0; i < world.Length; i += 3)
                {
                    world[i] = transform * local[indices?[i] ?? i];
                    world[i + 1] = transform * local[indices?[i + 1] ?? i + 1];
                    world[i + 2] = transform * local[indices?[i + 2] ?? i + 2];
                }

                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (Vector3 point in world)
                {
                    minX = MathF.Min(minX, point.X); maxX = MathF.Max(maxX, point.X);
                    minZ = MathF.Min(minZ, point.Z); maxZ = MathF.Max(maxZ, point.Z);
                }
                _surfaces.Add(new Surface { World = world, MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ });
            }
        }
    }
}
