namespace OpenWC3.Game;

public enum WeatherType
{
    None = 0,
    RainLight = 0x726C4C52,
    RainHeavy = 0x72684C52,
    SnowLight = 0x736C4E53,
    SnowHeavy = 0x73684E53,
    SnowBlizzard = 0x73624E53,
    WindLight = 0x776C4F57,
    WindHeavy = 0x77634E57,
    MistLight = 0x6C774446,
    MistHeavy = 0x68774446,
    LRaa = 0x6161524C,
    LRma = 0x616D524C,
    RAlr = 0x726C4152,
    RAhr = 0x72684152,
    Wocw = 0x77634F57,
    FDbh = 0x68624446,
    FDbl = 0x6C624446,
    FDgh = 0x68674446,
    FDgl = 0x6C674446,
    FDrh = 0x68724446,
    FDrl = 0x6C724446,
    Meds = 0x7364454D,
}

public static class WeatherTypeParser
{
    public static WeatherType FromW3iId(int w3iId)
    {
        if (w3iId == 0) return WeatherType.None;
        return Enum.IsDefined(typeof(WeatherType), w3iId)
            ? (WeatherType)w3iId : WeatherType.None;
    }

    public static WeatherType FromJassId(int jassId) =>
        FromW3iId(jassId == 0 ? 0
            : System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(jassId));

    public static WeatherType FromRawcode(string rawcode)
    {
        if (string.IsNullOrEmpty(rawcode) || rawcode.Length < 4) return WeatherType.None;
        int value = rawcode[0] | rawcode[1] << 8 | rawcode[2] << 16 | rawcode[3] << 24;
        return FromW3iId(value);
    }

    public static string Rawcode(WeatherType type)
    {
        int value = (int)type;
        return value == 0 ? string.Empty : new string(new[]
        {
            (char)(value & 0xFF),
            (char)((value >> 8) & 0xFF),
            (char)((value >> 16) & 0xFF),
            (char)((value >> 24) & 0xFF),
        });
    }

    public static string RawcodeOfId(int id)
    {
        if (id == 0) return string.Empty;
        var chars = new char[4];
        for (int i = 0; i < 4; i++)
        {
            int b = (id >> (i * 8)) & 0xFF;
            chars[i] = b >= 0x20 && b < 0x7F ? (char)b : '?';
        }
        return new string(chars);
    }

    public static string RawcodeOfJassId(int jassId) =>
        jassId == 0 ? string.Empty
            : RawcodeOfId(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(jassId));
}