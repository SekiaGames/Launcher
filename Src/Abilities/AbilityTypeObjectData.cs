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
        if (_type == null || string.IsNullOrEmpty(field)) return fallback;
        int slot = DataSlot(field, dataPointer);
        if (slot >= 0) return Or(_type.DataTextAt(level, (char)('A' + slot)), fallback);
        switch (field.ToLowerInvariant())
        {
            case "name": return Or(_type.Name, fallback);
            case "tip": return Or(_type.Tip, fallback);
            case "ubertip": return Or(_type.Ubertip, fallback);
            case "hotkey": return Or(_type.Hotkey, fallback);
            case "art": return Or(_type.IconArt, fallback);
            case "buffid": return Or(BuffAt(level, 0), fallback);
            case "buffid1": return Or(BuffAt(level, 0), fallback);
            case "buffid2": return Or(BuffAt(level, 1), fallback);
            case "buffid3": return Or(BuffAt(level, 2), fallback);
            case "buffid4": return Or(BuffAt(level, 3), fallback);
            case "summon": return Or(_type.SummonAt(level), fallback);
            case "code": return Or(_type.Code, fallback);
            default: return fallback;
        }
    }

    public float GetFloat(string field, int level, int dataPointer, float fallback)
    {
        if (_type == null || string.IsNullOrEmpty(field)) return fallback;
        int slot = DataSlot(field, dataPointer);
        if (slot >= 0) return _type.DataAt(level, (char)('A' + slot));
        switch (field.ToLowerInvariant())
        {
            case "cool": return _type.CooldownAt(level);
            case "cost": return _type.CostAt(level);
            case "rng": return _type.RangeAt(level);
            case "area": return _type.AreaAt(level);
            case "dur": return _type.DurationAt(level);
            case "herodur": return _type.HeroDurationAt(level);
            case "cast": return _type.CastAt(level);
            case "levels": return _type.MaxLevel;
            case "levelskip": return _type.LevelSkip;
            case "reqlevel": return _type.RequiredLevel;
            default: return fallback;
        }
    }

    public int GetInt(string field, int level, int dataPointer, int fallback)
    {
        if (_type == null || string.IsNullOrEmpty(field)) return fallback;
        switch (field.ToLowerInvariant())
        {
            case "levels": return _type.MaxLevel;
            case "levelskip": return _type.LevelSkip;
            case "reqlevel": return _type.RequiredLevel;
            case "checkdep": return 0;
            default: break;
        }
        int slot = DataSlot(field, dataPointer);
        if (slot >= 0) return (int)_type.DataAt(level, (char)('A' + slot));
        return (int)GetFloat(field, level, dataPointer, fallback);
    }

    static int DataSlot(string field, int dataPointer)
    {
        if (field.Length == 5 && field.StartsWith("Data", StringComparison.OrdinalIgnoreCase))
        {
            int index = char.ToUpperInvariant(field[4]) - 'A';
            return index is >= 0 and < 9 ? index : -1;
        }
        return dataPointer is >= 1 and <= 9 ? dataPointer - 1 : -1;
    }

    string BuffAt(int level, int index)
    {
        if (index != 0 || _type.BuffId == null || _type.BuffId.Length == 0) return "";
        int row = Math.Clamp(level, 1, _type.BuffId.Length) - 1;
        return _type.BuffId[row];
    }

    static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
}
