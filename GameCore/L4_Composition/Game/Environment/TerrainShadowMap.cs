using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class TerrainShadowMap
{
    private const float TexelWorldSize = 32f;
    private const float StrengthRefreshThreshold = 0.02f;

    private sealed class ShadowStamp
    {
        public byte[] Pixels;
        public int Width;
        public int Height;
        public int Column;
        public int TopRow;
        public Rect2I Bounds;
        public TreeState Tree;
        public bool Alive = true;
    }

    private readonly int _columns;
    private readonly int _rows;
    private readonly byte[] _base;
    private readonly byte[] _treeAlpha;
    private readonly byte[] _result;
    private readonly Image _image;
    private readonly ImageTexture _texture;
    private readonly List<ShadowStamp> _stamps = new();
    private float _strength = float.NaN;
    private Rect2I _dirtyTrees;
    private bool _logged;

    public TerrainShadowMap(W3eTerrain terrain, ShaderMaterial material,
        IReadOnlyList<MapWorldBuilder.TreeShadow> treeShadows)
    {
        _columns = Math.Max(1, (terrain.Width - 1) * 4);
        _rows = Math.Max(1, (terrain.Height - 1) * 4);
        _base = new byte[_columns * _rows];
        _treeAlpha = new byte[_base.Length];
        _result = new byte[_base.Length];

        int bakedBytes = ReadBakedShadow();
        int missingTextures = AddStamps(treeShadows);

        _image = Image.CreateFromData(_columns, _rows, false, Image.Format.L8, _result);
        _texture = ImageTexture.CreateFromImage(_image);

        material.SetShaderParameter("shadow_map", _texture);
        material.SetShaderParameter("shadow_scale",
            new Vector2(1f / (_columns * TexelWorldSize), 1f / (_rows * TexelWorldSize)));
        material.SetShaderParameter("shadow_strength", 1f);
        material.SetShaderParameter("shadow_enabled", true);

        RebuildTreeAlpha(new Rect2I(0, 0, _columns, _rows));
        GD.Print($"[SHADOW] 阴影图 {_columns}x{_rows}，war3map.shd {bakedBytes} 字节，"
            + $"投影 {_stamps.Count} 个，缺贴图 {missingTextures}");
    }

    public void Update(float strength)
    {
        strength = float.IsFinite(strength) ? Mathf.Clamp(strength, 0f, 1f) : 1f;
        bool treesChanged = RefreshAliveStamps();
        if (treesChanged)
        {
            RebuildTreeAlpha(_dirtyTrees);
        }
        else if (Mathf.Abs(strength - _strength) < StrengthRefreshThreshold)
        {
            return;
        }

        _strength = strength;
        Compose();
        _image.SetData(_columns, _rows, false, Image.Format.L8, _result);
        _texture.Update(_image);
        if (_logged) return;
        _logged = true;
        LogComposed();
    }

    private int ReadBakedShadow()
    {
        byte[] data = Assets.ReadBytes("res://gmap/map/war3map.shd");
        if (data == null) return 0;
        int count = Math.Min(_base.Length, data.Length);
        for (int i = 0; i < count; i++)
        {
            _base[i] = (byte)(data[i] / 2);
        }
        return data.Length;
    }

    private int AddStamps(IReadOnlyList<MapWorldBuilder.TreeShadow> treeShadows)
    {
        if (treeShadows == null) return 0;

        var textures = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        int missing = 0;
        foreach (MapWorldBuilder.TreeShadow shadow in treeShadows)
        {
            if (string.IsNullOrWhiteSpace(shadow.TextureName)) continue;
            string name = shadow.TextureName.Trim();
            if (!textures.TryGetValue(name, out Image image))
            {
                image = War3Assets.LoadImage(ShadowTexturePath(name));
                textures[name] = image;
            }
            if (image == null)
            {
                missing++;
                continue;
            }

            var stamp = new ShadowStamp
            {
                Pixels = image.GetData(),
                Width = image.GetWidth(),
                Height = image.GetHeight(),
                Column = Mathf.FloorToInt(-shadow.Position.X / TexelWorldSize) - Mathf.RoundToInt(image.GetWidth() * 0.3f),
                TopRow = Mathf.FloorToInt(shadow.Position.Z / TexelWorldSize) + Mathf.RoundToInt(image.GetHeight() * 0.7f),
                Tree = shadow.Tree,
            };
            stamp.Bounds = StampBounds(stamp);
            _stamps.Add(stamp);
        }
        return missing;
    }

    private static string ShadowTexturePath(string name)
    {
        string path = name.Replace('/', '\\');
        if (!path.StartsWith("ReplaceableTextures", StringComparison.OrdinalIgnoreCase))
        {
            path = "ReplaceableTextures\\Shadows\\" + path;
        }
        return System.IO.Path.HasExtension(path) ? path : path + ".blp";
    }

    private Rect2I StampBounds(ShadowStamp stamp)
    {
        int left = Math.Max(0, stamp.Column);
        int right = Math.Min(_columns, stamp.Column + stamp.Width);
        int top = Math.Max(0, stamp.TopRow - stamp.Height + 1);
        int bottom = Math.Min(_rows, stamp.TopRow + 1);
        return new Rect2I(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private bool RefreshAliveStamps()
    {
        Rect2I dirty = default;
        bool changed = false;
        foreach (ShadowStamp stamp in _stamps)
        {
            bool alive = stamp.Tree == null || stamp.Tree.Alive;
            if (alive == stamp.Alive) continue;
            stamp.Alive = alive;
            dirty = Union(dirty, stamp.Bounds);
            changed = true;
        }
        _dirtyTrees = dirty;
        return changed;
    }

    private static Rect2I Union(Rect2I a, Rect2I b)
    {
        if (a.Size.X <= 0 || a.Size.Y <= 0) return b;
        if (b.Size.X <= 0 || b.Size.Y <= 0) return a;
        int left = Math.Min(a.Position.X, b.Position.X);
        int top = Math.Min(a.Position.Y, b.Position.Y);
        int right = Math.Max(a.Position.X + a.Size.X, b.Position.X + b.Size.X);
        int bottom = Math.Max(a.Position.Y + a.Size.Y, b.Position.Y + b.Size.Y);
        return new Rect2I(left, top, right - left, bottom - top);
    }

    private void RebuildTreeAlpha(Rect2I region)
    {
        region = region.Intersection(new Rect2I(0, 0, _columns, _rows));
        if (region.Size.X <= 0 || region.Size.Y <= 0) return;

        int left = region.Position.X;
        int top = region.Position.Y;
        int right = left + region.Size.X;
        int bottom = top + region.Size.Y;
        for (int row = top; row < bottom; row++)
        {
            Array.Clear(_treeAlpha, row * _columns + left, region.Size.X);
        }

        foreach (ShadowStamp stamp in _stamps)
        {
            if (!stamp.Alive) continue;
            int columnStart = Math.Max(left, stamp.Column);
            int columnEnd = Math.Min(right, stamp.Column + stamp.Width);
            int rowStart = Math.Max(top, stamp.TopRow - stamp.Height + 1);
            int rowEnd = Math.Min(bottom, stamp.TopRow + 1);
            for (int row = rowStart; row < rowEnd; row++)
            {
                int pixelRow = stamp.TopRow - row;
                if (pixelRow < 0 || pixelRow >= stamp.Height) continue;
                int target = row * _columns + columnStart;
                int source = (pixelRow * stamp.Width + columnStart - stamp.Column) * 4 + 3;
                for (int column = columnStart; column < columnEnd; column++, source += 4, target++)
                {
                    byte alpha = stamp.Pixels[source];
                    if (alpha > _treeAlpha[target]) _treeAlpha[target] = alpha;
                }
            }
        }
    }

    private void Compose()
    {
        for (int i = 0; i < _result.Length; i++)
        {
            int trees = _strength <= 0f ? 0 : (int)MathF.Round(_treeAlpha[i] * _strength);
            if (trees > 255) trees = 255;
            _result[i] = (byte)(_base[i] > trees ? _base[i] : trees);
        }
    }

    private void LogComposed()
    {
        int darkest = 0;
        long total = 0;
        foreach (byte value in _result)
        {
            total += value;
            if (value > darkest) darkest = value;
        }
        GD.Print($"[SHADOW] 合成 强度 {_strength:0.00}，最深 {darkest}，"
            + $"均值 {(double)total / _result.Length:0.0}");
    }
}
