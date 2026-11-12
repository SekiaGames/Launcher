using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class MapWorldBuilder
{
    private ShaderMaterial _waterMaterial;

    private int _waterFrameCount = 1;

    private int _waterFrameRate = 1;

    private double _waterElapsed;

    private void BuildWater(W3eTerrain terrain)
    {
        War3WaterConfig war3WaterConfig = War3WaterConfig.Load(terrain.Tileset);
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
        _waterMaterial = shaderMaterial;
        _waterFrameCount = Mathf.Max(1, war3WaterConfig.FrameCount);
        _waterFrameRate = Mathf.Max(1, war3WaterConfig.FrameRate);
        SurfaceTool surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        int width = terrain.Width;
        int height = terrain.Height;
        int num = 0;
        Dictionary<int, int> levels = new Dictionary<int, int>();
        for (int i = 0; i < height - 1; i++)
        {
            for (int j = 0; j < width - 1; j++)
            {
                W3eTerrain.Corner corner = terrain.Corners[i * width + j];
                W3eTerrain.Corner corner2 = terrain.Corners[i * width + j + 1];
                W3eTerrain.Corner corner3 = terrain.Corners[(i + 1) * width + j];
                W3eTerrain.Corner corner4 = terrain.Corners[(i + 1) * width + j + 1];
                if (corner.HasWater || corner2.HasWater || corner3.HasWater || corner4.HasWater)
                {
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
                    Vector2 uv = new Vector2(j, 1 - i);
                    Vector2 uv2 = new Vector2(j + 1, 1 - i);
                    Vector2 uv3 = new Vector2(j, -i);
                    Vector2 uv4 = new Vector2(j + 1, -i);
                    AddWaterVertex(surfaceTool, position, uv, WaterColor(war3WaterConfig, terrain, j, i));
                    AddWaterVertex(surfaceTool, position4, uv4, WaterColor(war3WaterConfig, terrain, j + 1, i + 1));
                    AddWaterVertex(surfaceTool, position2, uv2, WaterColor(war3WaterConfig, terrain, j + 1, i));
                    AddWaterVertex(surfaceTool, position, uv, WaterColor(war3WaterConfig, terrain, j, i));
                    AddWaterVertex(surfaceTool, position3, uv3, WaterColor(war3WaterConfig, terrain, j, i + 1));
                    AddWaterVertex(surfaceTool, position4, uv4, WaterColor(war3WaterConfig, terrain, j + 1, i + 1));
                    num++;
                }
            }
        }
        WaterY = MostCommonLevel(levels, war3WaterConfig);
        if (num != 0)
        {
            Root.AddChild(new MeshInstance3D
            {
                Mesh = surfaceTool.Commit(null, 0uL),
                MaterialOverride = shaderMaterial,
                Name = "MapWater"
            }, forceReadableName: false, Node.InternalMode.Disabled);
            GD.Print($"[WATER] 水面格 {num}，水面 y={WaterY:0.0}，帧数 {war3WaterConfig.FrameCount}");
        }
    }

    private static void CountLevel(Dictionary<int, int> levels, W3eTerrain.Corner corner)
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

    private static float WaterWorld(W3eTerrain.Corner corner, War3WaterConfig water)
    {
        return (corner.WaterWorld + water.HeightOffset) * 128f;
    }

    private static Color WaterColor(War3WaterConfig water, W3eTerrain terrain, int x, int y)
    {
        W3eTerrain.Corner corner = terrain.Corners[y * terrain.Width + x];
        float num = corner.WaterWorld + water.HeightOffset;
        float num2 = CornerWorld(terrain, x, y) / 128f;
        float num3 = Mathf.Clamp(num - num2, 0f, 1f);
        if (num3 <= 0.5f)
        {
            float weight = Mathf.Clamp((num3 - 5f / 64f) / (27f / 64f), 0f, 1f);
            return water.ShallowMin.Lerp(water.ShallowMax, weight);
        }
        float weight2 = Mathf.Clamp((num3 - 0.5f) / 0.0625f, 0f, 1f);
        return water.DeepMin.Lerp(water.DeepMax, weight2);
    }

    private static void AddWaterVertex(SurfaceTool st, Vector3 position, Vector2 uv, Color color)
    {
        st.SetUV(uv);
        st.SetColor(color);
        st.AddVertex(new Vector3(0f - position.X, position.Y, position.Z));
    }
}
