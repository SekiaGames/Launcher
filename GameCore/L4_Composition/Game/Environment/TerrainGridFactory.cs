using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class MapWorldBuilder
{
    private TerrainTextureSet _groundLayers;

    /// <summary>地形材质（腐地遮罩等运行时参数都挂在它上面）。</summary>
    public ShaderMaterial TerrainMaterial { get; private set; }

    /// <summary>腐地遮罩：场景每帧推进，亡灵建筑落成时往里盖章。</summary>
    public BlightMask Blight { get; private set; }

    private List<string> _groundOrder = new List<string>();

    private void BuildTerrain(W3eTerrain terrain, bool[,] covered)
    {
        TerrainTextureLoader.SetBlightTileset(terrain.Tileset);
        List<string> list = new List<string>();
        if (terrain.FixedGroundOrder != null)
        {
            list.AddRange(terrain.FixedGroundOrder);
        }
        else
        {
            HashSet<string> hashSet = new HashSet<string>();
            W3eTerrain.Corner[] corners = terrain.Corners;
            foreach (W3eTerrain.Corner corner in corners)
            {
                int groundTexture = corner.GroundTexture;
                if (groundTexture >= 0 && groundTexture < terrain.GroundTextures.Count)
                {
                    hashSet.Add(terrain.GroundTextures[groundTexture]);
                }
            }
            foreach (string groundTexture2 in terrain.GroundTextures)
            {
                if (hashSet.Contains(groundTexture2))
                {
                    list.Add(groundTexture2);
                }
            }
        }
        foreach (string cliffTexture in terrain.CliffTextures)
        {
            string text = CliffTextureSet.GroundTileFor(cliffTexture);
            if (!string.IsNullOrEmpty(text) && !list.Contains(text))
            {
                list.Add(text);
            }
        }
        TerrainTextureLoader tiles = new TerrainTextureLoader();
        List<string> list2 = new List<string>();
        TerrainTextureSet terrainTextureSet = TerrainTextureSet.Build(tiles, list, list2);
        GD.Print($"[MAP] 地表贴图 {list2.Count}/{list.Count}: {string.Join(",", list2)}");
        ShaderMaterial shaderMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/terrain_map.gdshader")
        };
        if (terrainTextureSet?.Texture != null)
        {
            shaderMaterial.SetShaderParameter("ground_texture", terrainTextureSet.Texture);
        }
        ApplyBakedShadow(shaderMaterial, terrain);
        TerrainMaterial = shaderMaterial;
        Blight = new BlightMask(terrain, shaderMaterial,
            Assets.LoadTexture(PathResolve.IconPng(TerrainTextureLoader.BlightPath)));
        _groundLayers = terrainTextureSet;
        _groundOrder = list2;
        int width = terrain.Width;
        int height = terrain.Height;
        int num = 0;
        SurfaceTool surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        surfaceTool.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        for (int j = 0; j < height - 1; j++)
        {
            for (int k = 0; k < width - 1; k++)
            {
                if ((_cliffTransitions == null || !_cliffTransitions[k, j]) && !IsRampEntrance(terrain, k, j) && (IsCliffTile(terrain, k, j) || covered[k, j]))
                {
                    num++;
                    continue;
                }
                W3eTerrain.Corner c2 = terrain.Corners[j * width + k];
                W3eTerrain.Corner corner2 = terrain.Corners[j * width + k + 1];
                W3eTerrain.Corner corner3 = terrain.Corners[(j + 1) * width + k];
                W3eTerrain.Corner corner4 = terrain.Corners[(j + 1) * width + k + 1];
                Vector4 layers = TileLayers(RealTileTexture(terrain, k, j), RealTileTexture(terrain, k + 1, j), RealTileTexture(terrain, k, j + 1), RealTileTexture(terrain, k + 1, j + 1), c2);
                float x = (float)k * 128f;
                float x2 = (float)(k + 1) * 128f;
                float z = (float)j * 128f;
                float z2 = (float)(j + 1) * 128f;
                Vector3 position = new Vector3(x, CornerWorld(terrain, k, j), z);
                Vector3 position2 = new Vector3(x2, CornerWorld(terrain, k + 1, j), z);
                Vector3 position3 = new Vector3(x, CornerWorld(terrain, k, j + 1), z2);
                Vector3 position4 = new Vector3(x2, CornerWorld(terrain, k + 1, j + 1), z2);
                Vector3 normal = CornerNormal(terrain, k, j);
                Vector3 normal2 = CornerNormal(terrain, k + 1, j);
                Vector3 normal3 = CornerNormal(terrain, k, j + 1);
                Vector3 normal4 = CornerNormal(terrain, k + 1, j + 1);
                Vector2 uv = new Vector2(0f, 1f);
                Vector2 uv2 = new Vector2(1f, 1f);
                Vector2 uv3 = new Vector2(1f, 0f);
                Vector2 uv4 = new Vector2(0f, 0f);
                AddVertex(surfaceTool, position4, uv3, layers, normal4);
                AddVertex(surfaceTool, position2, uv2, layers, normal2);
                AddVertex(surfaceTool, position3, uv4, layers, normal3);
                AddVertex(surfaceTool, position3, uv4, layers, normal3);
                AddVertex(surfaceTool, position2, uv2, layers, normal2);
                AddVertex(surfaceTool, position, uv, layers, normal);
            }
        }
        GD.Print($"[MAP] 地面格抠掉 {num}（悬崖+斜坡过渡）");
        Root.AddChild(new MeshInstance3D
        {
            Mesh = surfaceTool.Commit(null, 0uL),
            MaterialOverride = shaderMaterial,
            Name = "MapTerrain"
        }, forceReadableName: false, Node.InternalMode.Disabled);
    }

    /// <summary>地图自带的 war3map.shd：一格地形一个字节，越大越暗（客户端按 1/2 折算成半强度）。</summary>
    private static void ApplyBakedShadow(ShaderMaterial material, W3eTerrain terrain)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        byte[] data = Assets.ReadBytes("res://gmap/map/war3map.shd");
        if (data == null || data.Length < width * height)
        {
            GD.Print($"[MAP] 阴影图缺失：要 {width * height} 字节，实得 {(data == null ? 0 : data.Length)}");
            return;
        }

        var shadow = new byte[width * height];
        Array.Copy(data, shadow, shadow.Length);

        int darkest = 0;
        long total = 0;
        foreach (byte value in shadow)
        {
            total += value;
            if (value > darkest) darkest = value;
        }

        Image image = Image.CreateFromData(width, height, false, Image.Format.L8, shadow);
        material.SetShaderParameter("shadow_map", ImageTexture.CreateFromImage(image));
        material.SetShaderParameter("shadow_scale", new Vector2(1f / (width * CellSize), 1f / (height * CellSize)));
        material.SetShaderParameter("shadow_strength", 0.5f);
        material.SetShaderParameter("shadow_enabled", true);
        GD.Print($"[MAP] 阴影图 {width}x{height}，最深 {darkest}，均值 {(double)total / shadow.Length:0.0}");
    }

    private Vector4 TileLayers(string t00, string t10, string t01, string t11, W3eTerrain.Corner c00)
    {
        int bl = TextureIndex(_groundOrder, t00);
        int br = TextureIndex(_groundOrder, t10);
        int tl = TextureIndex(_groundOrder, t01);
        int tr = TextureIndex(_groundOrder, t11);
        List<int> list = new List<int> { bl, br, tl, tr };
        list.Sort();
        List<int> list2 = new List<int>();
        foreach (int item in list)
        {
            if (!list2.Contains(item))
            {
                list2.Add(item);
            }
        }
        int textureIndex = list2[0];
        float x = LayerOf(c00, textureIndex);
        float y = ((list2.Count > 1) ? LayerWithMask(list2[1]) : 0f);
        float z = ((list2.Count > 2) ? LayerWithMask(list2[2]) : 0f);
        float w = ((list2.Count > 3) ? LayerWithMask(list2[3]) : 0f);
        return new Vector4(x, y, z, w);
        float LayerWithMask(int textureId)
        {
            int num = 0;
            if (br == textureId)
            {
                num |= 1;
            }
            if (bl == textureId)
            {
                num |= 2;
            }
            if (tr == textureId)
            {
                num |= 4;
            }
            if (tl == textureId)
            {
                num |= 8;
            }
            return BaseLayerOf(textureId) + (float)num;
        }
    }

    private float BaseLayerOf(int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count)
        {
            return 0f;
        }
        return _groundLayers.BaseLayer[_groundOrder[textureIndex]];
    }

    private float LayerOf(W3eTerrain.Corner corner, int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count)
        {
            return 0f;
        }
        return _groundLayers.LayerOf(_groundOrder[textureIndex], corner.VariationIndex);
    }

    private static string RealTileTexture(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        bool flag = false;
        for (int i = -1; i < 1; i++)
        {
            if (flag)
            {
                break;
            }
            for (int j = -1; j < 1; j++)
            {
                if (flag)
                {
                    break;
                }
                int num = x + i;
                int num2 = y + j;
                if (num < 0 || num >= width || num2 < 0 || num2 >= height)
                {
                    continue;
                }
                W3eTerrain.Corner corner = terrain.Corners[num2 * width + num];
                if (num >= width - 1 || num2 >= height - 1 || !IsCliffTile(terrain, num, num2))
                {
                    continue;
                }
                W3eTerrain.Corner corner2 = terrain.Corners[num2 * width + num + 1];
                W3eTerrain.Corner corner3 = terrain.Corners[(num2 + 1) * width + num];
                W3eTerrain.Corner corner4 = terrain.Corners[(num2 + 1) * width + num + 1];
                if (corner.Ramp && corner3.Ramp && corner2.Ramp && corner4.Ramp)
                {
                    flag = true;
                    continue;
                }
                int num3 = corner.CliffTexture;
                if (num3 == 15)
                {
                    num3 -= 14;
                }
                if (num3 >= 0 && num3 < terrain.CliffTextures.Count)
                {
                    string text = CliffTextureSet.GroundTileFor(terrain.CliffTextures[num3]);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
        }
        int groundTexture = terrain.Corners[y * width + x].GroundTexture;
        return (groundTexture >= 0 && groundTexture < terrain.GroundTextures.Count) ? terrain.GroundTextures[groundTexture] : null;
    }

    private static float GroundWorld(W3eTerrain.Corner corner)
    {
        return (corner.HeightWorld + (float)corner.LayerHeight - 2f) * 128f;
    }

    private static float CornerWorld(W3eTerrain terrain, int x, int y)
    {
        return GroundWorld(terrain.Corners[y * terrain.Width + x]) + RampLift(terrain, x, y) * 128f;
    }

    private static float RampLift(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int layerHeight = terrain.Corners[y * width + x].LayerHeight;
        for (int i = -1; i <= 0; i++)
        {
            for (int j = -1; j <= 0; j++)
            {
                int num = x + i;
                int num2 = y + j;
                if (num >= 0 && num2 >= 0 && num < width - 1 && num2 < height - 1 && IsRampEntrance(terrain, num, num2))
                {
                    int num3 = Math.Min(Math.Min(terrain.Corners[num2 * width + num].LayerHeight, terrain.Corners[num2 * width + num + 1].LayerHeight), Math.Min(terrain.Corners[(num2 + 1) * width + num].LayerHeight, terrain.Corners[(num2 + 1) * width + num + 1].LayerHeight));
                    if (layerHeight == num3)
                    {
                        return 0.5f;
                    }
                }
            }
        }
        return 0f;
    }

    private static void AddVertex(SurfaceTool st, Vector3 position, Vector2 uv, Vector4 layers, Vector3 normal)
    {
        st.SetUV(uv);
        st.SetNormal(new Vector3(0f - normal.X, normal.Y, normal.Z));
        st.SetCustom(0, new Color(layers.X, layers.Y, layers.Z, layers.W));
        st.AddVertex(new Vector3(0f - position.X, position.Y, position.Z));
    }

    private static Vector3 CornerNormal(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int num = Mathf.Max(0, x - 1);
        int num2 = Mathf.Min(width - 1, x + 1);
        int num3 = Mathf.Max(0, y - 1);
        int num4 = Mathf.Min(height - 1, y + 1);
        float x2 = (terrain.Corners[y * width + num].HeightWorld - terrain.Corners[y * width + num2].HeightWorld) / 128f;
        float z = (terrain.Corners[num3 * width + x].HeightWorld - terrain.Corners[num4 * width + x].HeightWorld) / 128f;
        return new Vector3(x2, 2f, z).Normalized();
    }

    private static int TextureIndex(List<string> used, string tileId)
    {
        if (string.IsNullOrEmpty(tileId))
        {
            return 0;
        }
        int num = used.IndexOf(tileId);
        return (num >= 0) ? num : 0;
    }

    private static float InterpolatedHeight(W3eTerrain terrain, float x, float y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        float num = Mathf.Clamp(x, 0f, (float)width - 1.01f);
        float num2 = Mathf.Clamp(y, 0f, (float)height - 1.01f);
        int num3 = (int)num;
        int num4 = (int)num2;
        int num5 = Mathf.Min(num3 + 1, width - 1);
        int num6 = Mathf.Min(num4 + 1, height - 1);
        float weight = num - (float)num3;
        float weight2 = num2 - (float)num4;
        float from = GroundWorld(terrain.Corners[num4 * width + num3]);
        float to = GroundWorld(terrain.Corners[num4 * width + num5]);
        float from2 = GroundWorld(terrain.Corners[num6 * width + num3]);
        float to2 = GroundWorld(terrain.Corners[num6 * width + num5]);
        return Mathf.Lerp(Mathf.Lerp(from, to, weight), Mathf.Lerp(from2, to2, weight), weight2);
    }

    private static float GroundSurface(W3eTerrain terrain, int x, int y, float worldX, float worldZ, int baseLayer)
    {
        float num = Mathf.Clamp((worldX - (float)x * 128f) / 128f, 0f, 1f);
        float num2 = Mathf.Clamp((worldZ - (float)y * 128f) / 128f, 0f, 1f);
        float num3 = Corner(x, y);
        float num4 = Corner(x + 1, y);
        float num5 = Corner(x, y + 1);
        float num6 = Corner(x + 1, y + 1);
        if (num + num2 >= 1f)
        {
            return num6 * (num + num2 - 1f) + num4 * (1f - num2) + num5 * (1f - num);
        }
        return num3 * (1f - num - num2) + num4 * num + num5 * num2;
        float Corner(int cx, int cz)
        {
            return (terrain.Corners[cz * terrain.Width + cx].HeightWorld + (float)baseLayer - 2f) * 128f + RampLift(terrain, cx, cz) * 128f;
        }
    }

    private static char RampChar(W3eTerrain.Corner corner, int baseLayer)
    {
        int num = corner.LayerHeight - baseLayer;
        if (!corner.Ramp)
        {
            return (char)(65 + num);
        }
        if (1 == 0)
        {
        }
        char result = num switch
        {
            0 => 'L',
            1 => 'H',
            _ => 'X',
        };
        if (1 == 0)
        {
        }
        return result;
    }

    private static void MarkFullyCoveredTiles(W3eTerrain terrain, List<Vector3> triangles, bool[,] covered)
    {
        if (triangles.Count == 0)
        {
            return;
        }
        int width = terrain.Width;
        int height = terrain.Height;
        float num = float.MaxValue;
        float num2 = float.MinValue;
        float num3 = float.MaxValue;
        float num4 = float.MinValue;
        foreach (Vector3 triangle in triangles)
        {
            num = Mathf.Min(num, triangle.X);
            num2 = Mathf.Max(num2, triangle.X);
            num3 = Mathf.Min(num3, triangle.Z);
            num4 = Mathf.Max(num4, triangle.Z);
        }
        int num5 = Mathf.Max(0, Mathf.FloorToInt(num / 128f));
        int num6 = Mathf.Min(width - 2, Mathf.FloorToInt(num2 / 128f - 0.001f));
        int num7 = Mathf.Max(0, Mathf.FloorToInt(num3 / 128f));
        int num8 = Mathf.Min(height - 2, Mathf.FloorToInt(num4 / 128f - 0.001f));
        for (int i = num7; i <= num8; i++)
        {
            for (int j = num5; j <= num6; j++)
            {
                if (covered[j, i])
                {
                    continue;
                }
                bool flag = true;
                for (int k = 1; k <= 4 && flag; k++)
                {
                    for (int l = 1; l <= 4 && flag; l++)
                    {
                        float num9 = ((float)j + (float)l * 0.2f) * 128f;
                        float num10 = ((float)i + (float)k * 0.2f) * 128f;
                        float num11 = InterpolatedHeight(terrain, num9 / 128f, num10 / 128f);
                        bool flag2 = false;
                        for (int m = 0; m + 2 < triangles.Count; m += 3)
                        {
                            if (TriangleHeightAt(triangles, m, num9, num10, out var y) && y >= num11 - 2f)
                            {
                                flag2 = true;
                                break;
                            }
                        }
                        flag = flag2;
                    }
                }
                covered[j, i] = flag;
            }
        }
    }
}
