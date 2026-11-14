using Godot;

namespace OpenWC3.Game.Generated.Lighting;

internal static class DNCDungeon
{
    internal static readonly DayNightLight Terrain = new()
    {
        Start = 333f,
        End = 60333f,
        Color = new Vector3(1f, 1f, 1f),
        Intensity = 1.3f,
        AmbientColor = new Vector3(1f, 1f, 1f),
        AmbientIntensity = 0.3f,
        Rotation = new DayNightTrack
        {
            Interpolation = 0,
            Frames = new float[] { 333f },
            Values = new float[] { 0.0546965f, 0.2626079f, 0.014912f, 0.9632357f },
            InTans = new float[] { 0f, 0f, 0f, 0f },
            OutTans = new float[] { 0f, 0f, 0f, 0f },
        },
        ColorTrack = null,
        IntensityTrack = null,
        AmbientColorTrack = null,
        AmbientIntensityTrack = null,
        VisibilityTrack = null,
    };

    internal static readonly DayNightLight Unit = new()
    {
        Start = 333f,
        End = 60333f,
        Color = new Vector3(1f, 0.258824f, 0.192157f),
        Intensity = 1.6f,
        AmbientColor = new Vector3(1f, 1f, 1f),
        AmbientIntensity = 0.4f,
        Rotation = new DayNightTrack
        {
            Interpolation = 0,
            Frames = new float[] { 333f },
            Values = new float[] { 0.9259449f, 0.171998f, 0.262739f, 0.209788f },
            InTans = new float[] { 0f, 0f, 0f, 0f },
            OutTans = new float[] { 0f, 0f, 0f, 0f },
        },
        ColorTrack = null,
        IntensityTrack = null,
        AmbientColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 0f, 333f, 60333f },
            Values = new float[] { 0.835294f, 0.823529f, 0.854902f, 1f, 1f, 1f, 1f, 1f, 1f },
            InTans = new float[] { 0f, 0f, 0f, 0.164706f, 0.176471f, 0.145098f, 0f, 0f, 0f },
            OutTans = new float[] { 0.164706f, 0.176471f, 0.145098f, 0f, 0f, 0f, 0f, 0f, 0f },
        },
        AmbientIntensityTrack = null,
        VisibilityTrack = null,
    };

}
