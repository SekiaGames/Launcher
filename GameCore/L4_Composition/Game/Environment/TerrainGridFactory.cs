using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    private const int ChunkTiles = 16;
    private const float BlightRefreshSeconds = 0.5f;

    private TerrainTextureSet _groundLayers;
    private List<string> _groundOrder = new List<string>();
    private TerrainData _terrain;
    private bool[,] _covered;
    private MeshInstance3D[] _terrainChunks;
    private int _chunkColumns;
    private int _chunkRows;
    private float _blightTimer;

    public ShaderMaterial TerrainMaterial { get; private set; }

    public ScoutedBlight Blight { get; private set; }

    private void BuildTerrain(TerrainData terrain, bool[,] covered)
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
            TerrainData.Corner[] corners = terrain.Corners;
            foreach (TerrainData.Corner corner in corners)
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
            AddGroundTile(CliffTextureSet.GroundTileFor(cliffTexture));
            AddGroundTile(CliffTextureSet.UpperTileFor(cliffTexture));
        }
        AddGroundTile(TerrainTextureLoader.BlightId);
        void AddGroundTile(string id)
        {
            if (!string.IsNullOrEmpty(id) && !list.Contains(id))
            {
                list.Add(id);
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
        TerrainMaterial = shaderMaterial;
        _groundLayers = terrainTextureSet;
        _groundOrder = list2;
        _terrain = terrain;
        _covered = covered;
        Blight = new ScoutedBlight(terrain);

        _chunkColumns = Math.Max(1, (terrain.Width - 1 + ChunkTiles - 1) / ChunkTiles);
        _chunkRows = Math.Max(1, (terrain.Height - 1 + ChunkTiles - 1) / ChunkTiles);
        _terrainChunks = new MeshInstance3D[_chunkColumns * _chunkRows];
        int num = 0;
        for (int row = 0; row < _chunkRows; row++)
        {
            for (int column = 0; column < _chunkColumns; column++)
            {
                num += BuildTerrainChunk(column, row);
            }
        }
        GD.Print($"[MAP] 地形 {_chunkColumns}x{_chunkRows} 块，地面格抠掉 {num}（悬崖+斜坡过渡），"
            + $"腐地角点 {Blight.BlightedCells}");
    }

    private int BuildTerrainChunk(int chunkColumn, int chunkRow)
    {
        int width = _terrain.Width;
        int startX = chunkColumn * ChunkTiles;
        int startY = chunkRow * ChunkTiles;
        int endX = Math.Min(startX + ChunkTiles, width - 1);
        int endY = Math.Min(startY + ChunkTiles, _terrain.Height - 1);

        SurfaceTool surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        surfaceTool.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        int num = 0;
        for (int j = startY; j < endY; j++)
        {
            for (int k = startX; k < endX; k++)
            {
                if (IsGroundCellHidden(k, j))
                {
                    num++;
                    continue;
                }
                TerrainData.Corner c2 = _terrain.Corners[j * width + k];
                Vector4 layers = TileLayers(TileTextureAt(k, j), TileTextureAt(k + 1, j), TileTextureAt(k, j + 1), TileTextureAt(k + 1, j + 1), c2);
                float x = (float)k * 128f;
                float x2 = (float)(k + 1) * 128f;
                float z = (float)j * 128f;
                float z2 = (float)(j + 1) * 128f;
                Vector3 position = new Vector3(x, CornerWorld(_terrain, k, j), z);
                Vector3 position2 = new Vector3(x2, CornerWorld(_terrain, k + 1, j), z);
                Vector3 position3 = new Vector3(x, CornerWorld(_terrain, k, j + 1), z2);
                Vector3 position4 = new Vector3(x2, CornerWorld(_terrain, k + 1, j + 1), z2);
                Vector3 normal = CornerNormal(_terrain, k, j);
                Vector3 normal2 = CornerNormal(_terrain, k + 1, j);
                Vector3 normal3 = CornerNormal(_terrain, k, j + 1);
                Vector3 normal4 = CornerNormal(_terrain, k + 1, j + 1);
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

        int chunkIndex = chunkRow * _chunkColumns + chunkColumn;
        MeshInstance3D chunk = _terrainChunks[chunkIndex];
        if (chunk == null)
        {
            chunk = new MeshInstance3D
            {
                Name = $"MapTerrain_{chunkColumn}_{chunkRow}",
                MaterialOverride = TerrainMaterial
            };
            Root.AddChild(chunk, forceReadableName: false, Node.InternalMode.Disabled);
            _terrainChunks[chunkIndex] = chunk;
        }
        chunk.Mesh = surfaceTool.Commit(null, 0uL);
        return num;
    }

    private bool IsGroundCellHidden(int column, int row)
    {
        if (IsRampEntrance(_terrain, column, row)) return false;
        return IsCliffTile(_terrain, column, row) || _cliffTransitions[column, row] || _covered[column, row];
    }

    private string TileTextureAt(int x, int y)
    {
        if (Blight != null && Blight.IsBlighted(x, y))
        {
            return TerrainTextureLoader.BlightId;
        }
        return RealTileTexture(_terrain, x, y);
    }

    public void TickBlight(float delta, IReadOnlyList<Unit> units, FogOfWar fog)
    {
        if (Blight == null) return;
        _blightTimer -= delta;
        if (_blightTimer > 0f) return;
        _blightTimer = BlightRefreshSeconds;

        if (units != null)
        {
            for (int index = 0; index < units.Count; index++)
            {
                Unit unit = units[index];
                if (unit == null || !unit.Alive || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (unit.Type == null || !string.Equals(unit.Type.Race, "undead", StringComparison.OrdinalIgnoreCase)) continue;

                float radius = unit.BlightRadius > 0f ? unit.BlightRadius : MeleeRules.BlightRadius;
                int painted = Blight.Stamp(unit.Position, radius);
                if (painted > 0)
                {
                    GD.Print($"[BLIGHT] {unit.Type.Name} 铺开腐地 {painted} 格"
                        + $"（世界 {Blight.WorldCells}、显示 {Blight.BlightedCells}）");
                }
            }
        }

        if (!Blight.Refresh(fog, out int minX, out int minY, out int maxX, out int maxY)) return;
        RebuildBlightChunks(minX, minY, maxX, maxY);
    }

    private void RebuildBlightChunks(int minX, int minY, int maxX, int maxY)
    {
        int lastTileColumn = Math.Max(0, _terrain.Width - 2);
        int lastTileRow = Math.Max(0, _terrain.Height - 2);
        int firstColumn = Math.Max(0, minX - 1) / ChunkTiles;
        int lastColumn = Math.Min(lastTileColumn, maxX) / ChunkTiles;
        int firstRow = Math.Max(0, minY - 1) / ChunkTiles;
        int lastRow = Math.Min(lastTileRow, maxY) / ChunkTiles;
        int rebuilt = 0;
        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                BuildTerrainChunk(column, row);
                rebuilt++;
            }
        }
        GD.Print($"[BLIGHT] 显示更新 角点({minX},{minY})-({maxX},{maxY})"
            + $" 显示 {Blight.BlightedCells} 格，重建 {rebuilt} 块");
    }

    private Vector4 TileLayers(string t00, string t10, string t01, string t11, TerrainData.Corner c00)
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

    private float LayerOf(TerrainData.Corner corner, int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count)
        {
            return 0f;
        }
        return _groundLayers.LayerOf(_groundOrder[textureIndex], corner.VariationIndex);
    }

    private static string RealTileTexture(TerrainData terrain, int x, int y)
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
                TerrainData.Corner corner = terrain.Corners[num2 * width + num];
                if (num >= width - 1 || num2 >= height - 1 || !IsCliffTile(terrain, num, num2))
                {
                    continue;
                }
                TerrainData.Corner corner2 = terrain.Corners[num2 * width + num + 1];
                TerrainData.Corner corner3 = terrain.Corners[(num2 + 1) * width + num];
                TerrainData.Corner corner4 = terrain.Corners[(num2 + 1) * width + num + 1];
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
                    string cliffId = terrain.CliffTextures[num3];
                    int minimum = Math.Min(Math.Min(corner.LayerHeight, corner2.LayerHeight),
                        Math.Min(corner3.LayerHeight, corner4.LayerHeight));
                    bool atBottom = terrain.Corners[y * width + x].LayerHeight == minimum;
                    string text = atBottom
                        ? CliffTextureSet.GroundTileFor(cliffId)
                        : CliffTextureSet.UpperTileFor(cliffId);
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

    private static float GroundWorld(TerrainData.Corner corner)
    {
        return (corner.HeightWorld + (float)corner.LayerHeight - 2f) * 128f;
    }

    private static float CornerWorld(TerrainData terrain, int x, int y)
    {
        return GroundWorld(terrain.Corners[y * terrain.Width + x]) + RampLift(terrain, x, y) * 128f;
    }

    private static float RampLift(TerrainData terrain, int x, int y)
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

    private static Vector3 CornerNormal(TerrainData terrain, int x, int y)
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

    private static float InterpolatedHeight(TerrainData terrain, float x, float y)
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

    private static float GroundSurface(TerrainData terrain, int x, int y, float worldX, float worldZ, int baseLayer)
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

    private static char RampChar(TerrainData.Corner corner, int baseLayer)
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

    private static void MarkFullyCoveredTiles(TerrainData terrain, List<Vector3> triangles, bool[,] covered)
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
