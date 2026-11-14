using System;

namespace OpenWC3.Game;

public static class War3Volume
{
    public const float DefaultMusicVolume = 0.6f;

    private static readonly byte[] SoftwareTable =
    {
        0,0,0,1,1,1,2,2,2,3,3,4,4,4,5,5,
        6,6,6,7,7,8,8,8,9,9,10,10,11,11,11,12,
        12,13,13,14,14,15,15,16,16,17,17,18,18,19,
        19,20,20,21,22,22,23,23,24,24,25,26,26,27,27,28,
        29,29,30,31,31,32,33,33,34,35,36,36,37,38,39,40,
        40,41,42,43,44,45,46,47,47,48,49,50,51,52,54,55,
        56,57,58,59,60,62,63,64,66,67,69,70,72,73,75,77,
        78,80,82,84,86,88,91,93,95,98,101,104,107,110,114,118,122,127
    };

    public static float ToLinearGain(float volume)
    {
        if (!float.IsFinite(volume)) return 0f;
        return SoftwareTable[(int)(Math.Clamp(volume, 0f, 1f) * 127f)] / 127f;
    }

    public static float ToDecibels(float volume) =>
        (float)(20.0 * Math.Log10(Math.Max(0.0001f, ToLinearGain(volume))));
}