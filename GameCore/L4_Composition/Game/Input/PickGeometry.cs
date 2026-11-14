using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 射线与模型网格求交，对标客户端 CharacterRenderer.IntersectRayPicking：
/// 包围盒只用来粗筛（省掉远处的活），命中一定来自三角面。
/// 网格数据按 Mesh 缓存一次（同一模型的单位共用），包围盒按节点缓存局部值、每帧乘世界变换。
/// </summary>
internal static class PickGeometry
{
    private readonly struct Surface
    {
        public readonly Vector3[] Vertices;
        public readonly int[] Indices;

        public Surface(Vector3[] vertices, int[] indices)
        {
            Vertices = vertices;
            Indices = indices;
        }
    }

    private static readonly Dictionary<ulong, Aabb> LocalBounds = new();
    private static readonly Dictionary<ulong, Surface[]> MeshSurfaces = new();
    private static readonly List<(MeshInstance3D Mesh, Aabb Bounds)> Instances = new();

    internal static bool TryRayBounds(Node3D root, Vector3 origin, Vector3 direction, out float entry)
    {
        entry = 0f;
        if (root == null || !root.IsInsideTree()) return false;
        if (!TryLocalBounds(root, out Aabb local)) return false;
        return RayIntersectsBounds(origin, direction, TransformBounds(root.GlobalTransform, local), out entry);
    }

    internal static bool TryRayHit(Node3D root, Vector3 origin, Vector3 direction, out float distance)
    {
        distance = float.MaxValue;
        if (root == null || !root.IsInsideTree()) return false;

        CollectInstances(root);
        foreach ((MeshInstance3D mesh, _) in Instances)
        {
            Surface[] surfaces = SurfacesOf(mesh.Mesh);
            if (surfaces == null) continue;
            Transform3D transform = mesh.GlobalTransform;
            foreach (Surface surface in surfaces)
            {
                Vector3[] vertices = surface.Vertices;
                int[] indices = surface.Indices;
                int count = indices.Length > 0 ? indices.Length : vertices.Length;
                for (int index = 0; index + 2 < count; index += 3)
                {
                    int a = indices.Length > 0 ? indices[index] : index;
                    int b = indices.Length > 0 ? indices[index + 1] : index + 1;
                    int c = indices.Length > 0 ? indices[index + 2] : index + 2;
                    if (a >= vertices.Length || b >= vertices.Length || c >= vertices.Length) continue;
                    if (!RayTriangle(origin, direction, transform * vertices[a], transform * vertices[b],
                            transform * vertices[c], out float hit)) continue;
                    if (hit < distance) distance = hit;
                }
            }
        }
        return distance < float.MaxValue;
    }

    private static void CollectInstances(Node3D root)
    {
        Instances.Clear();
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            if (!mesh.Visible || mesh.Transparency >= 1f) continue;
            Instances.Add((mesh, mesh.GetAabb()));
        }
    }

    private static bool TryLocalBounds(Node3D root, out Aabb bounds)
    {
        ulong key = root.GetInstanceId();
        if (LocalBounds.TryGetValue(key, out bounds)) return true;

        CollectInstances(root);
        bool any = false;
        bounds = default;
        foreach ((MeshInstance3D mesh, Aabb local) in Instances)
        {
            Aabb relative = RelativeBounds(root, mesh, local);
            bounds = any ? bounds.Merge(relative) : relative;
            any = true;
        }
        LocalBounds[key] = bounds;
        return any;
    }

    private static Aabb RelativeBounds(Node3D root, MeshInstance3D mesh, Aabb local)
    {
        Transform3D transform = root.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        return TransformBounds(transform, local);
    }

    private static Surface[] SurfacesOf(Mesh mesh)
    {
        if (mesh == null) return null;
        ulong key = mesh.GetInstanceId();
        if (MeshSurfaces.TryGetValue(key, out Surface[] cached)) return cached;

        var surfaces = new List<Surface>();
        for (int index = 0; index < mesh.GetSurfaceCount(); index++)
        {
            Godot.Collections.Array arrays = mesh.SurfaceGetArrays(index);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (vertices.Length < 3) continue;
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            surfaces.Add(new Surface(vertices, indices));
        }
        var result = surfaces.ToArray();
        MeshSurfaces[key] = result;
        return result;
    }

    private static Aabb TransformBounds(Transform3D transform, Aabb bounds)
    {
        Vector3 lo = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 hi = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int corner = 0; corner < 8; corner++)
        {
            var point = new Vector3(
                (corner & 1) == 0 ? bounds.Position.X : bounds.End.X,
                (corner & 2) == 0 ? bounds.Position.Y : bounds.End.Y,
                (corner & 4) == 0 ? bounds.Position.Z : bounds.End.Z);
            Vector3 world = transform * point;
            lo = new Vector3(Mathf.Min(lo.X, world.X), Mathf.Min(lo.Y, world.Y), Mathf.Min(lo.Z, world.Z));
            hi = new Vector3(Mathf.Max(hi.X, world.X), Mathf.Max(hi.Y, world.Y), Mathf.Max(hi.Z, world.Z));
        }
        return new Aabb(lo, hi - lo);
    }

    private static bool RayIntersectsBounds(Vector3 origin, Vector3 direction, Aabb bounds, out float entry)
    {
        entry = 0f;
        float near = 0f, far = float.MaxValue;
        for (int axis = 0; axis < 3; axis++)
        {
            float start = origin[axis];
            float delta = direction[axis];
            float min = bounds.Position[axis];
            float max = bounds.End[axis];
            if (Mathf.Abs(delta) < 1e-6f)
            {
                if (start < min || start > max) return false;
                continue;
            }
            float first = (min - start) / delta;
            float second = (max - start) / delta;
            if (first > second) (first, second) = (second, first);
            near = Mathf.Max(near, first);
            far = Mathf.Min(far, second);
            if (near > far) return false;
        }
        entry = near;
        return true;
    }

    private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b,
        Vector3 c, out float distance)
    {
        distance = 0f;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 pvec = direction.Cross(edge2);
        float determinant = edge1.Dot(pvec);
        if (Mathf.Abs(determinant) < 1e-8f) return false;

        float inverse = 1f / determinant;
        Vector3 tvec = origin - a;
        float u = tvec.Dot(pvec) * inverse;
        if (u < 0f || u > 1f) return false;

        Vector3 qvec = tvec.Cross(edge1);
        float v = direction.Dot(qvec) * inverse;
        if (v < 0f || u + v > 1f) return false;

        float hit = edge2.Dot(qvec) * inverse;
        if (hit <= 0f) return false;
        distance = hit;
        return true;
    }
}
