using Godot;

namespace OpenWC3.Game.Generated.Lighting;

internal static class DNCAshenVale
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
            Frames = new float[] { 333f, 13667f, 15333f, 15367f, 43700f, 45367f, 48567f, 60333f },
            Values = new float[] { 0.235294f, 0.4f, 0.6f, 0.470588f, 0.705882f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.627451f, 0.792157f, 0.996078f, 0.235294f, 0.4f, 0.6f },
            InTans = new float[] { 0f, 0f, 0f, 0.235294f, 0.305882f, 0.396078f, 0.52549f, 0.290196f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.368627f, -0.203922f, 0f, -0.392157f, -0.392157f, -0.396078f },
            OutTans = new float[] { 0.235294f, 0.305882f, 0.396078f, 0.52549f, 0.290196f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.368627f, -0.203922f, 0f, -0.392157f, -0.392157f, -0.396078f, 0f, 0f, 0f },
        },
        IntensityTrack = null,
        AmbientColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 14200f, 15133f, 15533f, 44200f, 46633f, 47933f, 49533f, 60333f },
            Values = new float[] { 0.235294f, 0.509804f, 0.509804f, 0.350997f, 0.731347f, 0.901961f, 0.814924f, 0.784314f, 0.941177f, 0.765979f, 1f, 0.941177f, 0.75834f, 1f, 0.941177f, 0.477748f, 0.748154f, 0.843137f, 0.27745f, 0.748154f, 0.843137f, 0.196078f, 0.748154f, 0.901961f, 0.235294f, 0.509804f, 0.509804f },
            InTans = new float[] { 0f, 0f, 0f, 0.115703f, 0.221543f, 0.392157f, 0.463927f, 0.0529666f, 0.0392157f, -0.0489445f, 0.215686f, 0f, -0.0076394f, 0f, 0f, -0.280592f, -0.251846f, -0.0980392f, -0.200299f, 0f, 0f, -0.0813713f, 0f, 0.0588235f, 0.0392157f, -0.23835f, -0.392157f },
            OutTans = new float[] { 0.115703f, 0.221543f, 0.392157f, 0.463927f, 0.0529666f, 0.0392157f, -0.0489445f, 0.215686f, 0f, -0.0076394f, 0f, 0f, -0.280592f, -0.251846f, -0.0980392f, -0.200299f, 0f, 0f, -0.0813713f, 0f, 0.0588235f, 0.0392157f, -0.23835f, -0.392157f, 0f, 0f, 0f },
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
        AmbientIntensity = 0.5f,
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
            Frames = new float[] { 333f, 13667f, 15333f, 15367f, 43700f, 45367f, 48567f, 60333f },
            Values = new float[] { 0.235294f, 0.4f, 0.6f, 0.470588f, 0.705882f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.627451f, 0.792157f, 0.996078f, 0.235294f, 0.4f, 0.6f },
            InTans = new float[] { 0f, 0f, 0f, 0.235294f, 0.305882f, 0.396078f, 0.52549f, 0.290196f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.368627f, -0.203922f, 0f, -0.392157f, -0.392157f, -0.396078f },
            OutTans = new float[] { 0.235294f, 0.305882f, 0.396078f, 0.52549f, 0.290196f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.368627f, -0.203922f, 0f, -0.392157f, -0.392157f, -0.396078f, 0f, 0f, 0f },
        },
        IntensityTrack = null,
        AmbientColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 14200f, 15133f, 15533f, 44200f, 46633f, 47933f, 49533f, 60333f },
            Values = new float[] { 0.235294f, 0.509804f, 0.509804f, 0.350997f, 0.731347f, 0.901961f, 0.814924f, 0.784314f, 0.941177f, 0.765979f, 1f, 0.941177f, 0.75834f, 1f, 0.941177f, 0.477748f, 0.748154f, 0.843137f, 0.27745f, 0.748154f, 0.843137f, 0.196078f, 0.748154f, 0.901961f, 0.235294f, 0.509804f, 0.509804f },
            InTans = new float[] { 0f, 0f, 0f, 0.115703f, 0.221543f, 0.392157f, 0.463927f, 0.0529666f, 0.0392157f, -0.0489445f, 0.215686f, 0f, -0.0076394f, 0f, 0f, -0.280592f, -0.251846f, -0.0980392f, -0.200299f, 0f, 0f, -0.0813713f, 0f, 0.0588235f, 0.0392157f, -0.23835f, -0.392157f },
            OutTans = new float[] { 0.115703f, 0.221543f, 0.392157f, 0.463927f, 0.0529666f, 0.0392157f, -0.0489445f, 0.215686f, 0f, -0.0076394f, 0f, 0f, -0.280592f, -0.251846f, -0.0980392f, -0.200299f, 0f, 0f, -0.0813713f, 0f, 0.0588235f, 0.0392157f, -0.23835f, -0.392157f, 0f, 0f, 0f },
        },
        AmbientIntensityTrack = null,
        VisibilityTrack = null,
    };

}
