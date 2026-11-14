using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

/// <summary>
/// 战争迷雾：只维护一张 R8 可见度纹理（一格 128 世界单位 = 一个地形格），不画任何几何。
/// 三态 255 可见 / 128 已探索 / 0 未探索，30ms 一步、每步最多 9 级向目标靠拢，视野进出是渐变的。
/// 地形、悬崖、水面、模型在自己的片元里采样这张图（客户端同款做法）。
/// </summary>
public sealed partial class FogOfWar : Node3D
{
    public const float FogCellSize = 128f;

    private const byte VisibleValue = 255;
    private const byte FoggedValue = 128;
    private const byte BlackValue = 0;
    private const float FadeInterval = 0.03f;
    private const int FadeStep = 9;
    private const int MaxStepsPerFrame = 8;

    private TerrainData _terrain;
    private int _width;
    private int _height;
    private bool[] _visible;
    private bool[] _explored;
    private byte[] _target;
    private byte[] _visual;
    private Image _image;
    private ImageTexture _texture;
    private Image _minimapImage;
    private ImageTexture _minimapTexture;
    private float _fadeAccum;
    private bool _uploaded;
    private int _logBudget = 3;

    private bool _enabled = true;

    /// <summary>关掉迷雾时整张图按「全可见」处理，并且立刻生效（不走淡变）。</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (!_enabled) FillVisible();
        }
    }

    public float LastVisibleRatio { get; private set; }

    public override void _EnterTree()
    {
        SceneTree tree = GetTree();
        if (tree != null) tree.NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        SceneTree tree = GetTree();
        if (tree != null) tree.NodeAdded -= OnNodeAdded;
    }

    private static void OnNodeAdded(Node node)
    {
        if (node is MeshInstance3D mesh) SceneLightRig.ForEachMaterial(mesh, FogOfWarUniforms.Register);
    }

    public void Build(TerrainData terrain)
    {
        _terrain = terrain;
        FogRules.Load();

        _width = Mathf.Max(1, terrain.Width);
        _height = Mathf.Max(1, terrain.Height);
        _visible = new bool[_width * _height];
        _explored = new bool[_width * _height];
        _target = new byte[_width * _height];
        _visual = new byte[_width * _height];

        _image = Image.CreateEmpty(_width, _height, false, Image.Format.R8);
        _image.Fill(new Color(0f, 0f, 0f));
        _texture = ImageTexture.CreateFromImage(_image);

        _minimapImage = Image.CreateEmpty(_width, _height, false, Image.Format.Rgba8);
        _minimapImage.Fill(FogRules.BlackMaskedTerrain);
        _minimapTexture = ImageTexture.CreateFromImage(_minimapImage);

        if (!_enabled) Array.Fill(_visual, VisibleValue);
        Upload();
        SceneLightRig.ForEachMaterial(GetParent(), FogOfWarUniforms.Register);
        Name = "FogOfWar";
    }

    private void FillVisible()
    {
        if (_visual == null) return;
        Array.Fill(_visual, VisibleValue);
        Array.Fill(_target, VisibleValue);
        Upload();
    }

    /// <summary>按视野源重算可见格（单位视野 + 技能给的临时揭示区域）。</summary>
    public void Refresh(IReadOnlyList<Unit> sources, int player, Func<int, int, bool> allied, Func<Unit, float> sightOf,
        IReadOnlyList<FogReveal> reveals = null)
    {
        if (_terrain == null) return;
        if (!_enabled)
        {
            FillVisible();
            return;
        }

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
            _target[index] = _visible[index] ? VisibleValue
                : (_explored[index] ? FoggedValue : BlackValue);
        }
        LastVisibleRatio = (float)visibleCount / _visible.Length;

        if (_logBudget > 0)
        {
            _logBudget--;
            GD.Print($"[FOG] 视野 {visibleCount}/{_visible.Length} 格（{LastVisibleRatio:P0}）网格 {_width}x{_height}");
        }
    }

    /// <summary>每帧推进淡变：30ms 一步，每步向目标最多挪 9 级（255 级约 0.85 秒走完）。</summary>
    public void TickFade(float delta)
    {
        if (_visual == null) return;
        if (!_enabled) return;

        _fadeAccum += delta;
        int steps = 0;
        while (_fadeAccum >= FadeInterval && steps < MaxStepsPerFrame)
        {
            _fadeAccum -= FadeInterval;
            steps++;
        }
        if (steps == 0)
        {
            if (!_uploaded) Upload();
            return;
        }

        int limit = Mathf.Min(255, steps * FadeStep);
        bool changed = false;
        for (int i = 0; i < _visual.Length; i++)
        {
            int current = _visual[i];
            int distance = _target[i] - current;
            if (distance == 0) continue;
            _visual[i] = (byte)(current + Mathf.Clamp(distance, -limit, limit));
            changed = true;
        }
        if (changed || !_uploaded) Upload();
    }

    private void Upload()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int index = y * _width + x;
                byte level = _visual[index];
                _image.SetPixel(x, y, new Color(level / 255f, level / 255f, level / 255f));
                _minimapImage.SetPixel(x, _height - 1 - y, MinimapColor(index));
            }
        }
        _texture.Update(_image);
        _minimapTexture.Update(_minimapImage);
        _uploaded = true;
        FogOfWarUniforms.Publish(_texture, SampleUv, _enabled);
    }

    private Color MinimapColor(int index)
    {
        if (_visible[index]) return new Color(0f, 0f, 0f, 0f);
        return _explored[index] ? FogRules.FoggedTerrain : FogRules.BlackMaskedTerrain;
    }

    private void Stamp(Vector3 world, float sight)
    {
        if (sight <= 0f) return;

        float centerI = -world.X / FogCellSize;
        float centerJ = world.Z / FogCellSize;
        float radius = sight / FogCellSize;

        int minI = Mathf.Max(0, Mathf.FloorToInt(centerI - radius));
        int maxI = Mathf.Min(_width - 1, Mathf.CeilToInt(centerI + radius));
        int minJ = Mathf.Max(0, Mathf.FloorToInt(centerJ - radius));
        int maxJ = Mathf.Min(_height - 1, Mathf.CeilToInt(centerJ + radius));
        float radiusSq = radius * radius;

        for (int j = minJ; j <= maxJ; j++)
        {
            float dj = j - centerJ;
            for (int i = minI; i <= maxI; i++)
            {
                float di = i - centerI;
                if (di * di + dj * dj > radiusSq) continue;
                _visible[j * _width + i] = true;
            }
        }
    }

    /// <summary>世界坐标当前是否在视野内（用于隐藏敌方单位/小地图打点）。</summary>
    public bool IsVisible(Vector3 world)
    {
        if (!_enabled || _visible == null) return true;

        int i = Mathf.FloorToInt(-world.X / FogCellSize);
        int j = Mathf.FloorToInt(world.Z / FogCellSize);
        if (i < 0 || j < 0 || i >= _width || j >= _height) return false;
        return _visible[j * _width + i];
    }

    public bool IsExplored(Vector3 world)
    {
        if (!_enabled || _explored == null) return true;

        int i = Mathf.FloorToInt(-world.X / FogCellSize);
        int j = Mathf.FloorToInt(world.Z / FogCellSize);
        if (i < 0 || j < 0 || i >= _width || j >= _height) return false;
        return _explored[j * _width + i];
    }

    public bool Dirty => false;

    /// <summary>给小地图用的雾贴图（行 0 是南边，画的时候要翻转 V）。</summary>
    public Texture2D Texture => _minimapTexture;

    public Texture2D MinimapTexture => _minimapTexture;

    /// <summary>世界着色器采样的 R8 可见度纹理。</summary>
    public Texture2D VisibilityTexture => _texture;

    /// <summary>
    /// 世界坐标 → 可见度纹理 UV：u = -x / (格宽*格数) + 半格，v = z / (格高*格数) + 半格。
    /// 半格偏移让纹理中心落在网格交点上（客户端 FogOfWarUvp 同款），否则地图边缘会偏半格。
    /// </summary>
    public Vector4 SampleUv => new(-1f / (_width * FogCellSize), 1f / (_height * FogCellSize),
        0.5f / _width, 0.5f / _height);
}
