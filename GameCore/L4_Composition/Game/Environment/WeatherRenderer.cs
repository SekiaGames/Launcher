using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using NVector3 = System.Numerics.Vector3;

namespace OpenWC3.Game;

public sealed class WeatherRenderer
{
    const int GlobalCellsPerSide = 5;
    const int AreaCellsPerSide = 7;

    readonly Node3D _root;
    readonly WeatherDefinition _definition;
    readonly Func<float, float, float> _terrainHeight;
    readonly List<CpuParticles3D> _emitters = new();
    readonly Dictionary<long, CpuParticles3D> _cellEmitters = new();
    readonly Stack<CpuParticles3D> _spareEmitters = new();
    readonly bool _hasArea;
    readonly float _areaMinX, _areaMinZ, _areaMaxX, _areaMaxZ;
    readonly int _cellsPerSide;
    AudioStreamPlayer _ambient;
    int _baseCol = int.MinValue;
    int _baseRow = int.MinValue;
    bool _enabled = true;

    internal WeatherDefinition Definition => _definition;

    WeatherRenderer(Node parent, WeatherDefinition definition, WeatherType weather,
        Func<float, float, float> terrainHeight, War3FogConfig fog, bool hasArea,
        Vector3 boundsMin, Vector3 boundsMax)
    {
        _definition = definition;
        _terrainHeight = terrainHeight ?? ((_, _) => 0f);
        _hasArea = hasArea;
        _areaMinX = Mathf.Min(boundsMin.X, boundsMax.X);
        _areaMinZ = Mathf.Min(boundsMin.Z, boundsMax.Z);
        _areaMaxX = Mathf.Max(boundsMin.X, boundsMax.X);
        _areaMaxZ = Mathf.Max(boundsMin.Z, boundsMax.Z);
        _cellsPerSide = hasArea ? AreaCellsPerSide : GlobalCellsPerSide;

        _root = new Node3D { Name = $"Weather_{definition.Id}" };
        parent.AddChild(_root);

        Texture2D texture = LoadTexture(definition.TexturePath);
        if (texture == null)
        {
            GD.PushWarning($"[WEATHER] 贴图缺失 {definition.TexturePath}");
            return;
        }

        ShaderMaterial material = BuildWeatherMaterial(definition, texture, fog);
        var mesh = new QuadMesh
        {
            Size = new Vector2(2f, 2f),
            Material = material,
        };
        Vector3 direction = ToWorldDirection(definition.Direction);
        if (definition.Velocity < 0f) direction = -direction;
        if (direction.LengthSquared() < 1e-6f) direction = Vector3.Down;
        float speed = Mathf.Abs(definition.Velocity);
        float variance = Mathf.Max(0f, definition.Variance);
        float spread = Mathf.RadToDeg(Mathf.Atan(variance * Mathf.Sqrt(2f)));

        int count = _cellsPerSide * _cellsPerSide;
        for (int i = 0; i < count; i++)
        {
            var emitter = new CpuParticles3D
            {
                Name = $"Cell{i}",
                Amount = Math.Max(1, definition.ActiveParticleCount),
                Lifetime = definition.LifeSpan,
                Preprocess = definition.LifeSpan,
                OneShot = false,
                LocalCoords = false,
                Emitting = false,
                FixedFps = 60,
                Mesh = mesh,
                Direction = direction,
                Spread = spread,
                InitialVelocityMin = speed,
                InitialVelocityMax = speed,
                Gravity = new Vector3(0f, -definition.Acceleration, 0f),
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(WeatherDefinition.CellSize * 0.5f, 1f,
                    WeatherDefinition.CellSize * 0.5f),
                VisibilityAabb = new Aabb(new Vector3(-20000f, -20000f, -20000f),
                    new Vector3(40000f, 40000f, 40000f)),
            };
            _root.AddChild(emitter);
            _emitters.Add(emitter);
            _spareEmitters.Push(emitter);
        }

        StartAmbient();
        GD.Print($"[WEATHER] {definition.Id} cells={_cellsPerSide}x{_cellsPerSide} "
            + $"perCell={definition.ActiveParticleCount} pool={definition.PoolCapacity} "
            + $"life={definition.LifeSpan:0.##}s height={definition.Height:0.#} "
            + $"veloc={speed:0.#} 抖动 var={variance:P0}（锥角 {spread:0.0}°）"
            + $"alphaMode={definition.AlphaMode} fog={definition.UseFog} "
            + $"uv={definition.UvStart:0.#}/{definition.UvMid:0.#}/{definition.UvEnd:0.#} "
            + $"grid={definition.TextureColumns}x{definition.TextureRows} "
            + $"材质={(definition.AlphaMode == 1 ? "weather_add" : "weather")} "
            + $"ambient={definition.AmbientLabel ?? "-"}");
    }

