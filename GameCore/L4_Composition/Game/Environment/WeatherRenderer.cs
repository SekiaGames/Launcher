using Godot;
using System;
using System.IO;
using NVector3 = System.Numerics.Vector3;

namespace OpenWC3.Game;

public sealed class WeatherRenderer
{
    const int GlobalCellsPerSide = 5;
    const int AreaCellsPerSide = 7;

    readonly Node3D _root;
    readonly WeatherDefinition _definition;
    readonly WeatherType _weather;
    readonly Func<float, float, float> _terrainHeight;
    readonly float _cosAngleX, _sinAngleX, _cosAngleY, _sinAngleY;
    readonly bool _hasArea;
    readonly float _areaMinX, _areaMinZ, _areaMaxX, _areaMaxZ;
    readonly int _cellsPerSide;
    readonly int _perCell;
    MultiMesh _multimesh;
    MultiMeshInstance3D _instanceNode;
    AudioStreamPlayer _ambient;
    float _userVolume = 1f;
    float _ambientGain = 1f;

    public void SetVolume(float value)
    {
        _userVolume = Mathf.Clamp(float.IsFinite(value) ? value : 1f, 0f, 1f);
        if (_ambient != null)
            _ambient.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, _ambientGain * _userVolume));
    }
    int _baseCol = int.MinValue;
    int _baseRow = int.MinValue;
    int _activeInstances;
    bool _enabled = true;

    internal WeatherDefinition Definition => _definition;

    WeatherRenderer(Node parent, WeatherDefinition definition, WeatherType weather,
        Func<float, float, float> terrainHeight, War3FogConfig fog, bool hasArea,
        Vector3 boundsMin, Vector3 boundsMax)
    {
        _definition = definition;
        _weather = weather;
        _terrainHeight = terrainHeight ?? ((_, _) => 0f);
        _hasArea = hasArea;
        _areaMinX = Mathf.Min(boundsMin.X, boundsMax.X);
        _areaMinZ = Mathf.Min(boundsMin.Z, boundsMax.Z);
        _areaMaxX = Mathf.Max(boundsMin.X, boundsMax.X);
        _areaMaxZ = Mathf.Max(boundsMin.Z, boundsMax.Z);
        _cellsPerSide = hasArea ? AreaCellsPerSide : GlobalCellsPerSide;
        _perCell = Math.Max(1, definition.ActiveParticleCount);

        float angleX = definition.AngleX * WeatherDefinition.DegreesToRadians;
        float angleY = definition.AngleY * WeatherDefinition.DegreesToRadians;
        _cosAngleX = MathF.Cos(angleX);
        _sinAngleX = MathF.Sin(angleX);
        _cosAngleY = MathF.Cos(angleY);
        _sinAngleY = MathF.Sin(angleY);

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
        int capacity = _cellsPerSide * _cellsPerSide * _perCell;
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = mesh,
            InstanceCount = capacity,
        };
        _multimesh.VisibleInstanceCount = 0;
        _instanceNode = new MultiMeshInstance3D
        {
            Name = "Cells",
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-20000f, -20000f, -20000f),
                new Vector3(40000f, 40000f, 40000f)),
        };
        _root.AddChild(_instanceNode);

        StartAmbient();
        GD.Print($"[WEATHER] {definition.Id} cells={_cellsPerSide}x{_cellsPerSide} "
            + $"perCell={_perCell} capacity={capacity} "
            + $"life={definition.LifeSpan:0.##}s height={definition.Height:0.#} "
            + $"veloc={definition.Velocity:0.#} var={definition.Variance:P0} "
            + $"angx={definition.AngleX:0.#} angy={definition.AngleY:0.#} "
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
        if (!GodotObject.IsInstanceValid(_root) || _multimesh == null) return;
        int centerCol = Mathf.FloorToInt(-focus.X / WeatherDefinition.CellSize);
        int centerRow = Mathf.FloorToInt(focus.Z / WeatherDefinition.CellSize);
        int baseCol = centerCol - _cellsPerSide / 2;
        int baseRow = centerRow - _cellsPerSide / 2;
        if (baseCol == _baseCol && baseRow == _baseRow) return;
        _baseCol = baseCol;
        _baseRow = baseRow;

        RebuildVisibleWindow(baseCol, baseRow);
        GD.Print($"[WEATHER] {_definition.Id} 跨格 ({baseCol},{baseRow})："
            + $"重建实例 {_activeInstances}，容量 {_multimesh.InstanceCount}");
    }

    void RebuildVisibleWindow(int baseCol, int baseRow)
    {
        int capacity = _multimesh.InstanceCount;
        int cursor = 0;
        float half = WeatherDefinition.CellSize * 0.5f;
        float lifeSpan = _definition.LifeSpan;
        for (int row = 0; row < _cellsPerSide; row++)
        {
            for (int col = 0; col < _cellsPerSide; col++)
            {
                int cellCol = baseCol + col;
                int cellRow = baseRow + row;
                float x = -(cellCol + 0.5f) * WeatherDefinition.CellSize;
                float z = (cellRow + 0.5f) * WeatherDefinition.CellSize;
                if (_hasArea && (x + half < _areaMinX || x - half > _areaMaxX
                    || z + half < _areaMinZ || z - half > _areaMaxZ))
                {
                    continue;
                }

                float ground = _terrainHeight(x, z);
                if (!float.IsFinite(ground)) ground = 0f;
                float baseY = ground + Mathf.Max(1f, _definition.Height);
                var rng = new Random(CellSeed(_weather, cellCol, cellRow));
                for (int i = 0; i < _perCell && cursor < capacity; i++)
                {
                    float dx = (rng.NextSingle() * 2f - 1f) * half;
                    float dy = (rng.NextSingle() * 2f - 1f) * half;
                    Vector3 scatter = ScatterOffset(dx, dy);
                    Vector3 origin = new(x + scatter.X, baseY + scatter.Y, z + scatter.Z);
                    Vector3 velocity = GetVelocity(rng);
                    float phase = rng.NextSingle() * lifeSpan;
                    _multimesh.SetInstanceTransform(cursor,
                        new Transform3D(Basis.Identity, origin));
                    _multimesh.SetInstanceCustomData(cursor,
                        new Color(velocity.X, velocity.Y, velocity.Z, phase));
                    cursor++;
                }
            }
        }
        _activeInstances = cursor;
        _multimesh.VisibleInstanceCount = cursor;
    }

    static int CellSeed(WeatherType weather, int col, int row)
        => unchecked(0x12345678 ^ (int)weather ^ (col * 73856093) ^ (row * 19349663));

    Vector3 ScatterOffset(float dx, float dy)
    {
        float sx = dx * _cosAngleY + dy * _sinAngleX * _sinAngleY;
        float sy = dy * _cosAngleX;
        float sz = -dx * _sinAngleY + dy * _sinAngleX * _cosAngleY;
        return new Vector3(-sx, sz, sy);
    }

    Vector3 GetVelocity(Random rng)
    {
        NVector3 direction = _definition.Direction;
        NVector3 velocity = direction * _definition.Velocity;
        float drift = MathF.Abs(_definition.Velocity) * _definition.Variance;
        if (drift > 0f && float.IsFinite(drift))
        {
            NVector3 seed = MathF.Abs(direction.Z) > 0.9f ? NVector3.UnitX : NVector3.UnitZ;
            NVector3 side = NVector3.Normalize(NVector3.Cross(direction, seed));
            NVector3 up = NVector3.Cross(side, direction);
            velocity += side * (drift * (rng.NextSingle() * 2f - 1f))
                + up * (drift * (rng.NextSingle() * 2f - 1f));
        }
        return ToWorldDirection(velocity);
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (GodotObject.IsInstanceValid(_instanceNode))
            _instanceNode.Visible = enabled;
        if (!GodotObject.IsInstanceValid(_ambient)) return;
        if (enabled) _ambient.Play();
        else _ambient.Stop();
    }

    public void Remove()
    {
        if (GodotObject.IsInstanceValid(_root)) _root.QueueFree();
        _multimesh = null;
        _instanceNode = null;
        _ambient = null;
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
        material.SetShaderParameter("weather_life_span", definition.LifeSpan);
        material.SetShaderParameter("weather_acceleration", definition.Acceleration);
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
        => new(-direction.X, direction.Z, direction.Y);

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
            VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, volume * _userVolume)),
        };
        _ambientGain = volume;
        _root.AddChild(_ambient);
        _ambient.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(_ambient) && _enabled) _ambient.Play();
        };
        _ambient.Play();
    }
}