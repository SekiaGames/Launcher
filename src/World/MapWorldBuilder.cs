using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed class MapWorldBuilder
{
    public const float CellSize = 128f;
    public const float WaterLevel = 14f;

    private static readonly Vector3 CliffLightDirection = new(-0.6095f, 0.5079f, -0.6095f);

    private GroundTextureArray _groundLayers;
    private List<string> _groundOrder = new();
    private bool[,] _cliffTransitions;
    private ShaderMaterial _waterMaterial;
    private int _waterFrameCount = 1;
    private int _waterFrameRate = 1;
    private double _waterElapsed;

    public W3eTerrain Terrain { get; private set; }
    public Node3D Root { get; private set; }
    public float WaterY { get; private set; } = WaterLevel;
    public List<TreeState> Trees { get; } = new();

    public Node3D Build(W3eTerrain terrain, W3oDoodads doodads)
    {
        Terrain = terrain;
        Root = new Node3D { Name = "MapRoot" };
        bool[,] covered = BuildCliffs(terrain);
        BuildTerrain(terrain, covered);
        BuildWater(terrain);
        if (doodads != null) BuildDoodads(terrain, doodads);
        return Root;
    }

    public void TickWater(double delta)
    {
        if (_waterMaterial == null) return;
        _waterElapsed += Math.Max(0d, delta);
        int frame = Mathf.FloorToInt(1f + (float)_waterElapsed * _waterFrameRate) % _waterFrameCount;
        _waterMaterial.SetShaderParameter("frame", (float)frame);
    }

    public static float HeightAt(W3eTerrain terrain, float worldX, float worldZ)
    {
        float gridX = -worldX / CellSize;
        float gridZ = worldZ / CellSize;
        return InterpolatedHeight(terrain, gridX, gridZ);
    }

    private bool[,] BuildCliffs(W3eTerrain terrain)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        var cliffCorner = new bool[width, height];
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                cliffCorner[x, y] = IsCliffTile(terrain, x, y);
            }
        }

        var covered = new bool[width, height];
        _cliffTransitions = new bool[width, height];

        CliffTextureSet cliffSet = CliffTextureSet.Build(terrain.CliffTextures);
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/cliff_map.gdshader"),
        };
        if (cliffSet?.Texture != null)
        {
            material.SetShaderParameter("cliff_texture", cliffSet.Texture);
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);

        int tiles = 0;
        int trans = 0;
        var missing = new Dictionary<string, int>();

        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                if (!cliffCorner[x, y]) continue;

                W3eTerrain.Corner bl = terrain.Corners[y * width + x];
                W3eTerrain.Corner br = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner tl = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner tr = terrain.Corners[(y + 1) * width + x + 1];

                bool facingDown = tl.LayerHeight >= bl.LayerHeight && tr.LayerHeight >= br.LayerHeight;
                bool facingLeft = br.LayerHeight >= bl.LayerHeight && tr.LayerHeight >= tl.LayerHeight;
                if ((facingDown && y == 0) || (!facingDown && y >= height - 2)
                    || (facingLeft && x == 0) || (!facingLeft && x >= width - 2))
                {
                    continue;
                }

                int baseLayer = Math.Min(Math.Min(bl.LayerHeight, br.LayerHeight), Math.Min(tl.LayerHeight, tr.LayerHeight));

                int sideX = x + (facingLeft ? -1 : 1);
                int sideZ = y + (facingDown ? -1 : 1);
                bool alongX = bl.Ramp != br.Ramp && tl.Ramp != tr.Ramp
                              && !CornerFlag(cliffCorner, x + (br.Ramp ? 1 : 0), sideZ);
                bool alongZ = bl.Ramp != tl.Ramp && br.Ramp != tr.Ramp
                              && !CornerFlag(cliffCorner, sideX, y + (tl.Ramp ? 1 : 0));
                if (alongX || alongZ)
                {
                    string variant = string.Concat(
                        RampChar(bl, baseLayer), RampChar(tl, baseLayer),
                        RampChar(tr, baseLayer), RampChar(br, baseLayer));
                    string transModel = CliffMeshFactory.Find("CliffTrans" + variant + "0");
                    if (transModel != null)
                    {
                        int px = x + (alongZ && !facingLeft ? 1 : 0);
                        int pz = y - (alongX && facingDown ? 1 : 0);
                        MarkCovered(st, terrain, transModel, px, pz, baseLayer, cliffSet, bl, covered, false);

                        _cliffTransitions[x, y] = true;
                        _cliffTransitions[px, pz] = true;
                        tiles++;
                        trans++;
                        continue;
                    }
                    missing["CliffTrans" + variant] = missing.GetValueOrDefault("CliffTrans" + variant) + 1;
                }

                if (IsRampEntrance(terrain, x, y)) continue;

                string cliffVariant = string.Concat(
                    (char)('A' + bl.LayerHeight - baseLayer),
                    (char)('A' + tl.LayerHeight - baseLayer),
                    (char)('A' + tr.LayerHeight - baseLayer),
                    (char)('A' + br.LayerHeight - baseLayer));
                if (cliffVariant == "AAAA") continue;

                string model = CliffMeshFactory.ResolveCliff(cliffVariant, bl.CliffVariation);
                if (model == null)
                {
                    missing[cliffVariant] = missing.GetValueOrDefault(cliffVariant) + 1;
                    continue;
                }

                MarkCovered(st, terrain, model, x, y, baseLayer, cliffSet, bl, covered);
                tiles++;
            }
        }

        if (missing.Count > 0)
        {
            GD.PushWarning($"[CLIFF] 缺少模型: {string.Join(",", missing.Keys)}");
        }

        if (tiles > 0)
        {
            Root.AddChild(new MeshInstance3D
            {
                Mesh = st.Commit(),
                MaterialOverride = material,
                Name = "MapCliffs",
            });
        }

        GD.Print($"[CLIFF] 悬崖格 {tiles}（过渡 {trans}），贴图集合 {terrain.CliffTextures.Count} 组");
        return covered;
    }

    private void MarkCovered(SurfaceTool st, W3eTerrain terrain, string model, int x, int y, int baseLayer,
        CliffTextureSet cliffSet, W3eTerrain.Corner bl, bool[,] covered, bool cullGround = true)
    {
        CliffGeometry geometry = CliffMeshFactory.Get(model);
        Vector3 normal = (
            CornerNormal(terrain, x, y) + CornerNormal(terrain, x + 1, y) +
            CornerNormal(terrain, x, y + 1) + CornerNormal(terrain, x + 1, y + 1)).Normalized();
        float layer = CliffLayer(cliffSet, terrain, bl.CliffTexture);

        Vector3 worldNormal = new(-normal.X, normal.Y, normal.Z);
        float shade = Mathf.Clamp(worldNormal.Dot(CliffLightDirection) + 0.45f, 0f, 1f);
        var offset = new Vector3((x + 1) * CellSize, 0f, y * CellSize);

        var triangles = new List<Vector3>();
        for (int i = 0; i + 2 < geometry.Indices.Length; i += 3)
        {
            var tri = new Vector3[3];
            for (int k = 0; k < 3; k++)
            {
                int index = geometry.Indices[i + (k == 1 ? 2 : (k == 2 ? 1 : 0))];
                Vector3 vertex = geometry.Vertices[index] + offset;
                vertex.Y = GroundSurface(terrain, x, y, vertex.X, vertex.Z, baseLayer) + geometry.Vertices[index].Y;
                tri[k] = vertex;
                AddVertex(st, vertex, geometry.Uvs[index], new Vector4(layer, shade, 0f, 0f), normal);
            }
            triangles.Add(tri[0]);
            triangles.Add(tri[1]);
            triangles.Add(tri[2]);
        }

        if (cullGround)
        {
            MarkFullyCoveredTiles(terrain, triangles, covered);
        }
    }

    private void BuildTerrain(W3eTerrain terrain, bool[,] covered)
    {
        TerrainTileSet.SetBlightTileset(terrain.Tileset);

        var order = new List<string>();
        if (terrain.FixedGroundOrder != null)
        {
            order.AddRange(terrain.FixedGroundOrder);
        }
        else
        {
            var used = new HashSet<string>();
            foreach (W3eTerrain.Corner corner in terrain.Corners)
            {
                int id = corner.GroundTexture;
                if (id >= 0 && id < terrain.GroundTextures.Count) used.Add(terrain.GroundTextures[id]);
            }
            foreach (string tileId in terrain.GroundTextures)
            {
                if (used.Contains(tileId)) order.Add(tileId);
            }
        }

        foreach (string cliffId in terrain.CliffTextures)
        {
            string cliffGround = CliffTextureSet.GroundTileFor(cliffId);
            if (!string.IsNullOrEmpty(cliffGround) && !order.Contains(cliffGround)) order.Add(cliffGround);
        }

        if (Array.Exists(terrain.Corners, c => c.Blight)) order.Add(TerrainTileSet.BlightId);

        var tiles = new TerrainTileSet();
        var loaded = new List<string>();
        GroundTextureArray ground = GroundTextureArray.Build(tiles, order, loaded);
        GD.Print($"[MAP] 地表贴图 {loaded.Count}/{order.Count}: {string.Join(",", loaded)}");

        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/terrain_map.gdshader"),
        };
        if (ground?.Texture != null)
        {
            material.SetShaderParameter("ground_texture", ground.Texture);
        }

        _groundLayers = ground;
        _groundOrder = loaded;

        int width = terrain.Width;
        int height = terrain.Height;
        int culled = 0;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                bool transition = _cliffTransitions != null && _cliffTransitions[x, y];
                if (!transition && !IsRampEntrance(terrain, x, y) && (IsCliffTile(terrain, x, y) || covered[x, y]))
                {
                    culled++;
                    continue;
                }

                W3eTerrain.Corner c00 = terrain.Corners[y * width + x];
                W3eTerrain.Corner c10 = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner c01 = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner c11 = terrain.Corners[(y + 1) * width + x + 1];

                var layers = TileLayers(
                    RealTileTexture(terrain, x, y), RealTileTexture(terrain, x + 1, y),
                    RealTileTexture(terrain, x, y + 1), RealTileTexture(terrain, x + 1, y + 1), c00);

                float x0 = x * CellSize;
                float x1 = (x + 1) * CellSize;
                float z0 = y * CellSize;
                float z1 = (y + 1) * CellSize;

                var p00 = new Vector3(x0, CornerWorld(terrain, x, y), z0);
                var p10 = new Vector3(x1, CornerWorld(terrain, x + 1, y), z0);
                var p01 = new Vector3(x0, CornerWorld(terrain, x, y + 1), z1);
                var p11 = new Vector3(x1, CornerWorld(terrain, x + 1, y + 1), z1);

                Vector3 n00 = CornerNormal(terrain, x, y);
                Vector3 n10 = CornerNormal(terrain, x + 1, y);
                Vector3 n01 = CornerNormal(terrain, x, y + 1);
                Vector3 n11 = CornerNormal(terrain, x + 1, y + 1);

                var uv00 = new Vector2(0f, 1f);
                var uv10 = new Vector2(1f, 1f);
                var uv11 = new Vector2(1f, 0f);
                var uv01 = new Vector2(0f, 0f);

                AddVertex(st, p11, uv11, layers, n11);
                AddVertex(st, p10, uv10, layers, n10);
                AddVertex(st, p01, uv01, layers, n01);
                AddVertex(st, p01, uv01, layers, n01);
                AddVertex(st, p10, uv10, layers, n10);
                AddVertex(st, p00, uv00, layers, n00);
            }
        }

        GD.Print($"[MAP] 地面格抠掉 {culled}（悬崖+斜坡过渡）");
        Root.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = material,
            Name = "MapTerrain",
        });
    }

    private Vector4 TileLayers(string t00, string t10, string t01, string t11, W3eTerrain.Corner c00)
    {
        int bl = TextureIndex(_groundOrder, t00);
        int br = TextureIndex(_groundOrder, t10);
        int tl = TextureIndex(_groundOrder, t01);
        int tr = TextureIndex(_groundOrder, t11);

        var ids = new List<int> { bl, br, tl, tr };
        ids.Sort();
        var distinct = new List<int>();
        foreach (int id in ids)
        {
            if (!distinct.Contains(id)) distinct.Add(id);
        }

        int baseId = distinct[0];

        float LayerWithMask(int textureId)
        {
            int mask = 0;
            if (br == textureId) mask |= 1;
            if (bl == textureId) mask |= 2;
            if (tr == textureId) mask |= 4;
            if (tl == textureId) mask |= 8;
            return BaseLayerOf(textureId) + mask;
        }

        float baseLayer = LayerOf(c00, baseId);
        float b = distinct.Count > 1 ? LayerWithMask(distinct[1]) : GroundTextureArray.BlankLayer;
        float g = distinct.Count > 2 ? LayerWithMask(distinct[2]) : GroundTextureArray.BlankLayer;
        float r = distinct.Count > 3 ? LayerWithMask(distinct[3]) : GroundTextureArray.BlankLayer;
        return new Vector4(baseLayer, b, g, r);
    }

    private float BaseLayerOf(int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count) return 0f;
        return _groundLayers.BaseLayer[_groundOrder[textureIndex]];
    }

    private float LayerOf(W3eTerrain.Corner corner, int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count) return 0f;
        return _groundLayers.LayerOf(_groundOrder[textureIndex], corner.VariationIndex);
    }

    private void BuildWater(W3eTerrain terrain)
    {
        WaterTileSet water = WaterTileSet.Load(terrain.Tileset);
        if (water == null) return;

        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/water_map.gdshader"),
        };
        if (water.Texture != null)
        {
            material.SetShaderParameter("water_texture", water.Texture);
        }
        _waterMaterial = material;
        _waterFrameCount = Mathf.Max(1, water.FrameCount);
        _waterFrameRate = Mathf.Max(1, water.FrameRate);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        int width = terrain.Width;
        int height = terrain.Height;
        int tiles = 0;
        var levels = new Dictionary<int, int>();

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                W3eTerrain.Corner c00 = terrain.Corners[y * width + x];
                W3eTerrain.Corner c10 = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner c01 = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner c11 = terrain.Corners[(y + 1) * width + x + 1];
                if (!c00.HasWater && !c10.HasWater && !c01.HasWater && !c11.HasWater) continue;

                float x0 = x * CellSize;
                float x1 = (x + 1) * CellSize;
                float z0 = y * CellSize;
                float z1 = (y + 1) * CellSize;

                var p00 = new Vector3(x0, WaterWorld(c00, water), z0);
                var p10 = new Vector3(x1, WaterWorld(c10, water), z0);
                var p01 = new Vector3(x0, WaterWorld(c01, water), z1);
                var p11 = new Vector3(x1, WaterWorld(c11, water), z1);
                CountLevel(levels, c00);
                CountLevel(levels, c10);
                CountLevel(levels, c01);
                CountLevel(levels, c11);

                var uv00 = new Vector2(x, 1 - y);
                var uv10 = new Vector2(x + 1, 1 - y);
                var uv01 = new Vector2(x, -y);
                var uv11 = new Vector2(x + 1, -y);

                AddWaterVertex(st, p00, uv00, WaterColor(water, terrain, x, y));
                AddWaterVertex(st, p11, uv11, WaterColor(water, terrain, x + 1, y + 1));
                AddWaterVertex(st, p10, uv10, WaterColor(water, terrain, x + 1, y));
                AddWaterVertex(st, p00, uv00, WaterColor(water, terrain, x, y));
                AddWaterVertex(st, p01, uv01, WaterColor(water, terrain, x, y + 1));
                AddWaterVertex(st, p11, uv11, WaterColor(water, terrain, x + 1, y + 1));
                tiles++;
            }
        }

        WaterY = MostCommonLevel(levels, water);
        if (tiles == 0) return;

        Root.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = material,
            Name = "MapWater",
        });
        GD.Print($"[WATER] 水面格 {tiles}，水面 y={WaterY:0.0}，帧数 {water.FrameCount}");
    }

    private void BuildDoodads(W3eTerrain terrain, W3oDoodads doodads)
    {
        var holder = new Node3D { Name = "MapDoodads" };
        Root.AddChild(holder);

        float offsetX = terrain.OffsetX;
        float offsetY = terrain.OffsetY;
        int placed = 0;
        int hidden = 0;
        int dead = 0;
        int missing = 0;

        foreach (DoodadPlacement entry in doodads.Entries)
        {
            if (entry.Hidden) { hidden++; continue; }
            if (entry.Dead) { dead++; continue; }

            DoodadModel model = DoodadMeshLibrary.Get(entry.Id, entry.Variation);
            if (model?.Scene == null) { missing++; continue; }

            var instance = model.Scene.Instantiate<Node3D>();
            instance.Position = new Vector3(-(entry.X - offsetX), entry.Z, entry.Y - offsetY);
            instance.Rotation = new Vector3(0f, Mathf.Pi + entry.Angle, 0f);
            instance.Scale = DoodadScale(model.BaseScale, entry);
            instance.Name = $"{entry.Id}_{placed}";
            ModelLibrary.ApplyMissingMaterials(instance, model.ScenePath);
            DoodadMeshLibrary.ApplyReplaceableTexture(instance, model);
            ApplyUnitTextureFilter(instance);
            holder.AddChild(instance);

            DoodadDefinition definition = DoodadMeshLibrary.Describe(entry.Id);
            if (definition != null && definition.IsTree)
            {
                Trees.Add(new TreeState
                {
                    Node = instance,
                    Position = instance.Position,
                    Id = entry.Id,
                    Hp = definition.Hp,
                });
            }
            placed++;
        }

        GD.Print($"[DOODAD] 装饰物 {placed}（模型 {DoodadMeshLibrary.Count} 种），隐藏 {hidden} 死亡 {dead} 缺模型 {missing}，树 {Trees.Count}");
    }

    private static Vector3 DoodadScale(float baseScale, DoodadPlacement entry)
    {
        float sx = entry.ScaleX > 0f ? entry.ScaleX : 1f;
        float sy = entry.ScaleY > 0f ? entry.ScaleY : 1f;
        float sz = entry.ScaleZ > 0f ? entry.ScaleZ : 1f;
        return new Vector3(baseScale - 1f + sx, baseScale - 1f + sy, baseScale - 1f + sz);
    }

    public static int ApplyUnitTextureFilter(Node node)
    {
        int changed = 0;
        if (node is MeshInstance3D instance && instance.Mesh != null)
        {
            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = instance.MaterialOverride
                    ?? instance.GetSurfaceOverrideMaterial(surface)
                    ?? instance.Mesh.SurfaceGetMaterial(surface);
                if (material is BaseMaterial3D base3D)
                {
                    base3D.TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear;
                    base3D.TextureRepeat = false;
                    changed++;
                }
            }
        }
        foreach (Node child in node.GetChildren()) changed += ApplyUnitTextureFilter(child);
        return changed;
    }

    private static string RealTileTexture(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        bool plainRampCell = false;
        for (int i = -1; i < 1 && !plainRampCell; i++)
        {
            for (int j = -1; j < 1 && !plainRampCell; j++)
            {
                int cx = x + i;
                int cy = y + j;
                if (cx < 0 || cx >= width || cy < 0 || cy >= height) continue;

                W3eTerrain.Corner corner = terrain.Corners[cy * width + cx];
                bool cliff = cx < width - 1 && cy < height - 1 && IsCliffTile(terrain, cx, cy);
                if (!cliff) continue;

                W3eTerrain.Corner br = terrain.Corners[cy * width + cx + 1];
                W3eTerrain.Corner tl = terrain.Corners[(cy + 1) * width + cx];
                W3eTerrain.Corner tr = terrain.Corners[(cy + 1) * width + cx + 1];

                if (corner.Ramp && tl.Ramp && br.Ramp && tr.Ramp)
                {
                    plainRampCell = true;
                    continue;
                }

                int texture = corner.CliffTexture;
                if (texture == 15) texture -= 14;
                if (texture >= 0 && texture < terrain.CliffTextures.Count)
                {
                    string ground = CliffTextureSet.GroundTileFor(terrain.CliffTextures[texture]);
                    if (!string.IsNullOrEmpty(ground)) return ground;
                }
            }
        }

        if (terrain.Corners[y * width + x].Blight) return TerrainTileSet.BlightId;

        int id = terrain.Corners[y * width + x].GroundTexture;
        return id >= 0 && id < terrain.GroundTextures.Count ? terrain.GroundTextures[id] : null;
    }

    private static bool IsCliffTile(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int bl = terrain.Corners[y * width + x].LayerHeight;
        int br = terrain.Corners[y * width + x + 1].LayerHeight;
        int tl = terrain.Corners[(y + 1) * width + x].LayerHeight;
        int tr = terrain.Corners[(y + 1) * width + x + 1].LayerHeight;
        return bl != br || bl != tl || bl != tr;
    }

    private static bool IsRampEntrance(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        W3eTerrain.Corner bl = terrain.Corners[y * width + x];
        W3eTerrain.Corner br = terrain.Corners[y * width + x + 1];
        W3eTerrain.Corner tl = terrain.Corners[(y + 1) * width + x];
        W3eTerrain.Corner tr = terrain.Corners[(y + 1) * width + x + 1];
        return bl.Ramp && tl.Ramp && br.Ramp && tr.Ramp
               && !(bl.LayerHeight == tr.LayerHeight && tl.LayerHeight == br.LayerHeight);
    }

    private static float GroundWorld(W3eTerrain.Corner corner)
    {
        return (corner.HeightWorld + corner.LayerHeight - 2f) * CellSize;
    }

    private static float CornerWorld(W3eTerrain terrain, int x, int y)
    {
        return GroundWorld(terrain.Corners[y * terrain.Width + x]) + RampLift(terrain, x, y) * CellSize;
    }

    private static float RampLift(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int layer = terrain.Corners[y * width + x].LayerHeight;

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int tx = x + ox;
                int ty = y + oy;
                if (tx < 0 || ty < 0 || tx >= width - 1 || ty >= height - 1) continue;
                if (!IsRampEntrance(terrain, tx, ty)) continue;

                int baseLayer = Math.Min(
                    Math.Min(terrain.Corners[ty * width + tx].LayerHeight, terrain.Corners[ty * width + tx + 1].LayerHeight),
                    Math.Min(terrain.Corners[(ty + 1) * width + tx].LayerHeight, terrain.Corners[(ty + 1) * width + tx + 1].LayerHeight));
                if (layer == baseLayer) return 0.5f;
            }
        }
        return 0f;
    }

    private static void AddVertex(SurfaceTool st, Vector3 position, Vector2 uv, Vector4 layers, Vector3 normal)
    {
        st.SetUV(uv);
        st.SetNormal(new Vector3(-normal.X, normal.Y, normal.Z));
        st.SetCustom(0, new Color(layers.X, layers.Y, layers.Z, layers.W));
        st.AddVertex(new Vector3(-position.X, position.Y, position.Z));
    }

    private static Vector3 CornerNormal(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int x0 = Mathf.Max(0, x - 1);
        int x1 = Mathf.Min(width - 1, x + 1);
        int y0 = Mathf.Max(0, y - 1);
        int y1 = Mathf.Min(height - 1, y + 1);

        float nx = (terrain.Corners[y * width + x0].HeightWorld - terrain.Corners[y * width + x1].HeightWorld) / CellSize;
        float nz = (terrain.Corners[y0 * width + x].HeightWorld - terrain.Corners[y1 * width + x].HeightWorld) / CellSize;
        return new Vector3(nx, 2f, nz).Normalized();
    }

    private static int TextureIndex(List<string> used, string tileId)
    {
        if (string.IsNullOrEmpty(tileId)) return 0;
        int index = used.IndexOf(tileId);
        return index < 0 ? 0 : index;
    }

    private static float InterpolatedHeight(W3eTerrain terrain, float x, float y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        float cx = Mathf.Clamp(x, 0f, width - 1.01f);
        float cy = Mathf.Clamp(y, 0f, height - 1.01f);
        int x0 = (int)cx;
        int y0 = (int)cy;
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = cx - x0;
        float ty = cy - y0;

        float h00 = GroundWorld(terrain.Corners[y0 * width + x0]);
        float h10 = GroundWorld(terrain.Corners[y0 * width + x1]);
        float h01 = GroundWorld(terrain.Corners[y1 * width + x0]);
        float h11 = GroundWorld(terrain.Corners[y1 * width + x1]);
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
    }

    private static float GroundSurface(W3eTerrain terrain, int x, int y, float worldX, float worldZ, int baseLayer)
    {
        float tx = Mathf.Clamp((worldX - x * CellSize) / CellSize, 0f, 1f);
        float tz = Mathf.Clamp((worldZ - y * CellSize) / CellSize, 0f, 1f);

        float Corner(int cx, int cz)
        {
            return (terrain.Corners[cz * terrain.Width + cx].HeightWorld + baseLayer - 2f) * CellSize
                + RampLift(terrain, cx, cz) * CellSize;
        }

        float h00 = Corner(x, y);
        float h10 = Corner(x + 1, y);
        float h01 = Corner(x, y + 1);
        float h11 = Corner(x + 1, y + 1);

        if (tx + tz >= 1f)
        {
            return h11 * (tx + tz - 1f) + h10 * (1f - tz) + h01 * (1f - tx);
        }
        return h00 * (1f - tx - tz) + h10 * tx + h01 * tz;
    }

    private static float CliffLayer(CliffTextureSet set, W3eTerrain terrain, int cliffTexture)
    {
        if (set == null || cliffTexture < 0 || cliffTexture >= terrain.CliffTextures.Count) return 0f;
        return set.LayerOf(terrain.CliffTextures[cliffTexture]);
    }

    private static bool CornerFlag(bool[,] grid, int x, int y)
    {
        return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) && grid[x, y];
    }

    private static char RampChar(W3eTerrain.Corner corner, int baseLayer)
    {
        int delta = corner.LayerHeight - baseLayer;
        if (!corner.Ramp) return (char)('A' + delta);
        return delta switch
        {
            0 => 'L',
            1 => 'H',
            _ => 'X',
        };
    }

    private static void MarkFullyCoveredTiles(W3eTerrain terrain, List<Vector3> triangles, bool[,] covered)
    {
        if (triangles.Count == 0) return;

        int width = terrain.Width;
        int height = terrain.Height;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 v in triangles)
        {
            minX = Mathf.Min(minX, v.X);
            maxX = Mathf.Max(maxX, v.X);
            minZ = Mathf.Min(minZ, v.Z);
            maxZ = Mathf.Max(maxZ, v.Z);
        }

        int tx0 = Mathf.Max(0, Mathf.FloorToInt(minX / CellSize));
        int tx1 = Mathf.Min(width - 2, Mathf.FloorToInt(maxX / CellSize - 0.001f));
        int tz0 = Mathf.Max(0, Mathf.FloorToInt(minZ / CellSize));
        int tz1 = Mathf.Min(height - 2, Mathf.FloorToInt(maxZ / CellSize - 0.001f));

        for (int tz = tz0; tz <= tz1; tz++)
        {
            for (int tx = tx0; tx <= tx1; tx++)
            {
                if (covered[tx, tz]) continue;

                bool all = true;
                for (int sz = 1; sz <= 4 && all; sz++)
                {
                    for (int sx = 1; sx <= 4 && all; sx++)
                    {
                        float wx = (tx + sx * 0.2f) * CellSize;
                        float wz = (tz + sz * 0.2f) * CellSize;
                        float ground = InterpolatedHeight(terrain, wx / CellSize, wz / CellSize);

                        bool hit = false;
                        for (int t = 0; t + 2 < triangles.Count; t += 3)
                        {
                            if (TriangleHeightAt(triangles, t, wx, wz, out float surface) && surface >= ground - 2f)
                            {
                                hit = true;
                                break;
                            }
                        }
                        all = hit;
                    }
                }
                covered[tx, tz] = all;
            }
        }
    }

    private static bool TriangleHeightAt(List<Vector3> triangles, int start, float x, float z, out float y)
    {
        Vector3 a = triangles[start];
        Vector3 b = triangles[start + 1];
        Vector3 c = triangles[start + 2];

        float area = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
        if (Mathf.Abs(area) < 1e-4f)
        {
            y = 0f;
            return false;
        }

        float l1 = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / area;
        float l2 = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / area;
        float l3 = 1f - l1 - l2;
        if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f)
        {
            y = 0f;
            return false;
        }

        y = l1 * a.Y + l2 * b.Y + l3 * c.Y;
        return true;
    }

    private static void CountLevel(Dictionary<int, int> levels, W3eTerrain.Corner corner)
    {
        if (!corner.HasWater) return;
        int key = (int)Mathf.Round(corner.WaterWorld * 100f);
        levels[key] = levels.GetValueOrDefault(key) + 1;
    }

    private static float MostCommonLevel(Dictionary<int, int> levels, WaterTileSet water)
    {
        int bestKey = int.MinValue;
        int bestCount = 0;
        foreach (var pair in levels)
        {
            if (pair.Value > bestCount)
            {
                bestCount = pair.Value;
                bestKey = pair.Key;
            }
        }
        return bestCount == 0 ? WaterLevel : (bestKey / 100f + water.HeightOffset) * CellSize;
    }

    private static float WaterWorld(W3eTerrain.Corner corner, WaterTileSet water)
    {
        return (corner.WaterWorld + water.HeightOffset) * CellSize;
    }

    private static Color WaterColor(WaterTileSet water, W3eTerrain terrain, int x, int y)
    {
        const float MinDepth = 10f / 128f;
        const float DeepLevel = 64f / 128f;
        const float MaxDepth = 72f / 128f;

        W3eTerrain.Corner corner = terrain.Corners[y * terrain.Width + x];
        float waterY = corner.WaterWorld + water.HeightOffset;
        float groundY = CornerWorld(terrain, x, y) / CellSize;
        float depth = Mathf.Clamp(waterY - groundY, 0f, 1f);

        if (depth <= DeepLevel)
        {
            float t = Mathf.Clamp((depth - MinDepth) / (DeepLevel - MinDepth), 0f, 1f);
            return water.ShallowMin.Lerp(water.ShallowMax, t);
        }

        float u = Mathf.Clamp((depth - DeepLevel) / (MaxDepth - DeepLevel), 0f, 1f);
        return water.DeepMin.Lerp(water.DeepMax, u);
    }

    private static void AddWaterVertex(SurfaceTool st, Vector3 position, Vector2 uv, Color color)
    {
        st.SetUV(uv);
        st.SetColor(color);
        st.AddVertex(new Vector3(-position.X, position.Y, position.Z));
    }
}