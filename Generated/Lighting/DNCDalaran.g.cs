using Godot;

namespace OpenWC3.Game.Generated.Lighting;

internal static class DNCDalaran
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
            Values = new float[] { 0.3815389f, -0.215864f, -0.4425869f, 0.7822708f },
            InTans = new float[] { 0f, 0f, 0f, 0f },
            OutTans = new float[] { 0f, 0f, 0f, 0f },
        },
        ColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 13667f, 15333f, 15367f, 43700f, 45367f, 49033f, 60333f },
            Values = new float[] { 0.235294f, 0.4f, 0.6f, 0.470588f, 0.705882f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0.743399f, 0.792157f, 0.942026f, 0.235294f, 0.4f, 0.6f },
            InTans = new float[] { 0f, 0f, 0f, 0.235294f, 0.305882f, 0.4f, 0.529412f, 0.294118f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.256601f, -0.207843f, -0.0579739f, -0.508105f, -0.392157f, -0.342026f },
            OutTans = new float[] { 0.235294f, 0.305882f, 0.4f, 0.529412f, 0.294118f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.256601f, -0.207843f, -0.0579739f, -0.508105f, -0.392157f, -0.342026f, 0f, 0f, 0f },
        },
        IntensityTrack = null,
        AmbientColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 13733f, 15733f, 43900f, 45633f, 47500f, 49767f, 60333f },
            Values = new float[] { 0.235294f, 0.4f, 0.6f, 0.350997f, 0.392157f, 0.901961f, 1f, 0.87782f, 0.830782f, 1f, 0.87782f, 0.830782f, 0.717125f, 0.392157f, 0.815085f, 0.447632f, 0.392157f, 0.830046f, 0.36626f, 0.392157f, 0.89074f, 0.235294f, 0.4f, 0.6f },
            InTans = new float[] { 0f, 0f, 0f, 0.115703f, -0.0078431f, 0.301961f, 0.649003f, 0.485663f, -0.071179f, 0f, 0f, 0f, -0.282875f, -0.485663f, -0.0156965f, -0.269493f, 0f, 0.0149611f, -0.0813713f, 0f, 0.0606936f, -0.130966f, 0.0078431f, -0.29074f },
            OutTans = new float[] { 0.115703f, -0.0078431f, 0.301961f, 0.649003f, 0.485663f, -0.071179f, 0f, 0f, 0f, -0.282875f, -0.485663f, -0.0156965f, -0.269493f, 0f, 0.0149611f, -0.0813713f, 0f, 0.0606936f, -0.130966f, 0.0078431f, -0.29074f, 0f, 0f, 0f },
        },
        AmbientIntensityTrack = null,
        VisibilityTrack = null,
    };

    internal static readonly DayNightLight Unit = new()
    {
        Start = 333f,
        End = 60333f,
        Color = new Vector3(1f, 1f, 1f),
        Intensity = 1.3f,
        AmbientColor = new Vector3(1f, 1f, 1f),
        AmbientIntensity = 0.7f,
        Rotation = new DayNightTrack
        {
            Interpolation = 0,
            Frames = new float[] { 333f },
            Values = new float[] { 0.3815389f, -0.215864f, -0.4425869f, 0.7822708f },
            InTans = new float[] { 0f, 0f, 0f, 0f },
            OutTans = new float[] { 0f, 0f, 0f, 0f },
        },
        ColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 13667f, 15333f, 15367f, 43700f, 45367f, 49033f, 60333f },
            Values = new float[] { 0.235294f, 0.4f, 0.6f, 0.470588f, 0.705882f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0.743399f, 0.792157f, 0.942026f, 0.235294f, 0.4f, 0.6f },
            InTans = new float[] { 0f, 0f, 0f, 0.235294f, 0.305882f, 0.4f, 0.529412f, 0.294118f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.256601f, -0.207843f, -0.0579739f, -0.508105f, -0.392157f, -0.342026f },
            OutTans = new float[] { 0.235294f, 0.305882f, 0.4f, 0.529412f, 0.294118f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.256601f, -0.207843f, -0.0579739f, -0.508105f, -0.392157f, -0.342026f, 0f, 0f, 0f },
        },
        IntensityTrack = null,
        AmbientColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 13733f, 15733f, 43900f, 45633f, 47500f, 49767f, 60333f },
            Values = new float[] { 0.235294f, 0.4f, 0.6f, 0.350997f, 0.392157f, 0.901961f, 1f, 0.87782f, 0.830782f, 1f, 0.87782f, 0.830782f, 0.717125f, 0.392157f, 0.815085f, 0.447632f, 0.392157f, 0.830046f, 0.36626f, 0.392157f, 0.89074f, 0.235294f, 0.4f, 0.6f },
            InTans = new float[] { 0f, 0f, 0f, 0.115703f, -0.0078431f, 0.301961f, 0.649003f, 0.485663f, -0.071179f, 0f, 0f, 0f, -0.282875f, -0.485663f, -0.0156965f, -0.269493f, 0f, 0.0149611f, -0.0813713f, 0f, 0.0606936f, -0.130966f, 0.0078431f, -0.29074f },
            OutTans = new float[] { 0.115703f, -0.0078431f, 0.301961f, 0.649003f, 0.485663f, -0.071179f, 0f, 0f, 0f, -0.282875f, -0.485663f, -0.0156965f, -0.269493f, 0f, 0.0149611f, -0.0813713f, 0f, 0.0606936f, -0.130966f, 0.0078431f, -0.29074f, 0f, 0f, 0f },
        },
        AmbientIntensityTrack = null,
        VisibilityTrack = null,
    };

}
