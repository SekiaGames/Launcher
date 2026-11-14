using OpenWC3.Game;

namespace OpenWC3;

public sealed class AbilityTypeObjectData : IRuntimeObjectData
{
    readonly AbilityType _type;

    public AbilityTypeObjectData(AbilityType type) => _type = type;

    public string Id => _type?.Id ?? "";

    public string BaseId => string.IsNullOrEmpty(_type?.Code) ? Id : _type.Code;

    public string GetString(string field, string fallback = "") => GetString(field, 1, 0, fallback);

    public string GetString(string field, int level, int dataPointer = 0, string fallback = "")
    {
        AbilityTypeData data = _type?.ObjectData;
        if (data == null || string.IsNullOrEmpty(field)) return fallback;
        if (field.Equals("summon", StringComparison.OrdinalIgnoreCase))
            return Or(data.GetString("UnitID", level, dataPointer, ""), fallback);
        if (field.Length == 7 && field.StartsWith("buffid", StringComparison.OrdinalIgnoreCase)
            && field[6] is >= '1' and <= '4')
            return Or(data.GetString("BuffID" + field[6], level, dataPointer, ""), fallback);
        return Or(data.GetString(field, level, dataPointer, ""), fallback);
    }

    public float GetFloat(string field, int level, int dataPointer, float fallback)
        => _type?.ObjectData?.GetFloat(field, level, dataPointer, fallback) ?? fallback;

    public int GetInt(string field, int level, int dataPointer, int fallback)
        => _type?.ObjectData?.GetInt(field, level, dataPointer, fallback) ?? fallback;

    static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
}