    public static WeatherRenderer TryCreate(Node parent, string effectId,
        Func<float, float, float> terrainHeight, War3FogConfig fog, Vector3 boundsMin = default,
        Vector3 boundsMax = default, bool areaSpecified = false)
    {
        WeatherType type = WeatherTypeParser.FromRawcode(effectId?.Trim());
        if (type == WeatherType.None) return null;
        WeatherDefinition definition = WeatherDefinition.Load(type);
        if (definition == null)
        {
            GD.PushWarning($"[WEATHER] Weather.slk 缺少 {effectId}");
            return null;
        }
        bool hasArea = areaSpecified
            || (boundsMax.X > boundsMin.X && boundsMax.Z > boundsMin.Z);
        return new WeatherRenderer(parent, definition, type, terrainHeight, fog,
            hasArea, boundsMin, boundsMax);
    }

    public void FrameSeconds(Vector3 focus)
    {
        if (!GodotObject.IsInstanceValid(_root)) return;
        int centerCol = Mathf.FloorToInt(-focus.X / WeatherDefinition.CellSize);
        int centerRow = Mathf.FloorToInt(focus.Z / WeatherDefinition.CellSize);
        int baseCol = centerCol - _cellsPerSide / 2;
        int baseRow = centerRow - _cellsPerSide / 2;
        if (baseCol == _baseCol && baseRow == _baseRow) return;
        _baseCol = baseCol;
        _baseRow = baseRow;

        int released = ReleaseOutside(baseCol, baseRow);
        int acquired = AcquireWindow(baseCol, baseRow);
        if (released > 0 || acquired > 0)
        {
            GD.Print($"[WEATHER] {_definition.Id} 跨格 ({baseCol},{baseRow})："
                + $"放出 {released}，新起 {acquired}，在位 {_cellEmitters.Count}");
        }
    }

    int ReleaseOutside(int baseCol, int baseRow)
    {
        if (_cellEmitters.Count == 0) return 0;
        var leaving = new List<long>();
        foreach (KeyValuePair<long, CpuParticles3D> pair in _cellEmitters)
        {
            int col = (int)(pair.Key >> 32);
            int row = (int)(pair.Key & 0xFFFFFFFFL);
            if (col >= baseCol && col < baseCol + _cellsPerSide
                && row >= baseRow && row < baseRow + _cellsPerSide)
            {
                continue;
            }
            leaving.Add(pair.Key);
        }

        foreach (long key in leaving)
        {
            CpuParticles3D emitter = _cellEmitters[key];
            _cellEmitters.Remove(key);
            emitter.Emitting = false;
            _spareEmitters.Push(emitter);
        }
        return leaving.Count;
    }

    int AcquireWindow(int baseCol, int baseRow)
    {
        int acquired = 0;
        float half = WeatherDefinition.CellSize * 0.5f;
        for (int row = 0; row < _cellsPerSide; row++)
        {
            for (int col = 0; col < _cellsPerSide; col++)
            {
                int cellCol = baseCol + col;
                int cellRow = baseRow + row;
                long key = CellKey(cellCol, cellRow);
                if (_cellEmitters.ContainsKey(key)) continue;
                float x = -(cellCol + 0.5f) * WeatherDefinition.CellSize;
                float z = (cellRow + 0.5f) * WeatherDefinition.CellSize;
                if (_hasArea && (x + half < _areaMinX || x - half > _areaMaxX
                    || z + half < _areaMinZ || z - half > _areaMaxZ))
                {
                    continue;
                }
                if (_spareEmitters.Count == 0) return acquired;

                CpuParticles3D emitter = _spareEmitters.Pop();
                float ground = _terrainHeight(x, z);
                if (!float.IsFinite(ground)) ground = 0f;
                emitter.Position = new Vector3(x,
                    ground + Mathf.Max(1f, _definition.Height), z);
                emitter.Emitting = _enabled;
                if (_enabled) emitter.Restart();
                _cellEmitters[key] = emitter;
                acquired++;
            }
        }
        return acquired;
    }

