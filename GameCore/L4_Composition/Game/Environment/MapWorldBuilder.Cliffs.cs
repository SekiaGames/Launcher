using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    private static readonly Vector3 CliffLightDirection = new Vector3(-0.6095f, 0.5079f, -0.6095f);

    private bool[,] _cliffTransitions;

    public ShaderMaterial CliffMaterial { get; private set; }

    private static List<string> UsedCliffIds(TerrainData terrain)
    {
        List<string> list = new List<string>();
        TerrainData.Corner[] corners = terrain.Corners;
        foreach (TerrainData.Corner corner in corners)
        {
            int cliffTexture = corner.CliffTexture;
            if (cliffTexture >= 0 && cliffTexture < terrain.CliffTextures.Count)
            {
                string item = terrain.CliffTextures[cliffTexture];
                if (!list.Contains(item))
                {
                    list.Add(item);
                }
            }
        }
        return list;
    }

    private bool[,] BuildCliffs(TerrainData terrain)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        bool[,] array = new bool[width, height];
        for (int i = 0; i < height - 1; i++)
        {
            for (int j = 0; j < width - 1; j++)
            {
                array[j, i] = IsCliffTile(terrain, j, i);
            }
        }
        bool[,] array2 = new bool[width, height];
        _cliffTransitions = new bool[width, height];
        CliffTextureSet cliffTextureSet = CliffTextureSet.Build(UsedCliffIds(terrain));
        ShaderMaterial shaderMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/cliff_map.gdshader")
        };
        if (cliffTextureSet?.Texture != null)
        {
            shaderMaterial.SetShaderParameter("cliff_texture", cliffTextureSet.Texture);
        }
        CliffMaterial = shaderMaterial;
        SurfaceTool surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        surfaceTool.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        int num = 0;
        int num2 = 0;
        Dictionary<string, int> dictionary = new Dictionary<string, int>();
        for (int k = 0; k < width - 1; k++)
        {
            for (int l = 0; l < height - 1; l++)
            {
                if (!array[k, l])
                {
                    continue;
                }
                TerrainData.Corner corner = terrain.Corners[l * width + k];
                TerrainData.Corner corner2 = terrain.Corners[l * width + k + 1];
                TerrainData.Corner corner3 = terrain.Corners[(l + 1) * width + k];
                TerrainData.Corner corner4 = terrain.Corners[(l + 1) * width + k + 1];
                bool facingDown = corner3.LayerHeight >= corner.LayerHeight && corner4.LayerHeight >= corner2.LayerHeight;
                bool facingLeft = corner2.LayerHeight >= corner.LayerHeight && corner4.LayerHeight >= corner3.LayerHeight;
                if ((facingDown && l == 0) || (!facingDown && l >= height - 2) || (facingLeft && k == 0) || (!facingLeft && k >= width - 2))
                {
                    continue;
                }
                int num3 = Math.Min(Math.Min(corner.LayerHeight, corner2.LayerHeight), Math.Min(corner3.LayerHeight, corner4.LayerHeight));
                bool verticalRamp = corner.Ramp != corner2.Ramp && corner3.Ramp != corner4.Ramp;
                bool horizontalRamp = corner.Ramp != corner3.Ramp && corner2.Ramp != corner4.Ramp;
                if (verticalRamp || horizontalRamp)
                {
                    bool rampBlockedByCliff = verticalRamp && CornerFlag(array, k, l + (facingDown ? -1 : 1))
                        || horizontalRamp && CornerFlag(array, k + (facingLeft ? -1 : 1), l);
                    bool invalidRamp = false;
                    if (rampBlockedByCliff)
                    {
                        invalidRamp = true;
                    }
                    else if (corner3.LayerHeight - num3 > 1)
                    {
                        invalidRamp = true;
                        ClearRampCorner(terrain, k, l + 1);
                    }
                    else if (corner4.LayerHeight - num3 > 1)
                    {
                        invalidRamp = true;
                        ClearRampCorner(terrain, k + 1, l + 1);
                    }
                    else if (corner2.LayerHeight - num3 > 1)
                    {
                        invalidRamp = true;
                        ClearRampCorner(terrain, k + 1, l);
                    }
                    else if (corner.LayerHeight - num3 > 1)
                    {
                        invalidRamp = true;
                        ClearRampCorner(terrain, k, l);
                    }
                    if (!invalidRamp)
                    {
                        string text = string.Concat(RampChar(corner, num3), RampChar(corner3, num3), RampChar(corner4, num3), RampChar(corner2, num3));
                        string rampDirectory = CliffTextureSet.RampModelDirectoryFor(CliffIdOf(terrain, k, l));
                        string text2 = CliffModelLibrary.FindModel(rampDirectory, rampDirectory + text + "0");
                        if (text2 != null)
                        {
                            int placementColumn = k + ((horizontalRamp && facingLeft) ? -1 : 0);
                            int placementRow = l - ((verticalRamp && facingDown) ? 1 : 0);
                            MarkCovered(surfaceTool, terrain, text2, placementColumn, placementRow, num3, cliffTextureSet, corner, array2, cullGround: false);
                            _cliffTransitions[k, l] = true;
                            _cliffTransitions[k + (facingLeft ? -1 : 1) * (horizontalRamp ? 1 : 0), l + (facingDown ? -1 : 1) * (verticalRamp ? 1 : 0)] = true;
                            num++;
                            num2++;
                            continue;
                        }
                        dictionary["CliffTrans" + text] = dictionary.GetValueOrDefault("CliffTrans" + text) + 1;
                    }
                }
                if (IsRampEntrance(terrain, k, l))
                {
                    continue;
                }
                string text3 = string.Concat((char)(65 + corner.LayerHeight - num3), (char)(65 + corner3.LayerHeight - num3), (char)(65 + corner4.LayerHeight - num3), (char)(65 + corner2.LayerHeight - num3));
                if (!(text3 == "AAAA"))
                {
                    string cliffDirectory = CliffTextureSet.CliffModelDirectoryFor(CliffIdOf(terrain, k, l));
                    string text4 = CliffModelLibrary.ResolveCliff(cliffDirectory, text3, corner.CliffVariation);
                    if (text4 == null)
                    {
                        dictionary[text3] = dictionary.GetValueOrDefault(text3) + 1;
                        continue;
                    }
                    MarkCovered(surfaceTool, terrain, text4, k, l, num3, cliffTextureSet, corner, array2);
                    num++;
                }
            }
        }
        if (dictionary.Count > 0)
        {
            GD.PushWarning("[CLIFF] 缺少模型: " + string.Join(",", dictionary.Keys));
        }
        if (num > 0)
        {
            Root.AddChild(new MeshInstance3D
            {
                Mesh = surfaceTool.Commit(null, 0uL),
                MaterialOverride = shaderMaterial,
                Name = "MapCliffs"
            }, forceReadableName: false, Node.InternalMode.Disabled);
        }
        GD.Print($"[CLIFF] 悬崖格 {num}（过渡 {num2}），贴图集合 {terrain.CliffTextures.Count} 组");
        return array2;
    }

    private void MarkCovered(SurfaceTool st, TerrainData terrain, string model, int x, int y, int baseLayer, CliffTextureSet cliffSet, TerrainData.Corner bl, bool[,] covered, bool cullGround = true)
    {
        CliffGeometry cliffGeometry = CliffModelLibrary.Get(model);
        Vector3 normal = (CornerNormal(terrain, x, y) + CornerNormal(terrain, x + 1, y) + CornerNormal(terrain, x, y + 1) + CornerNormal(terrain, x + 1, y + 1)).Normalized();
        float x2 = CliffLayer(cliffSet, terrain, bl.CliffTexture);
        float y2 = Mathf.Clamp(new Vector3(0f - normal.X, normal.Y, normal.Z).Dot(CliffLightDirection) + 0.45f, 0f, 1f);
        Vector3 vector = new Vector3((float)(x + 1) * 128f, 0f, (float)y * 128f);
        List<Vector3> list = new List<Vector3>();
        for (int i = 0; i + 2 < cliffGeometry.Indices.Length; i += 3)
        {
            Vector3[] array = new Vector3[3];
            for (int j = 0; j < 3; j++)
            {
                int num = cliffGeometry.Indices[i + j switch
                {
                    2 => 1,
                    1 => 2,
                    _ => 0,
                }];
                Vector3 vector2 = cliffGeometry.Vertices[num] + vector;
                vector2.Y = GroundSurface(terrain, x, y, vector2.X, vector2.Z, baseLayer) + cliffGeometry.Vertices[num].Y;
                array[j] = vector2;
                AddVertex(st, vector2, cliffGeometry.Uvs[num], new Vector4(x2, y2, 0f, 0f), normal);
            }
            list.Add(array[0]);
            list.Add(array[1]);
            list.Add(array[2]);
        }
        if (cullGround)
        {
            MarkFullyCoveredTiles(terrain, list, covered);
        }
    }

    private static bool IsCliffTile(TerrainData terrain, int x, int y)
    {
        int width = terrain.Width;
        int layerHeight = terrain.Corners[y * width + x].LayerHeight;
        int layerHeight2 = terrain.Corners[y * width + x + 1].LayerHeight;
        int layerHeight3 = terrain.Corners[(y + 1) * width + x].LayerHeight;
        int layerHeight4 = terrain.Corners[(y + 1) * width + x + 1].LayerHeight;
        return layerHeight != layerHeight2 || layerHeight != layerHeight3 || layerHeight != layerHeight4;
    }

    private static bool IsRampEntrance(TerrainData terrain, int x, int y)
    {
        int width = terrain.Width;
        TerrainData.Corner corner = terrain.Corners[y * width + x];
        TerrainData.Corner corner2 = terrain.Corners[y * width + x + 1];
        TerrainData.Corner corner3 = terrain.Corners[(y + 1) * width + x];
        TerrainData.Corner corner4 = terrain.Corners[(y + 1) * width + x + 1];
        return corner.Ramp && corner3.Ramp && corner2.Ramp && corner4.Ramp && (corner.LayerHeight != corner4.LayerHeight || corner3.LayerHeight != corner2.LayerHeight);
    }

    private static void ClearRampCorner(TerrainData terrain, int x, int y)
    {
        TerrainData.Corner corner = terrain.Corners[y * terrain.Width + x];
        terrain.SetCorner(x, y, corner with { TextureFlags = (byte)(corner.TextureFlags & 0xEF) });
    }

    private static float CliffLayer(CliffTextureSet set, TerrainData terrain, int cliffTexture)
    {
        if (set == null || cliffTexture < 0 || cliffTexture >= terrain.CliffTextures.Count)
        {
            return 0f;
        }
        return set.LayerOf(terrain.CliffTextures[cliffTexture]);
    }

    private static string CliffIdOf(TerrainData terrain, int x, int y)
    {
        int width = terrain.Width;
        for (int i = 0; i < 4; i++)
        {
            int cx = x + (i & 1);
            int cy = y + (i >> 1);
            if (cx >= width || cy >= terrain.Height)
            {
                continue;
            }
            int index = terrain.Corners[cy * width + cx].CliffTexture;
            if (index != 15 && index >= 0 && index < terrain.CliffTextures.Count)
            {
                return terrain.CliffTextures[index];
            }
        }
        for (int cx = Math.Max(0, x - 2); cx <= Math.Min(width - 1, x + 2); cx++)
        {
            for (int cy = Math.Max(0, y - 2); cy <= Math.Min(terrain.Height - 1, y + 2); cy++)
            {
                int index = terrain.Corners[cy * width + cx].CliffTexture;
                if (index != 15 && index >= 0 && index < terrain.CliffTextures.Count)
                {
                    return terrain.CliffTextures[index];
                }
            }
        }
        return null;
    }

    private static bool CornerFlag(bool[,] grid, int x, int y)
    {
        return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) && grid[x, y];
    }
}
