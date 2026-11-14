using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    public const float CellSize = 128f;

    public const float WaterLevel = 14f;

    public W3eTerrain Terrain { get; private set; }

    public Node3D Root { get; private set; }

    public float WaterY { get; private set; } = 14f;

    public List<TreeState> Trees { get; } = new List<TreeState>();

    public Node3D Build(W3eTerrain terrain, War3MapDoodads doodads, Color waterBaseColor)
    {
        Terrain = terrain;
        Root = new Node3D
        {
            Name = "MapRoot"
        };
        bool[,] covered = BuildCliffs(terrain);
        BuildTerrain(terrain, covered);
        BuildWater(terrain, waterBaseColor);
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

    public static float HeightAt(W3eTerrain terrain, float worldX, float worldZ)
    {
        float x = (0f - worldX) / 128f;
        float y = worldZ / 128f;
        return InterpolatedHeight(terrain, x, y);
    }

    public static float SurfaceHeightAt(W3eTerrain terrain, float worldX, float worldZ)
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

    private static float CornerSurfaceHeight(W3eTerrain terrain, int x, int y)
        => GroundWorld(terrain.Corners[y * terrain.Width + x]) + RampLift(terrain, x, y) * CellSize;

    private static readonly Dictionary<char, float> _waterOffsets = new();

    public static float WaterHeightAt(W3eTerrain terrain, float worldX, float worldZ)
    {
        if (terrain == null) return 0f;
        int x = Mathf.Clamp(Mathf.FloorToInt((0f - worldX) / 128f), 0, terrain.Width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(worldZ / 128f), 0, terrain.Height - 1);
        W3eTerrain.Corner corner = terrain.Corners[y * terrain.Width + x];
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
}
