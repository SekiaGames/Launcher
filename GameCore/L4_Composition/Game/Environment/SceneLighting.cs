using Godot;
using OpenWC3.Game.Generated;
using System;

namespace OpenWC3.Game;

public readonly record struct SceneLight(
    Vector3 Direction,
    Vector3 DiffuseColor,
    float DiffuseIntensity,
    Vector3 AmbientColor,
    float AmbientIntensity)
{
    public static readonly SceneLight Fallback = new(
        new Vector3(-0.3f, 0.3f, 0.25f).Normalized(),
        Vector3.One, 1f, Vector3.One, 0.3f);
}

public sealed class SceneLighting
{
    public SceneLight Terrain { get; private set; }
    public SceneLight Units { get; private set; }

    DayNightLight _terrainSource;
    DayNightLight _unitSource;

    SceneLighting(DayNightLight terrainSource, DayNightLight unitSource)
    {
        _terrainSource = terrainSource;
        _unitSource = unitSource;
        Terrain = Evaluate(terrainSource, 12f) ?? SceneLight.Fallback;
        Units = Evaluate(unitSource, 12f) ?? SceneLight.Fallback;
    }

    public void Update(float gameHour)
    {
        Terrain = Evaluate(_terrainSource, gameHour) ?? SceneLight.Fallback;
        Units = Evaluate(_unitSource, gameHour) ?? SceneLight.Fallback;
    }

    public static SceneLighting Load(char tileset, float gameHour)
    {
        return Load(FamilyFor(tileset), gameHour);
    }

    public static SceneLighting Load(string family, float gameHour)
    {
        var lighting = new SceneLighting(SourceFor(family, false), SourceFor(family, true));
        lighting.Update(gameHour);
        return lighting;
    }

    public static char ResolveTileset(char lightEnvTileset, char terrainTileset) =>
        lightEnvTileset != 0 ? lightEnvTileset : terrainTileset;

    static string FamilyFor(char tileset) => tileset switch
    {
        'A' => "DNCAshenVale",
        'B' => "DNCBarrens",
        'C' => "DNCFelWood",
        'D' => "DNCDungeon",
        'G' => "DNCUnderground",
        'X' => "DNCDalaran",
        'I' => "DNCIcecrown",
        'N' => "DNCNorthrend",
        'V' => "DNCVillage",
        'Y' => "DNCCityscape",
        _ => "DNCLordaeron"
    };

    static DayNightLight SourceFor(string family, bool unit)
    {
        DayNightLight light = Lookup(family, unit);
        return light ?? Lookup("DNCLordaeron", unit);
    }

    static DayNightLight Lookup(string family, bool unit)
    {
        if (!DayNight.Environments.TryGetValue(family, out (DayNightLight Terrain, DayNightLight Unit) entry))
        {
            return null;
        }
        return unit ? entry.Unit : entry.Terrain;
    }

    public static string Describe(SceneLight light) =>
        $"dir=({light.Direction.X:0.00},{light.Direction.Y:0.00},{light.Direction.Z:0.00}) "
        + $"diff=({light.DiffuseColor.X:0.00},{light.DiffuseColor.Y:0.00},{light.DiffuseColor.Z:0.00})*{light.DiffuseIntensity:0.00} "
        + $"amb=({light.AmbientColor.X:0.00},{light.AmbientColor.Y:0.00},{light.AmbientColor.Z:0.00})*{light.AmbientIntensity:0.00}";

    static SceneLight? Evaluate(DayNightLight light, float gameHour)
    {
        if (light == null)
        {
            return null;
        }

        float start = light.Start;
        float end = light.End;
        float frame = start + (end - start) * (((gameHour % 24f + 24f) % 24f) / 24f);

        Quaternion rotation = EvaluateQuaternion(light.Rotation, frame, start, end);
        Vector3 axis = new Basis(rotation) * Vector3.Back;
        Vector3 direction = new(-axis.X, axis.Z, axis.Y);
        direction = direction.LengthSquared() <= 0.000001f ? Vector3.Up : direction.Normalized();
        Vector3 diffuse = EvaluateVector(light.ColorTrack, frame, start, end, light.Color);
        Vector3 ambient = EvaluateVector(light.AmbientColorTrack, frame, start, end, light.AmbientColor);
        float intensity = EvaluateScalar(light.IntensityTrack, frame, start, end, light.Intensity);
        float ambientIntensity = EvaluateScalar(light.AmbientIntensityTrack, frame, start, end, light.AmbientIntensity);
        float visibility = Mathf.Clamp(EvaluateScalar(light.VisibilityTrack, frame, start, end, 1f), 0f, 1f);
        return new SceneLight(direction, diffuse, intensity * visibility, ambient, ambientIntensity * visibility);
    }

    static float EvaluateScalar(DayNightTrack track, float frame, float start, float end, float fallback)
    {
        if (track == null || track.Width < 1)
        {
            return fallback;
        }

        (int a, int b, float t) = FindKeys(track, frame, start, end);
        float av = track.Values[a * track.Width];
        if (a == b || track.Interpolation == 0)
        {
            return av;
        }

        float bv = track.Values[b * track.Width];
        return track.Interpolation switch
        {
            2 => Hermite(av, track.OutTans[a], bv, track.InTans[b], t),
            3 => Bezier(av, track.OutTans[a], track.InTans[b], bv, t),
            _ => Mathf.Lerp(av, bv, t)
        };
    }

