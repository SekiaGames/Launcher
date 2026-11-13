using System;
using System.Numerics;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

internal sealed record WeatherDefinition(
    string Id,
    string TexturePath,
    int AlphaMode,
    bool UseFog,
    float Height,
    float EmissionRate,
    float LifeSpan,
    float Velocity,
    float Variance,
    int TextureRows,
    int TextureColumns,
    float MidTime,
    Vector3 ColorStart,
    Vector3 ColorMid,
    Vector3 ColorEnd,
    float AlphaStart,
    float AlphaMid,
    float AlphaEnd,
    float ScaleStart,
    float ScaleMid,
    float ScaleEnd,
    float UvStart,
    float UvMid,
    float UvEnd,
    float AngleX,
    float AngleY,
    int Head,
    int Tail,
    float TailLength,
    float Acceleration,
    float Latitude,
    float Longitude,
    string AmbientSound)
{
    public const float CellSize = 512f;
    public const float EmissionBudgetScale = 1.15f;
    public const float DegreesToRadians = MathF.PI / 180f;
    public const int MaxActiveParticlesPerCell = 4096;

    public int PoolCapacity => ParticleCount(EmissionBudgetScale);
    public int ActiveParticleCount => ParticleCount(1f);

    public Vector3 Direction
    {
        get
        {
            float ax = AngleX * DegreesToRadians;
            float ay = AngleY * DegreesToRadians;
            float cosX = MathF.Cos(ax);
            float sinX = MathF.Sin(ax);
            return new Vector3(cosX * MathF.Sin(ay), -sinX, cosX * MathF.Cos(ay));
        }
    }

    public string AmbientLabel =>
        string.IsNullOrWhiteSpace(AmbientSound) || AmbientSound.Trim() == "-"
            ? null : AmbientSound.Trim();

    int ParticleCount(float scale)
    {
        if (!float.IsFinite(EmissionRate) || !float.IsFinite(LifeSpan)
            || EmissionRate <= 0f || LifeSpan <= 0f) return 0;
        float count = EmissionRate * scale * LifeSpan;
        int maximum = (int)(MaxActiveParticlesPerCell * scale);
        return count >= maximum ? maximum : (int)count;
    }

    public static WeatherDefinition Load(WeatherType type)
    {
        if (type == WeatherType.None) return null;
        string id = WeatherTypeParser.Rawcode(type);
        SlkTable table = War3SlkReader.Get("TerrainArtWeather");
        int row = table?.FindRow("effectID", id) ?? -1;
        if (row < 0) return null;
        string directory = (table.Get(row, "texDir") ?? "").TrimEnd('\\', '/');
        string file = table.Get(row, "texFile") ?? "";
        string texture = (directory.Length > 0 ? directory + "\\" : "") + file + ".blp";
        bool head = table.GetInt(row, "head", 1) != 0;
        return new WeatherDefinition(
            id,
            texture,
            table.GetInt(row, "alphaMode", 0),
            table.GetInt(row, "useFog", 1) != 0,
            table.GetFloat(row, "height", 512f),
            table.GetFloat(row, "emrate", 0f),
            Math.Max(0.001f, table.GetFloat(row, "lifespan", 1f)),
            table.GetFloat(row, "veloc", 0f),
            Math.Max(0f, table.GetFloat(row, "var", 0f)),
            Math.Max(1, table.GetInt(row, "texr", 1)),
            Math.Max(1, table.GetInt(row, "texc", 1)),
            Math.Clamp(table.GetFloat(row, "midTime", 0.5f), 0.001f, 0.999f),
            Color(table, row, "Start"),
            Color(table, row, "Mid"),
            Color(table, row, "End"),
            Alpha(table, row, "Start"),
            Alpha(table, row, "Mid"),
            Alpha(table, row, "End"),
            table.GetFloat(row, "scaleStart", 1f),
            table.GetFloat(row, "scaleMid", 1f),
            table.GetFloat(row, "scaleEnd", 1f),
            table.GetFloat(row, head ? "hUVStart" : "tUVStart", 0f),
            table.GetFloat(row, head ? "hUVMid" : "tUVMid", 0f),
            table.GetFloat(row, head ? "hUVEnd" : "tUVEnd", 0f),
            table.GetFloat(row, "angx", 0f),
            table.GetFloat(row, "angy", 0f),
            table.GetInt(row, "head", 1),
            table.GetInt(row, "tail", 0),
            Math.Max(0f, table.GetFloat(row, "taillen", 0f)),
            table.GetFloat(row, "accel", 0f),
            table.GetFloat(row, "lati", 0f),
            table.GetFloat(row, "long", 0f),
            table.Get(row, "AmbientSound") ?? string.Empty);
    }

    static Vector3 Color(SlkTable table, int row, string phase) => new(
        table.GetFloat(row, "red" + phase, 255f) / 255f,
        table.GetFloat(row, "green" + phase, 255f) / 255f,
        table.GetFloat(row, "blue" + phase, 255f) / 255f);

    static float Alpha(SlkTable table, int row, string phase)
        => table.GetFloat(row, "alpha" + phase, 255f) / 255f;
}