using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

/// <summary>
/// 战争迷雾/视野：按单位 sight（UnitBalance 的 sight/nsight）点亮格子，
/// 未探索全黑、已探索但当前不可见用 [FogOfWar] 的 FoggedTerrain 压暗。
/// 覆盖层是一张贴着地形起伏的网格 + 一张随视野更新的贴图。
/// </summary>
public sealed partial class FogOfWar : Node3D
{
    /// <summary>一个雾格子相当于几个地形格（32 世界单位/格）。</summary>
    private const int CellsPerTexel = 2;
    private const float FogHeight = 320f;

    private W3eTerrain _terrain;
    private int _width;
    private int _height;
    private float _step;
    private bool[] _visible;
    private bool[] _explored;
    private Image _image;
    private ImageTexture _texture;
    private Image _minimapImage;
    private ImageTexture _minimapTexture;
    private MeshInstance3D _mesh;
    private bool _dirty = true;
    private int _logBudget = 3;

    private bool _enabled = true;

    /// <summary>关掉迷雾时要连同覆盖网格一起隐藏，否则停在全黑未刷新状态会盖住整张地图。</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (_mesh != null)
            {
                _mesh.Visible = value;
            }
        }
    }

    public float LastVisibleRatio { get; private set; }

    public void Build(W3eTerrain terrain)
    {
        _terrain = terrain;
        FogRules.Load();

        int cellsX = Mathf.Max(1, terrain.Width - 1);
        int cellsY = Mathf.Max(1, terrain.Height - 1);
        _width = Mathf.Max(1, cellsX / CellsPerTexel);
        _height = Mathf.Max(1, cellsY / CellsPerTexel);
        _step = MapWorldBuilder.CellSize * CellsPerTexel;

        _visible = new bool[_width * _height];
        _explored = new bool[_width * _height];
        _image = Image.CreateEmpty(_width, _height, false, Image.Format.Rgba8);
        _image.Fill(FogRules.BlackMaskedTerrain);
        _minimapImage = Image.CreateEmpty(_width, _height, false, Image.Format.Rgba8);
        _minimapImage.Fill(FogRules.BlackMaskedTerrain);

        _texture = ImageTexture.CreateFromImage(_image);
        _minimapTexture = ImageTexture.CreateFromImage(_minimapImage);
        BuildMesh();
        _mesh.Visible = _enabled;
        Name = "FogOfWar";
    }

    private void BuildMesh()
    {
        var mesh = new ArrayMesh();
        var vertices = new Vector3[_width * _height * 6];
        var uvs = new Vector2[_width * _height * 6];
        var indices = new int[_width * _height * 6];
        int cursor = 0;

        for (int j = 0; j < _height; j++)
        {
            for (int i = 0; i < _width; i++)
            {
                Vector3 a = Corner(i, j);
                Vector3 b = Corner(i + 1, j);
                Vector3 c = Corner(i + 1, j + 1);
                Vector3 d = Corner(i, j + 1);

                Vector2 ua = new((float)i / _width, (float)j / _height);
                Vector2 ub = new((float)(i + 1) / _width, (float)j / _height);
                Vector2 uc = new((float)(i + 1) / _width, (float)(j + 1) / _height);
                Vector2 ud = new((float)i / _width, (float)(j + 1) / _height);

                AddQuad(vertices, uvs, indices, ref cursor, a, b, c, d, ua, ub, uc, ud);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        _mesh = new MeshInstance3D
        {
            Name = "FogPlane",
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = _texture,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
                NoDepthTest = false,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_mesh);
    }

    private void AddQuad(Vector3[] vertices, Vector2[] uvs, int[] indices, ref int cursor,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
        int baseIndex = cursor;
        vertices[cursor] = a; uvs[cursor] = ua; indices[cursor] = cursor; cursor++;
        vertices[cursor] = b; uvs[cursor] = ub; indices[cursor] = cursor; cursor++;
        vertices[cursor] = c; uvs[cursor] = uc; indices[cursor] = cursor; cursor++;

        vertices[cursor] = a; uvs[cursor] = ua; indices[cursor] = cursor; cursor++;
        vertices[cursor] = c; uvs[cursor] = uc; indices[cursor] = cursor; cursor++;
        vertices[cursor] = d; uvs[cursor] = ud; indices[cursor] = cursor; cursor++;

        _ = baseIndex;
    }

    private Vector3 Corner(int i, int j)
    {
        float mapX = _terrain.OffsetX + i * _step;
        float mapY = _terrain.OffsetY + j * _step;
        float worldX = -(mapX - _terrain.OffsetX);
        float worldZ = mapY - _terrain.OffsetY;
        float height = MapWorldBuilder.HeightAt(_terrain, worldX, worldZ);
        // 抬高到树冠之上：未探索区必须把装饰物也盖住（原版是全黑），
        // 可见格 alpha=0 不会挡任何东西。
        return new Vector3(worldX, height + FogHeight, worldZ);
    }

    /// <summary>按视野源重算可见格（单位视野 + 技能给的临时揭示区域）；返回本次是否真的有变化。</summary>
    public void Refresh(IReadOnlyList<Unit> sources, int player, Func<int, int, bool> allied, Func<Unit, float> sightOf,
        IReadOnlyList<FogReveal> reveals = null)
    {
        if (!Enabled || _terrain == null) return;

        Array.Clear(_visible, 0, _visible.Length);

        foreach (Unit unit in sources)
        {
            if (!unit.Alive) continue;
            bool mine = unit.Player == player || (allied != null && allied(unit.Player, player));
            if (!mine) continue;
            Stamp(unit.Position, sightOf != null ? sightOf(unit) : unit.Type.SightDay);
        }

        if (reveals != null)
        {
            foreach (FogReveal reveal in reveals)
            {
                if (reveal.Player != player && !(allied != null && allied(reveal.Player, player))) continue;
                Stamp(reveal.Center, reveal.Radius);
            }
        }

        int visibleCount = 0;
        for (int index = 0; index < _visible.Length; index++)
        {
            if (_visible[index])
            {
                visibleCount++;
                _explored[index] = true;
            }
        }
        LastVisibleRatio = (float)visibleCount / _visible.Length;
        Repaint();

        if (_logBudget > 0)
        {
            _logBudget--;
            GD.Print($"[FOG] 视野 {visibleCount}/{_visible.Length} 格（{LastVisibleRatio:P0}）网格 {_width}x{_height}");
        }
    }

    private void Stamp(Vector3 world, float sight)
    {
        if (sight <= 0f) return;

        float mapX = _terrain.OffsetX - world.X;
        float mapY = world.Z + _terrain.OffsetY;
        float centerI = (mapX - _terrain.OffsetX) / _step;
        float centerJ = (mapY - _terrain.OffsetY) / _step;
        float radius = sight / _step;

        int minI = Mathf.Max(0, Mathf.FloorToInt(centerI - radius));
        int maxI = Mathf.Min(_width - 1, Mathf.CeilToInt(centerI + radius));
        int minJ = Mathf.Max(0, Mathf.FloorToInt(centerJ - radius));
        int maxJ = Mathf.Min(_height - 1, Mathf.CeilToInt(centerJ + radius));
        float radiusSq = radius * radius;

        for (int j = minJ; j <= maxJ; j++)
        {
            float dj = j + 0.5f - centerJ;
            for (int i = minI; i <= maxI; i++)
            {
                float di = i + 0.5f - centerI;
                if (di * di + dj * dj > radiusSq) continue;
                _visible[j * _width + i] = true;
            }
        }
    }

    private void Repaint()
    {
        Color black = FogRules.BlackMaskedTerrain;
        Color fogged = FogRules.FoggedTerrain;

        for (int j = 0; j < _height; j++)
        {
            for (int i = 0; i < _width; i++)
            {
                int index = j * _width + i;
                Color color = _visible[index] ? new Color(0f, 0f, 0f, 0f) : _explored[index] ? fogged : black;
                _image.SetPixel(i, j, color);
                _minimapImage.SetPixel(i, _height - 1 - j, color);
            }
        }

        _texture.Update(_image);
        _minimapTexture.Update(_minimapImage);
        _dirty = false;
    }

    /// <summary>世界坐标当前是否在视野内（用于隐藏敌方单位/小地图打点）。</summary>
    public bool IsVisible(Vector3 world)
    {
        if (!Enabled || _visible == null) return true;

        float mapX = _terrain.OffsetX - world.X;
        float mapY = world.Z + _terrain.OffsetY;
        int i = Mathf.FloorToInt((mapX - _terrain.OffsetX) / _step);
        int j = Mathf.FloorToInt((mapY - _terrain.OffsetY) / _step);
        if (i < 0 || j < 0 || i >= _width || j >= _height) return false;
        return _visible[j * _width + i];
    }

    public bool IsExplored(Vector3 world)
    {
        if (!Enabled || _explored == null) return true;

        float mapX = _terrain.OffsetX - world.X;
        float mapY = world.Z + _terrain.OffsetY;
        int i = Mathf.FloorToInt((mapX - _terrain.OffsetX) / _step);
        int j = Mathf.FloorToInt((mapY - _terrain.OffsetY) / _step);
        if (i < 0 || j < 0 || i >= _width || j >= _height) return false;
        return _explored[j * _width + i];
    }

    public bool Dirty => _dirty;

    /// <summary>给小地图用的雾贴图（行 0 是南边，画的时候要翻转 V）。</summary>
    public Texture2D Texture => _texture;

    public Texture2D MinimapTexture => _minimapTexture;
}
