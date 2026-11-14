using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>贴地贴花：沿 128 世界单位地形格切片，逐角采地形高，按 贴图+混合档 分批。</summary>
public sealed class ShadowRenderer
{
    internal const float UnitShadowOffset = 3f;
    internal const float GroundSplatOffset = 1f;
    private const float CellSize = 128f;

    public readonly record struct Quad(float X0, float Y0, float X1, float Y1);

    public readonly record struct ColoredQuad(Quad Rectangle, Color Color);

    private sealed class Batch
    {
        public string TexturePath;
        public int BlendMode;
        public readonly List<Vector3> Positions = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<Color> Colors = new();
    }

    private readonly MeshInstance3D _node;
    private readonly W3eTerrain _terrain;
    private readonly Dictionary<string, Shader> _shaders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ShaderMaterial> _materials = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Batch> _batches = new();
    private readonly Dictionary<string, Batch> _batchesByKey = new(StringComparer.Ordinal);
    private readonly ArrayMesh _mesh = new();
    private int _committedSignature;
    private bool _shaderMissingReported;

    public int DrawnQuadCount { get; private set; }

    public ShadowRenderer(Node3D parent, W3eTerrain terrain)
    {
        _node = new MeshInstance3D { Name = "GroundDecals", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(_node);
        _terrain = terrain;
    }

    public void Begin()
    {
        foreach (Batch batch in _batches)
        {
            batch.Positions.Clear();
            batch.Uvs.Clear();
            batch.Colors.Clear();
        }
        _batchesByKey.Clear();
        _batches.Clear();
        DrawnQuadCount = 0;
    }

    public void Draw(IReadOnlyList<Quad> quads, string texturePath, float opacity,
        Color? color = null, float groundOffset = GroundSplatOffset, int blendMode = 0)
    {
        if (quads == null || quads.Count == 0) return;
        Color tint = color ?? new Color(1f, 1f, 1f, 1f);
        tint.A *= opacity;
        for (int i = 0; i < quads.Count; i++)
            Append(quads[i], texturePath, tint, groundOffset, blendMode);
    }

    public void DrawColored(IReadOnlyList<ColoredQuad> quads, string texturePath,
        float groundOffset = GroundSplatOffset, int blendMode = 0)
    {
        if (quads == null || quads.Count == 0) return;
        for (int i = 0; i < quads.Count; i++)
            Append(quads[i].Rectangle, texturePath, quads[i].Color, groundOffset, blendMode);
    }

    private void Append(Quad quad, string texturePath, Color color, float groundOffset, int blendMode)
    {
        if (string.IsNullOrWhiteSpace(texturePath)) return;
        Texture2D texture = Texture(texturePath);
        if (texture == null) return;
        Vector2 size = texture.GetSize();
        if (size.X <= 0f || size.Y <= 0f) return;

        string key = texturePath + "|" + blendMode;
        if (!_batchesByKey.TryGetValue(key, out Batch batch))
        {
            batch = new Batch { TexturePath = texturePath, BlendMode = blendMode };
            _batchesByKey.Add(key, batch);
            _batches.Add(batch);
        }
        BuildQuadVertices(batch, quad, new Vector2(1f / size.X, 1f / size.Y), color, groundOffset);
        DrawnQuadCount++;
    }

    /// <summary>与 TerrainRenderer/BuildTerrain 同一条对角线（TL-BR），逐格角取地形高。</summary>
    private void BuildQuadVertices(Batch batch, Quad quad, Vector2 inverseTextureSize,
        Color color, float groundOffset)
    {
        float width = quad.X1 - quad.X0, height = quad.Y1 - quad.Y0;
        if (width <= 0f || height <= 0f) return;
        float marginX = width * inverseTextureSize.X * 0.5f;
        float marginY = height * inverseTextureSize.Y * 0.5f;
        int ix0 = (int)MathF.Floor((quad.X0 - marginX) / CellSize);
        int ix1 = (int)MathF.Ceiling((quad.X1 + marginX) / CellSize);
        int iy0 = (int)MathF.Floor((quad.Y0 - marginY) / CellSize);
        int iy1 = (int)MathF.Ceiling((quad.Y1 + marginY) / CellSize);
        for (int iy = iy0; iy < iy1; iy++)
        {
            for (int ix = ix0; ix < ix1; ix++)
            {
                float x0 = ix * CellSize;
                float x1 = (ix + 1) * CellSize;
                float y0 = iy * CellSize;
                float y1 = (iy + 1) * CellSize;
                float u0 = (x0 - quad.X0) / width, u1 = (x1 - quad.X0) / width;
                float v0 = 1f - (y0 - quad.Y0) / height;
                float v1 = 1f - (y1 - quad.Y0) / height;

                float tl = HeightAt(x0, y1) + groundOffset;
                float br = HeightAt(x1, y0) + groundOffset;
                float tr = HeightAt(x1, y1) + groundOffset;
                float bl = HeightAt(x0, y0) + groundOffset;

                Put(batch, x0, y1, tl, u0, v1, color);
                Put(batch, x1, y0, br, u1, v0, color);
                Put(batch, x1, y1, tr, u1, v1, color);
                Put(batch, x0, y0, bl, u0, v0, color);
                Put(batch, x1, y0, br, u1, v0, color);
                Put(batch, x0, y1, tl, u0, v1, color);
            }
        }
    }

    private void Put(Batch batch, float mapX, float mapY, float y, float u, float v, Color color)
    {
        batch.Positions.Add(new Vector3(0f - mapX, y, mapY));
        batch.Uvs.Add(new Vector2(u, v));
        batch.Colors.Add(color);
    }

    private float HeightAt(float mapX, float mapY) =>
        MapWorldBuilder.HeightAt(_terrain, 0f - mapX, mapY);

    public void Flush()
    {
        int signature = Signature();
        if (signature == _committedSignature) return;
        _committedSignature = signature;

        _mesh.ClearSurfaces();
        foreach (Batch batch in _batches)
        {
            if (batch.Positions.Count == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = batch.Positions.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = batch.Uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = batch.Colors.ToArray();
            _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }

        _node.Mesh = _mesh;
        int surface = 0;
        foreach (Batch batch in _batches)
        {
            if (batch.Positions.Count == 0) continue;
            ShaderMaterial material = Material(batch.TexturePath, batch.BlendMode);
            if (material == null) continue;
            _node.SetSurfaceOverrideMaterial(surface++, material);
        }
        _node.Visible = surface > 0;
    }

    private int Signature()
    {
        var hash = new HashCode();
        foreach (Batch batch in _batches)
        {
            hash.Add(batch.TexturePath ?? "");
            hash.Add(batch.BlendMode);
            hash.Add(batch.Positions.Count);
            foreach (Vector3 position in batch.Positions)
            {
                hash.Add(position.X);
                hash.Add(position.Y);
                hash.Add(position.Z);
            }
            foreach (Color color in batch.Colors)
            {
                hash.Add(color.R);
                hash.Add(color.G);
                hash.Add(color.B);
                hash.Add(color.A);
            }
        }
        return hash.ToHashCode();
    }

    private Texture2D Texture(string path)
    {
        if (_textures.TryGetValue(path, out Texture2D cached)) return cached;
        Texture2D texture = Assets.LoadTexture(Assets.Res(path));
        _textures[path] = texture;
        return texture;
    }

    private ShaderMaterial Material(string path, int blendMode)
    {
        string key = path + "|" + blendMode;
        if (_materials.TryGetValue(key, out ShaderMaterial cached)) return cached;
        Shader shader = ShaderFor(blendMode);
        if (shader == null) return null;
        var material = new ShaderMaterial { Shader = shader };
        if (blendMode == 3) material.SetShaderParameter("mul_factor", 2f);
        material.SetShaderParameter("decal_texture", Texture(path));
        _materials[key] = material;
        return material;
    }

    private Shader ShaderFor(int blendMode)
    {
        string file = blendMode switch
        {
            1 => "shadow_map_add",
            2 or 3 => "shadow_map_mul",
            _ => "shadow_map",
        };
        if (_shaders.TryGetValue(file, out Shader cached)) return cached;
        Shader shader = GD.Load<Shader>($"res://assets/shaders/{file}.gdshader");
        if (shader == null)
        {
            if (!_shaderMissingReported)
            {
                _shaderMissingReported = true;
                GD.PushWarning($"[DECAL] 缺着色器 {file}.gdshader，需要重新导出 extract.pck");
            }
            return null;
        }
        _shaders[file] = shader;
        return shader;
    }
}
