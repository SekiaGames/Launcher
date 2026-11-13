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
    readonly bool _hasArea;
    readonly float _areaMinX, _areaMinZ, _areaMaxX, _areaMaxZ;
    readonly int _cellsPerSide;
    AudioStreamPlayer _ambient;
    int _baseCol = int.MinValue;
    int _baseRow = int.MinValue;
    bool _enabled = true;

    internal WeatherDefinition Definition => _definition;

    WeatherRenderer(Node parent, WeatherDefinition definition, WeatherType weather,
        Func<float, float, float> terrainHeight, bool hasArea,
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

        StandardMaterial3D material = BuildMaterial(definition, texture);
        float maxScale = Mathf.Max(1e-4f, Mathf.Max(definition.ScaleStart,
            Mathf.Max(definition.ScaleMid, definition.ScaleEnd)));
        var mesh = new QuadMesh
        {
            Size = new Vector2(maxScale * 2f, maxScale * 2f),
            Material = material,
        };
        Curve scale = BuildScaleCurve(definition, maxScale);
        Gradient colors = BuildColorRamp(definition);
        float frames = Math.Max(1, definition.TextureColumns * definition.TextureRows);
        float uvRange = definition.UvEnd - definition.UvStart;
        float animSpeed = frames > 1f ? Math.Abs(uvRange) / (frames - 1f) : 0f;
        float animOffset = frames > 1f ? definition.UvStart / (frames - 1f) : 0f;
        Vector3 direction = ToWorldDirection(definition.Direction);
        if (definition.Velocity < 0f) direction = -direction;
        if (direction.LengthSquared() < 1e-6f) direction = Vector3.Down;
        float speed = Mathf.Abs(definition.Velocity);
        float spread = Mathf.RadToDeg(Mathf.Atan(Mathf.Max(0f, definition.Variance)));

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
                ScaleAmountMin = 1f,
                ScaleAmountMax = 1f,
                ScaleAmountCurve = scale,
                ColorRamp = colors,
                AnimSpeedMin = animSpeed,
                AnimSpeedMax = animSpeed,
                AnimOffsetMin = animOffset,
                AnimOffsetMax = animOffset,
            };
            _root.AddChild(emitter);
            _emitters.Add(emitter);
        }

        StartAmbient();
        GD.Print($"[WEATHER] {definition.Id} cells={_cellsPerSide}x{_cellsPerSide} "
            + $"perCell={definition.ActiveParticleCount} pool={definition.PoolCapacity} "
            + $"life={definition.LifeSpan:0.##}s height={definition.Height:0.#} "
            + $"alphaMode={definition.AlphaMode} fog={definition.UseFog} "
            + $"uv={definition.UvStart:0.#}/{definition.UvMid:0.#}/{definition.UvEnd:0.#} "
            + $"grid={definition.TextureColumns}x{definition.TextureRows} "
            + $"anim={animSpeed:0.###} ambient={definition.AmbientLabel ?? "-"}");
    }

    public static WeatherRenderer TryCreate(Node parent, string effectId,
        Func<float, float, float> terrainHeight, Vector3 boundsMin = default,
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
        return new WeatherRenderer(parent, definition, type, terrainHeight,
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

        int slot = 0;
        float half = WeatherDefinition.CellSize * 0.5f;
        for (int row = 0; row < _cellsPerSide; row++)
        {
            for (int col = 0; col < _cellsPerSide; col++)
            {
                if (slot >= _emitters.Count) break;
                int cellCol = baseCol + col;
                int cellRow = baseRow + row;
                float x = -(cellCol + 0.5f) * WeatherDefinition.CellSize;
                float z = (cellRow + 0.5f) * WeatherDefinition.CellSize;
                CpuParticles3D emitter = _emitters[slot];
                if (_hasArea && (x + half < _areaMinX || x - half > _areaMaxX
                    || z + half < _areaMinZ || z - half > _areaMaxZ))
                {
                    emitter.Emitting = false;
                    continue;
                }
                float ground = _terrainHeight(x, z);
                if (!float.IsFinite(ground)) ground = 0f;
                emitter.Position = new Vector3(x,
                    ground + Mathf.Max(1f, _definition.Height), z);
                emitter.Emitting = _enabled;
                emitter.Restart();
                slot++;
            }
        }
        for (; slot < _emitters.Count; slot++) _emitters[slot].Emitting = false;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!GodotObject.IsInstanceValid(_root)) return;
        foreach (CpuParticles3D emitter in _emitters)
            if (GodotObject.IsInstanceValid(emitter)) emitter.Emitting = enabled;
        if (!GodotObject.IsInstanceValid(_ambient)) return;
        if (enabled) _ambient.Play();
        else _ambient.Stop();
    }

    static StandardMaterial3D BuildMaterial(WeatherDefinition definition, Texture2D texture)
    {
        var material = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            AlbedoColor = Colors.White,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            DisableFog = !definition.UseFog,
            ParticlesAnimHFrames = definition.TextureColumns,
            ParticlesAnimVFrames = definition.TextureRows,
            ParticlesAnimLoop = false,
        };
        if (definition.AlphaMode == 1) material.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        return material;
    }

    static Curve BuildScaleCurve(WeatherDefinition definition, float maxScale)
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, definition.ScaleStart / maxScale), 0f, 0f,
            Curve.TangentMode.Linear, Curve.TangentMode.Linear);
        curve.AddPoint(new Vector2(definition.MidTime, definition.ScaleMid / maxScale), 0f, 0f,
            Curve.TangentMode.Linear, Curve.TangentMode.Linear);
        curve.AddPoint(new Vector2(1f, definition.ScaleEnd / maxScale), 0f, 0f,
            Curve.TangentMode.Linear, Curve.TangentMode.Linear);
        return curve;
    }

    static Gradient BuildColorRamp(WeatherDefinition definition)
    {
        return new Gradient
        {
            Offsets = new[] { 0f, definition.MidTime, 1f },
            Colors = new[]
            {
                ToColor(definition.ColorStart, definition.AlphaStart),
                ToColor(definition.ColorMid, definition.AlphaMid),
                ToColor(definition.ColorEnd, definition.AlphaEnd),
            },
        };
    }

    static Color ToColor(NVector3 color, float alpha)
        => new(Mathf.Clamp(color.X, 0f, 1f), Mathf.Clamp(color.Y, 0f, 1f),
            Mathf.Clamp(color.Z, 0f, 1f), Mathf.Clamp(alpha, 0f, 1f));

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