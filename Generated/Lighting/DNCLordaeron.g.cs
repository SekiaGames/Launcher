using Godot;

namespace OpenWC3.Game.Generated.Lighting;

internal static class DNCLordaeron
{
    internal static readonly DayNightLight Terrain = new()
    {
        Start = 333f,
        End = 60333f,
        Color = new Vector3(1f, 1f, 1f),
        Intensity = 1.3f,
        AmbientColor = new Vector3(1f, 1f, 1f),
        AmbientIntensity = 0.2f,
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
            Values = new float[] { 0.313408f, 0.532794f, 0.799191f, 0.626817f, 0.755867f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.671882f, 0.848251f, 0.996078f, 0.313408f, 0.532794f, 0.799191f },
            InTans = new float[] { 0f, 0f, 0f, 0.313408f, 0.223073f, 0.196887f, 0.369262f, 0.240211f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.324197f, -0.147828f, 0f, -0.358473f, -0.315457f, -0.196887f },
            OutTans = new float[] { 0.313408f, 0.223073f, 0.196887f, 0.369262f, 0.240211f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.324197f, -0.147828f, 0f, -0.358473f, -0.315457f, -0.196887f, 0f, 0f, 0f },
        },
        IntensityTrack = null,
        AmbientColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 13733f, 15333f, 15367f, 43700f, 45433f, 46733f, 48333f, 60333f },
            Values = new float[] { 0.313408f, 0.532794f, 0.799191f, 0.467523f, 0.522347f, 0.96583f, 0.839852f, 0.839852f, 0.980392f, 0.839852f, 0.839852f, 0.980392f, 0.839852f, 0.839852f, 0.980392f, 0.636354f, 0.522347f, 0.902841f, 0.369559f, 0.522347f, 0.902841f, 0.261174f, 0.522347f, 0.96583f, 0.313408f, 0.532794f, 0.799191f },
            InTans = new float[] { 0f, 0f, 0f, 0.154114f, -0.010447f, 0.166639f, 0.37233f, 0.317505f, 0.0145619f, 0f, 0f, 0f, 0f, 0f, 0f, -0.203499f, -0.317505f, -0.0775508f, -0.266795f, 0f, 0f, -0.108385f, 0f, 0.0629889f, 0.0522348f, 0.010447f, -0.166639f },
            OutTans = new float[] { 0.154114f, -0.010447f, 0.166639f, 0.37233f, 0.317505f, 0.0145619f, 0f, 0f, 0f, 0f, 0f, 0f, -0.203499f, -0.317505f, -0.0775508f, -0.266795f, 0f, 0f, -0.108385f, 0f, 0.0629889f, 0.0522348f, 0.010447f, -0.166639f, 0f, 0f, 0f },
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
            Values = new float[] { 0.313408f, 0.532794f, 0.799191f, 0.626817f, 0.755867f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.996078f, 0.671882f, 0.848251f, 0.996078f, 0.313408f, 0.532794f, 0.799191f },
            InTans = new float[] { 0f, 0f, 0f, 0.313408f, 0.223073f, 0.196887f, 0.369262f, 0.240211f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.324197f, -0.147828f, 0f, -0.358473f, -0.315457f, -0.196887f },
            OutTans = new float[] { 0.313408f, 0.223073f, 0.196887f, 0.369262f, 0.240211f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -0.324197f, -0.147828f, 0f, -0.358473f, -0.315457f, -0.196887f, 0f, 0f, 0f },
        },
        IntensityTrack = null,
        AmbientColorTrack = new DayNightTrack
        {
            Interpolation = 2,
            Frames = new float[] { 333f, 13733f, 15333f, 15367f, 43700f, 45433f, 46733f, 48333f, 60333f },
            Values = new float[] { 0.313408f, 0.532794f, 0.799191f, 0.467523f, 0.522347f, 0.96583f, 0.839852f, 0.839852f, 0.980392f, 0.839852f, 0.839852f, 0.980392f, 0.839852f, 0.839852f, 0.980392f, 0.636354f, 0.522347f, 0.902841f, 0.369559f, 0.522347f, 0.902841f, 0.261174f, 0.522347f, 0.96583f, 0.313408f, 0.532794f, 0.799191f },
            InTans = new float[] { 0f, 0f, 0f, 0.154114f, -0.010447f, 0.166639f, 0.37233f, 0.317505f, 0.0145619f, 0f, 0f, 0f, 0f, 0f, 0f, -0.203499f, -0.317505f, -0.0775508f, -0.266795f, 0f, 0f, -0.108385f, 0f, 0.0629889f, 0.0522348f, 0.010447f, -0.166639f },
            OutTans = new float[] { 0.154114f, -0.010447f, 0.166639f, 0.37233f, 0.317505f, 0.0145619f, 0f, 0f, 0f, 0f, 0f, 0f, -0.203499f, -0.317505f, -0.0775508f, -0.266795f, 0f, 0f, -0.108385f, 0f, 0.0629889f, 0.0522348f, 0.010447f, -0.166639f, 0f, 0f, 0f },
        },
        AmbientIntensityTrack = null,
        VisibilityTrack = null,
    };

}
