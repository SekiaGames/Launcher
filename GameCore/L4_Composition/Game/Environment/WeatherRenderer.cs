using Godot;
using System;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class WeatherRenderer
{
    private readonly Node3D _root;
    private readonly CpuParticles3D _rain;
    private readonly float _spawnHeight;

    private WeatherRenderer(Node3D root, CpuParticles3D rain, float spawnHeight)
    {
        _root = root;
        _rain = rain;
        _spawnHeight = spawnHeight;
    }

    public static WeatherRenderer TryCreate(Node parent, string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId)) return null;

        SlkTable table = War3SlkReader.Get("TerrainArtWeather");
        if (table == null) return null;

        int row = table.FindRow("effectID", effectId.Trim());
        if (row < 0) return null;

        string texDir = (table.Get(row, "texDir") ?? "").Replace('\\', '/').Trim('/');
        string texFile = (table.Get(row, "texFile") ?? "").Trim();
        if (texFile.Length == 0) return null;
        if (texFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) texFile = texFile[..^4];

        Texture2D texture = Assets.LoadTexture(Assets.Res($"{texDir}/{texFile}.png"));
        if (texture == null) return null;

        float amount = Mathf.Clamp(Number(table, row, "particles", 120f), 16f, 400f);
        float lifespan = Mathf.Max(0.3f, Number(table, row, "lifespan", 1f));
        float velocity = Number(table, row, "veloc", -500f);
        float acceleration = Number(table, row, "accel", 0f);
        float spawnHeight = Mathf.Max(120f, Number(table, row, "height", 600f));
        float scaleStart = Mathf.Max(0.1f, Number(table, row, "scaleStart", 1f));

        var material = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(
                Mathf.Clamp(Number(table, row, "redStart", 255f) / 255f, 0f, 1f),
                Mathf.Clamp(Number(table, row, "greenStart", 255f) / 255f, 0f, 1f),
                Mathf.Clamp(Number(table, row, "blueStart", 255f) / 255f, 0f, 1f),
                Mathf.Clamp(Number(table, row, "alphaStart", 255f) / 255f, 0f, 1f)),
        };

        var rain = new CpuParticles3D
        {
            Name = $"Weather_{effectId}",
            Amount = (int)amount,
            Lifetime = lifespan,
            Preprocess = lifespan,
            LocalCoords = false,
            Mesh = new QuadMesh { Size = new Vector2(scaleStart * 32f, scaleStart * 32f), Material = material },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(900f, 4f, 900f),
            Direction = new Vector3(0f, -1f, 0f),
            Spread = 0f,
            InitialVelocityMin = Mathf.Abs(velocity),
            InitialVelocityMax = Mathf.Abs(velocity),
            Gravity = new Vector3(0f, -Mathf.Abs(acceleration), 0f),
            ScaleAmountMin = 1f,
            ScaleAmountMax = 1f,
            Position = new Vector3(0f, spawnHeight, 0f),
        };

        var root = new Node3D { Name = "WeatherRoot" };
        root.AddChild(rain);
        parent.AddChild(root);
        return new WeatherRenderer(root, rain, spawnHeight);
    }

    public void FrameSeconds(Vector3 focus)
    {
        if (_root == null || !GodotObject.IsInstanceValid(_root)) return;
        _root.Position = new Vector3(focus.X, 0f, focus.Z);
        _rain.Position = new Vector3(0f, _spawnHeight, 0f);
    }

    public void SetEnabled(bool enabled)
    {
        if (_rain != null && GodotObject.IsInstanceValid(_rain)) _rain.Emitting = enabled;
    }

    private static float Number(SlkTable table, int row, string column, float fallback)
    {
        string text = table.Get(row, column);
        if (string.IsNullOrWhiteSpace(text) || text == "-") return fallback;
        return float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}