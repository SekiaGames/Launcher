using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    public const float CellSize = 128f;

    public const float WaterLevel = 14f;

    public TerrainData Terrain { get; private set; }

    public Node3D Root { get; private set; }

    public float WaterY { get; private set; } = 14f;

    public List<TreeState> Trees { get; } = new List<TreeState>();

    public WalkableSurfaceProbe Walkable { get; } = new();

    public bool TryGetWalkableHeight(float worldX, float worldZ, out float height)
        => Walkable.TryGetHeight(worldX, worldZ, out height);

    public Node3D Build(TerrainData terrain, War3MapDoodads doodads, Color waterBaseColor, War3FogConfig fog)
    {
        Terrain = terrain;
        Root = new Node3D
        {
            Name = "MapRoot"
        };
        bool[,] covered = BuildCliffs(terrain);
        BuildTerrain(terrain, covered);
        BuildWater(terrain, waterBaseColor, fog);
        MapVoidRenderer.TryCreate(Root, terrain,
            (x, z) => Mathf.Max(HeightAt(terrain, 0f - x, z), WaterHeightAt(terrain, 0f - x, z)));
        new ShorelineWaveRenderer(Root, terrain, War3WaterConfig.Load(terrain.Tileset));
        if (doodads != null)
        {
            BuildDoodads(terrain, doodads);
        }
        return Root;
    }

    public void TickWater(double delta)
    {
        if (_waterMaterial != null)
        {
            _waterElapsed += Math.Max(0.0, delta);
            int num = Mathf.FloorToInt((float)_waterElapsed * (float)_waterFrameRate) + _waterFrameOffset;
            num %= _waterFrameCount;
            if (num < 0) num += _waterFrameCount;
            _waterMaterial.SetShaderParameter("frame", (float)num);
        }
    }

    public static float HeightAt(TerrainData terrain, float worldX, float worldZ)
    {
        float x = (0f - worldX) / 128f;
        float y = worldZ / 128f;
        return InterpolatedHeight(terrain, x, y);
    }

    public static float SurfaceHeightAt(TerrainData terrain, float worldX, float worldZ)
    {
        if (terrain == null) return 0f;
        float gridX = (0f - worldX) / CellSize;
        float gridZ = worldZ / CellSize;
        int x = Mathf.Clamp(Mathf.FloorToInt(gridX), 0, Mathf.Max(0, terrain.Width - 2));
        int y = Mathf.Clamp(Mathf.FloorToInt(gridZ), 0, Mathf.Max(0, terrain.Height - 2));
        float u = Mathf.Clamp(gridX - x, 0f, 1f);
        float v = Mathf.Clamp(gridZ - y, 0f, 1f);

        float h00 = CornerSurfaceHeight(terrain, x, y);
        float h10 = CornerSurfaceHeight(terrain, x + 1, y);
        float h01 = CornerSurfaceHeight(terrain, x, y + 1);
        float h11 = CornerSurfaceHeight(terrain, x + 1, y + 1);
        return u + v >= 1f
            ? h11 * (u + v - 1f) + h10 * (1f - v) + h01 * (1f - u)
            : h00 * (1f - u - v) + h10 * u + h01 * v;
    }

    private static float CornerSurfaceHeight(TerrainData terrain, int x, int y)
        => GroundWorld(terrain.Corners[y * terrain.Width + x]) + RampLift(terrain, x, y) * CellSize;

    private static readonly Dictionary<char, float> _waterOffsets = new();

    public static float WaterPlaneHeightAt(TerrainData terrain, float worldX, float worldZ)
    {
        if (terrain == null) return 0f;
        float gridX = Mathf.Clamp((0f - worldX) / CellSize, 0f, Mathf.Max(0, terrain.Width - 1));
        float gridZ = Mathf.Clamp(worldZ / CellSize, 0f, Mathf.Max(0, terrain.Height - 1));
        int x0 = Mathf.Min(Mathf.FloorToInt(gridX), Mathf.Max(0, terrain.Width - 2));
        int z0 = Mathf.Min(Mathf.FloorToInt(gridZ), Mathf.Max(0, terrain.Height - 2));
        int x1 = Mathf.Min(x0 + 1, terrain.Width - 1);
        int z1 = Mathf.Min(z0 + 1, terrain.Height - 1);
        float u = Mathf.Clamp(gridX - x0, 0f, 1f);
        float v = Mathf.Clamp(gridZ - z0, 0f, 1f);
        float w00 = terrain.Corners[z0 * terrain.Width + x0].WaterWorld;
        float w10 = terrain.Corners[z0 * terrain.Width + x1].WaterWorld;
        float w01 = terrain.Corners[z1 * terrain.Width + x0].WaterWorld;
        float w11 = terrain.Corners[z1 * terrain.Width + x1].WaterWorld;
        return Mathf.Lerp(Mathf.Lerp(w00, w10, u), Mathf.Lerp(w01, w11, u), v) * CellSize;
    }

    public static float WaterHeightAt(TerrainData terrain, float worldX, float worldZ)
    {
        if (terrain == null) return 0f;
        int x = Mathf.Clamp(Mathf.FloorToInt((0f - worldX) / 128f), 0, terrain.Width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(worldZ / 128f), 0, terrain.Height - 1);
        TerrainData.Corner corner = terrain.Corners[y * terrain.Width + x];
        if (!corner.HasWater) return HeightAt(terrain, worldX, worldZ);
        return (corner.WaterWorld + WaterOffset(terrain.Tileset)) * 128f;
    }

    private static float WaterOffset(char tileset)
    {
        if (_waterOffsets.TryGetValue(tileset, out float offset)) return offset;
        offset = War3WaterConfig.LoadHeightOffset(tileset);
        _waterOffsets[tileset] = offset;
        return offset;
    }

    public static int ApplyUnitTextureFilter(Node node)
    {
        int num = 0;
        if (node is MeshInstance3D { Mesh: not null } meshInstance3D)
        {
            for (int i = 0; i < meshInstance3D.Mesh.GetSurfaceCount(); i++)
            {
                Material material = meshInstance3D.MaterialOverride ?? meshInstance3D.GetSurfaceOverrideMaterial(i) ?? meshInstance3D.Mesh.SurfaceGetMaterial(i);
                if (material is BaseMaterial3D baseMaterial3D)
                {
                    baseMaterial3D.TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear;
                    baseMaterial3D.TextureRepeat = false;
                    num++;
                }
            }
        }
        foreach (Node child in node.GetChildren())
        {
            num += ApplyUnitTextureFilter(child);
        }
        return num;
    }

    private static bool TriangleHeightAt(List<Vector3> triangles, int start, float x, float z, out float y)
    {
        Vector3 vector = triangles[start];
        Vector3 vector2 = triangles[start + 1];
        Vector3 vector3 = triangles[start + 2];
        float num = (vector2.Z - vector3.Z) * (vector.X - vector3.X) + (vector3.X - vector2.X) * (vector.Z - vector3.Z);
        if (Mathf.Abs(num) < 0.0001f)
        {
            y = 0f;
            return false;
        }
        float num2 = ((vector2.Z - vector3.Z) * (x - vector3.X) + (vector3.X - vector2.X) * (z - vector3.Z)) / num;
        float num3 = ((vector3.Z - vector.Z) * (x - vector3.X) + (vector.X - vector3.X) * (z - vector3.Z)) / num;
        float num4 = 1f - num2 - num3;
        if (num2 < -0.02f || num3 < -0.02f || num4 < -0.02f)
        {
            y = 0f;
            return false;
        }
        y = num2 * vector.Y + num3 * vector2.Y + num4 * vector3.Y;
        return true;
    }
}