    static Vector3 EvaluateVector(DayNightTrack track, float frame, float start, float end, Vector3 fallback)
    {
        if (track == null || track.Width < 3)
        {
            return fallback;
        }

        (int a, int b, float t) = FindKeys(track, frame, start, end);
        Vector3 av = ReadVector(track.Values, a, track.Width);
        if (a == b || track.Interpolation == 0)
        {
            return av;
        }

        Vector3 bv = ReadVector(track.Values, b, track.Width);
        return track.Interpolation switch
        {
            2 => Hermite(av, ReadVector(track.OutTans, a, track.Width), bv, ReadVector(track.InTans, b, track.Width), t),
            3 => Bezier(av, ReadVector(track.OutTans, a, track.Width), ReadVector(track.InTans, b, track.Width), bv, t),
            _ => av.Lerp(bv, t)
        };
    }

    static Quaternion EvaluateQuaternion(DayNightTrack track, float frame, float start, float end)
    {
        if (track == null || track.Width < 4)
        {
            return Quaternion.Identity;
        }

        (int a, int b, float t) = FindKeys(track, frame, start, end);
        Quaternion av = ReadQuaternion(track, a);
        if (a == b || track.Interpolation == 0)
        {
            return av;
        }

        return av.Slerp(ReadQuaternion(track, b), t).Normalized();
    }

    static (int a, int b, float t) FindKeys(DayNightTrack track, float frame, float start, float end)
    {
        int first = -1;
        int last = -1;
        for (int i = 0; i < track.Frames.Length; i++)
        {
            if (track.Frames[i] < start || track.Frames[i] > end)
            {
                continue;
            }
            if (first < 0)
            {
                first = i;
            }
            last = i;
        }

        if (first < 0)
        {
            return (0, 0, 0f);
        }
        if (frame <= track.Frames[first])
        {
            return (first, first, 0f);
        }
        for (int i = first; i < last; i++)
        {
            if (frame > track.Frames[i + 1])
            {
                continue;
            }
            float span = track.Frames[i + 1] - track.Frames[i];
            return (i, i + 1, span <= 0f ? 0f : (frame - track.Frames[i]) / span);
        }
        return (last, last, 0f);
    }

    static Vector3 ReadVector(float[] values, int index, int width)
    {
        if (values == null || values.Length < (index + 1) * width || width < 3)
        {
            return Vector3.Zero;
        }
        int offset = index * width;
        return new Vector3(values[offset], values[offset + 1], values[offset + 2]);
    }

    static Quaternion ReadQuaternion(DayNightTrack track, int index)
    {
        float[] values = track.Values;
        if (values == null || values.Length < (index + 1) * track.Width || track.Width < 4)
        {
            return Quaternion.Identity;
        }
        int offset = index * track.Width;
        return new Quaternion(values[offset], values[offset + 1], values[offset + 2], values[offset + 3]).Normalized();
    }

    static float Hermite(float a, float outTan, float b, float inTan, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * a + (t3 - 2f * t2 + t) * outTan
             + (-2f * t3 + 3f * t2) * b + (t3 - t2) * inTan;
    }

    static Vector3 Hermite(Vector3 a, Vector3 outTan, Vector3 b, Vector3 inTan, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * a + (t3 - 2f * t2 + t) * outTan
             + (-2f * t3 + 3f * t2) * b + (t3 - t2) * inTan;
    }

    static float Bezier(float a, float outTan, float inTan, float b, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * outTan + 3f * u * t * t * inTan + t * t * t * b;
    }

    static Vector3 Bezier(Vector3 a, Vector3 outTan, Vector3 inTan, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * outTan + 3f * u * t * t * inTan + t * t * t * b;
    }
}

public static class SceneLightRig
{
    public static void Apply(Node root, SceneLight terrain, SceneLight units)
    {
        if (root == null)
        {
            return;
        }

        ForEachMaterial(root, material => Write(material, IsModelMaterial(material) ? units : terrain));
    }

    public static void ForEachMaterial(Node root, Action<ShaderMaterial> action)
    {
        if (root == null)
        {
            return;
        }

        foreach (Node child in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            ForEachMaterial((MeshInstance3D)child, action);
        }
    }

    public static void ForEachMaterial(MeshInstance3D mesh, Action<ShaderMaterial> action)
    {
        if (mesh.MaterialOverride is ShaderMaterial overrideMaterial)
        {
            action(overrideMaterial);
            return;
        }

        for (int surface = 0; surface < mesh.GetSurfaceOverrideMaterialCount(); surface++)
        {
            if (mesh.GetSurfaceOverrideMaterial(surface) is ShaderMaterial material)
            {
                action(material);
            }
        }
    }

    public static void Write(ShaderMaterial material, SceneLight light)
    {
        material.SetShaderParameter("war3_scene_direction", light.Direction);
        material.SetShaderParameter("war3_scene_color", light.DiffuseColor * light.DiffuseIntensity);
        material.SetShaderParameter("war3_scene_ambient", light.AmbientColor * light.AmbientIntensity);
    }

    static bool IsModelMaterial(ShaderMaterial material) =>
        material.Shader != null && material.Shader.ResourcePath.Contains("war3_model_");
}
