using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    private ShaderMaterial _waterMaterial;

    private int _waterFrameCount = 1;

    private int _waterFrameRate = 1;

    private int _waterFrameOffset;

    private double _waterElapsed;

    private void BuildWater(TerrainData terrain, Color baseColor, War3FogConfig fog)
    {
        War3WaterConfig war3WaterConfig = War3WaterConfig.Load(terrain.Tileset, baseColor);
        if (war3WaterConfig == null)
        {
            return;
        }
        ShaderMaterial shaderMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/water_map.gdshader")
        };
        if (war3WaterConfig.Texture != null)
        {
            shaderMaterial.SetShaderParameter("water_texture", war3WaterConfig.Texture);
        }
        shaderMaterial.SetShaderParameter("water_lighting", war3WaterConfig.Lighting);
        shaderMaterial.SetShaderParameter("fog_color",
            new Vector4(fog.Tint.R, fog.Tint.G, fog.Tint.B, 1f));
        shaderMaterial.SetShaderParameter("fog_params", fog.Params);
        _waterMaterial = shaderMaterial;
        _waterFrameCount = Mathf.Max(1, war3WaterConfig.FrameCount);
        _waterFrameRate = Mathf.Max(1, war3WaterConfig.FrameRate);
        _waterFrameOffset = war3WaterConfig.FrameOffset;
        int cells = Mathf.Max(1, war3WaterConfig.TextureCells);
        const int chunkCells = 8;
        var chunks = new Dictionary<(int X, int Y), SurfaceTool>();
        int width = terrain.Width;
        int height = terrain.Height;
        int num = 0;
        Dictionary<int, int> levels = new Dictionary<int, int>();
        for (int i = 0; i < height - 1; i++)
        {
            for (int j = 0; j < width - 1; j++)
            {
                TerrainData.Corner corner = terrain.Corners[i * width + j];
                TerrainData.Corner corner2 = terrain.Corners[i * width + j + 1];
                TerrainData.Corner corner3 = terrain.Corners[(i + 1) * width + j];
                TerrainData.Corner corner4 = terrain.Corners[(i + 1) * width + j + 1];
                if (!corner.HasWater && !corner2.HasWater && !corner3.HasWater && !corner4.HasWater)
                {
                    continue;
                }
                var key = (j / chunkCells, i / chunkCells);
                if (!chunks.TryGetValue(key, out SurfaceTool surfaceTool))
                {
                    surfaceTool = new SurfaceTool();
                    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
                    chunks[key] = surfaceTool;
                }
                float x = (float)j * 128f;
                float x2 = (float)(j + 1) * 128f;
                float z = (float)i * 128f;
                float z2 = (float)(i + 1) * 128f;
                Vector3 position = new Vector3(x, WaterWorld(corner, war3WaterConfig), z);
                Vector3 position2 = new Vector3(x2, WaterWorld(corner2, war3WaterConfig), z);
                Vector3 position3 = new Vector3(x, WaterWorld(corner3, war3WaterConfig), z2);
                Vector3 position4 = new Vector3(x2, WaterWorld(corner4, war3WaterConfig), z2);
                CountLevel(levels, corner);
                CountLevel(levels, corner2);
                CountLevel(levels, corner3);
                CountLevel(levels, corner4);
                float u0 = (float)(j % cells) / cells;
                float u1 = (float)(1 + j % cells) / cells;
                float v0 = 1f - (float)(i % cells) / cells;
                float v1 = 1f - (float)(1 + i % cells) / cells;
                Vector2 uv = new Vector2(u0, v0);
                Vector2 uv2 = new Vector2(u1, v0);
                Vector2 uv3 = new Vector2(u0, v1);
                Vector2 uv4 = new Vector2(u1, v1);
                AddWaterVertex(surfaceTool, position, uv, WaterColor(war3WaterConfig, terrain, j, i));
                AddWaterVertex(surfaceTool, position4, uv4, WaterColor(war3WaterConfig, terrain, j + 1, i + 1));
                AddWaterVertex(surfaceTool, position2, uv2, WaterColor(war3WaterConfig, terrain, j + 1, i));
                AddWaterVertex(surfaceTool, position, uv, WaterColor(war3WaterConfig, terrain, j, i));
                AddWaterVertex(surfaceTool, position3, uv3, WaterColor(war3WaterConfig, terrain, j, i + 1));
                AddWaterVertex(surfaceTool, position4, uv4, WaterColor(war3WaterConfig, terrain, j + 1, i + 1));
                num++;
            }
        }
        WaterY = MostCommonLevel(levels, war3WaterConfig);
        foreach (KeyValuePair<(int X, int Y), SurfaceTool> chunk in chunks)
        {
            Root.AddChild(new MeshInstance3D
            {
                Mesh = chunk.Value.Commit(null, 0uL),
                MaterialOverride = shaderMaterial,
                Name = $"MapWater_{chunk.Key.X}_{chunk.Key.Y}"
            }, forceReadableName: false, Node.InternalMode.Disabled);
        }
        if (num != 0)
        {
            GD.Print($"[WATER] 水面格 {num}，分块 {chunks.Count}，水面 y={WaterY:0.0}，帧数 {war3WaterConfig.FrameCount}"
                + $"，cells={cells}，光照 {(war3WaterConfig.Lighting ? "开" : "关")}"
                + $"，水色 ({war3WaterConfig.BaseColor.R:0.00},{war3WaterConfig.BaseColor.G:0.00},"
                + $"{war3WaterConfig.BaseColor.B:0.00},{war3WaterConfig.BaseColor.A:0.00})"
                + $"，深度 {war3WaterConfig.MinimumDepth:0.000}/{war3WaterConfig.DeepLevel:0.000}/{war3WaterConfig.MaximumDepth:0.000}"
                + $"，alphaMode {war3WaterConfig.AlphaMode}"
                + $"，距离雾 {fog.Describe()}");
        }
    }

    public void BindWaterFogOfWar(Texture2D visibility, Vector4 uvp, bool enabled)
    {
        if (_waterMaterial == null) return;
        _waterMaterial.SetShaderParameter("fog_of_war_enable", enabled && visibility != null);
        if (!enabled || visibility == null) return;
        _waterMaterial.SetShaderParameter("fog_of_war_texture", visibility);
        _waterMaterial.SetShaderParameter("fog_of_war_uvp", uvp);
    }

    private static void CountLevel(Dictionary<int, int> levels, TerrainData.Corner corner)
    {
        if (corner.HasWater)
        {
            int key = (int)Mathf.Round(corner.WaterWorld * 100f);
            levels[key] = levels.GetValueOrDefault(key) + 1;
        }
    }

    private static float MostCommonLevel(Dictionary<int, int> levels, War3WaterConfig water)
    {
        int num = int.MinValue;
        int num2 = 0;
        foreach (KeyValuePair<int, int> level in levels)
        {
            if (level.Value > num2)
            {
                num2 = level.Value;
                num = level.Key;
            }
        }
        return (num2 == 0) ? 14f : (((float)num / 100f + water.HeightOffset) * 128f);
    }

    private static float WaterWorld(TerrainData.Corner corner, War3WaterConfig water)
    {
        return (corner.WaterWorld + water.HeightOffset) * 128f;
    }

    private static Color WaterColor(War3WaterConfig water, TerrainData terrain, int x, int y)
    {
        TerrainData.Corner corner = terrain.Corners[y * terrain.Width + x];
        float num = corner.WaterWorld + water.HeightOffset;
        float num2 = CornerWorld(terrain, x, y) / 128f;
        float num3 = Mathf.Clamp(num - num2, 0f, 1f);
        if (num3 <= water.DeepLevel)
        {
            float weight = Mathf.Clamp((num3 - water.MinimumDepth)
                / Mathf.Max(0.0001f, water.DeepLevel - water.MinimumDepth), 0f, 1f);
            return water.ShallowMin.Lerp(water.ShallowMax, weight);
        }
        float weight2 = Mathf.Clamp((num3 - water.DeepLevel)
            / Mathf.Max(0.0001f, water.MaximumDepth - water.DeepLevel), 0f, 1f);
        return water.DeepMin.Lerp(water.DeepMax, weight2);
    }

    private static void AddWaterVertex(SurfaceTool st, Vector3 position, Vector2 uv, Color color)
    {
        st.SetUV(uv);
        st.SetColor(color);
        st.AddVertex(new Vector3(0f - position.X, position.Y, position.Z));
    }
}