    static long CellKey(int col, int row) => ((long)col << 32) | (uint)row;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!GodotObject.IsInstanceValid(_root)) return;
        foreach (CpuParticles3D emitter in _cellEmitters.Values)
            if (GodotObject.IsInstanceValid(emitter)) emitter.Emitting = enabled;
        if (!GodotObject.IsInstanceValid(_ambient)) return;
        if (enabled) _ambient.Play();
        else _ambient.Stop();
    }

    public void Remove()
    {
        if (!GodotObject.IsInstanceValid(_root)) return;
        _cellEmitters.Clear();
        _spareEmitters.Clear();
        _emitters.Clear();
        _root.QueueFree();
    }

    static ShaderMaterial BuildWeatherMaterial(WeatherDefinition definition,
        Texture2D texture, War3FogConfig fog)
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>(definition.AlphaMode == 1
                ? "res://assets/shaders/weather_add.gdshader"
                : "res://assets/shaders/weather.gdshader")
        };
        if (texture != null) material.SetShaderParameter("weather_texture", texture);
        material.SetShaderParameter("weather_color_start", ToVector3(definition.ColorStart));
        material.SetShaderParameter("weather_color_mid", ToVector3(definition.ColorMid));
        material.SetShaderParameter("weather_color_end", ToVector3(definition.ColorEnd));
        material.SetShaderParameter("weather_alpha",
            new Vector3(definition.AlphaStart, definition.AlphaMid, definition.AlphaEnd));
        material.SetShaderParameter("weather_scale",
            new Vector3(definition.ScaleStart, definition.ScaleMid, definition.ScaleEnd));
        material.SetShaderParameter("weather_uv_frames",
            new Vector3(definition.UvStart, definition.UvMid, definition.UvEnd));
        material.SetShaderParameter("weather_grid",
            new Vector2(definition.TextureColumns, definition.TextureRows));
        material.SetShaderParameter("weather_mid_time", definition.MidTime);
        material.SetShaderParameter("fog_color", new Vector4(fog.Tint.R, fog.Tint.G, fog.Tint.B, 1f));
        material.SetShaderParameter("fog_params", definition.UseFog ? fog.Params : Vector4.Zero);
        return material;
    }

    static Vector3 ToVector3(NVector3 value) => new(value.X, value.Y, value.Z);

    static Vector3 ToWorldDirection(NVector3 direction)
        => new(direction.X, direction.Z, -direction.Y);

    static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string relative = Path.ChangeExtension(path.Replace('\\', '/'), ".png");
        string resPath = ResolveCaseInsensitive(Assets.Res(relative));
        return Assets.LoadTexture(resPath);
    }

    static string ResolveCaseInsensitive(string resPath)
    {
        if (ResourceLoader.Exists(resPath)) return resPath;
        int slash = resPath.LastIndexOf('/');
        if (slash < 0) return resPath;
        string directory = resPath[..slash];
        string file = resPath[(slash + 1)..];
        using DirAccess dir = DirAccess.Open(directory);
        if (dir == null) return resPath;
        foreach (string candidate in dir.GetFiles())
        {
            string name = candidate.EndsWith(".import", StringComparison.OrdinalIgnoreCase)
                ? candidate[..^".import".Length] : candidate;
            if (!string.Equals(name, file, StringComparison.OrdinalIgnoreCase)) continue;
            return directory + "/" + name;
        }
        return resPath;
    }

    void StartAmbient()
    {
        string label = _definition.AmbientLabel;
        if (label == null) return;
        AudioStream stream = War3SoundTables.Pick(label, out float volume);
        if (stream == null)
        {
            GD.PushWarning($"[WEATHER] 环境音缺失 {label}");
            return;
        }
        _ambient = new AudioStreamPlayer
        {
            Name = "WeatherAmbience",
            Stream = stream,
            VolumeDb = Mathf.LinearToDb(Mathf.Max(0.02f, volume)),
        };
        _root.AddChild(_ambient);
        _ambient.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(_ambient) && _enabled) _ambient.Play();
        };
        _ambient.Play();
    }
}