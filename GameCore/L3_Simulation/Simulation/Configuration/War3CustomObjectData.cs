using System.Text;
using OpenWC3.War3Formats.Text;

namespace OpenWC3.Game;

public enum War3ObjectValueType
{
    Integer = 0,
    Real = 1,
    Unreal = 2,
    String = 3,
}

public readonly record struct War3ObjectFieldKey(string FieldId, int Level = 0, int DataPointer = 0);

public sealed record War3ObjectModification(War3ObjectFieldKey Key, War3ObjectValueType Type, object Value)
{
    public string AsString() => Value switch
    {
        float number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture) ?? "",
    };
}

public sealed class War3CustomObject
{
    public string BaseId { get; init; } = "";
    public string Id { get; init; } = "";
    public List<War3ObjectModification> Modifications { get; } = new();
}

public sealed class War3CustomObjectTable
{
    public int Version { get; init; }
    public bool Extended { get; init; }
    public List<War3CustomObject> OriginalObjects { get; } = new();
    public List<War3CustomObject> CustomObjects { get; } = new();

    public IEnumerable<War3CustomObject> Objects => OriginalObjects.Concat(CustomObjects);
}

// Warcraft III object modification format used by war3map.w3u/w3a/w3h.
// Ability data is the extended form and carries level + data pointer per modification.
public static class War3CustomObjectData
{
    const int MaxObjects = 1_000_000;
    const int MaxModifications = 1_000_000;

    public static War3CustomObjectTable Parse(byte[] data, bool extended)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (stream.Length < 12) throw new InvalidDataException("Object data is truncated.");

        int version=reader.ReadInt32();
        bool hasLevelFields=extended;
        var table = new War3CustomObjectTable
            { Version = version, Extended = hasLevelFields };
        ReadObjectSet(reader, table.OriginalObjects, hasLevelFields);
        // Some protected maps omit the final empty custom-object set. Only
        // accept EOF at this complete-set boundary; truncated records and
        // partial counts must still fail.
        if (stream.Position != stream.Length)
            ReadObjectSet(reader, table.CustomObjects, hasLevelFields);
        return table;
    }

    static void ReadObjectSet(BinaryReader reader, List<War3CustomObject> target, bool extended)
    {
        int count = ReadCount(reader, MaxObjects, "object");
        for (int i = 0; i < count; i++)
        {
            string baseId = ReadRawcode(reader);
            string newId = ReadRawcode(reader);
            var entry = new War3CustomObject
            {
                BaseId = baseId,
                Id = IsZeroRawcode(newId) ? baseId : newId,
            };
            int modificationCount = ReadCount(reader, MaxModifications, "modification");
            for (int j = 0; j < modificationCount; j++)
            {
                string fieldId = ReadRawcode(reader);
                int rawType = reader.ReadInt32();
                if (rawType is < 0 or > 3)
                    throw new InvalidDataException($"Unsupported object value type {rawType}.");
                int level = extended ? reader.ReadInt32() : 0;
                int dataPointer = extended ? reader.ReadInt32() : 0;
                object value = (War3ObjectValueType)rawType switch
                {
                    War3ObjectValueType.Integer => reader.ReadInt32(),
                    War3ObjectValueType.Real or War3ObjectValueType.Unreal => reader.ReadSingle(),
                    War3ObjectValueType.String => ReadNullString(reader),
                    _ => throw new InvalidDataException(),
                };
                ReadRawcode(reader); // end token; protected maps do not always preserve a useful value here
                entry.Modifications.Add(new(
                    new War3ObjectFieldKey(fieldId, level, dataPointer),
                    (War3ObjectValueType)rawType, value));
            }
            target.Add(entry);
        }
    }

    static int ReadCount(BinaryReader reader, int maximum, string label)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > maximum)
            throw new InvalidDataException($"Invalid {label} count {count}.");
        return count;
    }

    static string ReadRawcode(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length != 4) throw new EndOfStreamException();
        return Encoding.Latin1.GetString(bytes);
    }

    static bool IsZeroRawcode(string value) => value.All(character => character == '\0');

    static string ReadNullString(BinaryReader reader)
        => War3TextEncoding.ReadNullTerminated(reader);
}

public class War3TypeData : OpenWC3.IRuntimeObjectData
{
    public string Id { get; internal set; } = "";
    public string BaseId { get; internal set; } = "";
    public readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<War3ObjectFieldKey, string> LevelFields = new(new FieldKeyComparer());

    sealed class FieldKeyComparer : IEqualityComparer<War3ObjectFieldKey>
    {
        public bool Equals(War3ObjectFieldKey x, War3ObjectFieldKey y) =>
            x.Level == y.Level && x.DataPointer == y.DataPointer
            && StringComparer.OrdinalIgnoreCase.Equals(x.FieldId, y.FieldId);
        public int GetHashCode(War3ObjectFieldKey key) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(key.FieldId), key.Level, key.DataPointer);
    }

    // Normalize while merging, before a higher layer can lose to an older alias.
    static bool TryColumn(string column, out War3ObjectFieldKey key)
    {
        key = default;
        if (column.StartsWith("Data", StringComparison.OrdinalIgnoreCase) && column.Length > 5)
        {
            if (char.ToUpperInvariant(column[4]) is >= 'A' and <= 'I'
                && int.TryParse(column.AsSpan(5), out int level))
            { key = new("Data", level, char.ToUpperInvariant(column[4]) - 'A' + 1); return true; }
            if (column[^1] is >= '1' and <= '9'
                && int.TryParse(column.AsSpan(4, column.Length - 5), out level))
            { key = new("Data", level, column[^1] - '0'); return true; }
        }
        foreach (string field in LevelColumns)
            if (column.StartsWith(field, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(column.AsSpan(field.Length), out int level) && level > 0)
            { key = new(field, level); return true; }
        return false;
    }

    static readonly string[] LevelColumns =
        { "Cool", "Cost", "Cast", "Dur", "HeroDur", "Area", "Rng", "targs", "Targets", "BuffID", "EfctID", "UnitID" };

    internal void SetField(string field, string value)
    {
        if (this is AbilityTypeData && TryColumn(field, out var key))
            SetLevelField(key, value);
        else
        {
            Fields[field] = value;
            // A map profile must also replace the campaign's level-zero slot.
            LevelFields.Remove(new(field));
        }
    }

    internal void SetLevelField(War3ObjectFieldKey key, string value)
    {
        LevelFields[key] = value;
        if (key.Level == 0) Fields[key.FieldId] = value;
        else if (this is AbilityTypeData)
        {
            if (key.FieldId.Equals("Data", StringComparison.OrdinalIgnoreCase) && key.DataPointer is >= 1 and <= 9)
            {
                Fields[$"Data{(char)('A' + key.DataPointer - 1)}{key.Level}"] = value;
                Fields[$"Data{key.Level}{key.DataPointer}"] = value;
            }
            else if (LevelColumns.Contains(key.FieldId, StringComparer.OrdinalIgnoreCase))
                Fields[$"{key.FieldId}{key.Level}"] = value;
        }
    }

    // 级别复合键("Dur1"/"DataA2")每帧大量重建; 按参数对缓存键字符串, 查表零分配
    static readonly Dictionary<(string Field, int Level), string> CompositeFieldKeys = new();
    static readonly Dictionary<(int Pointer, int Level), string> DataAKeys = new();
    static readonly Dictionary<(int Level, int Pointer), string> DataLevelKeys = new();
    static string CompositeFieldKey(string field, int level)
    {
        var key = (field, level);
        if (!CompositeFieldKeys.TryGetValue(key, out string cached))
            CompositeFieldKeys[key] = cached = $"{field}{level}";
        return cached;
    }

    public string GetString(string field, string fallback = "") =>
        Fields.TryGetValue(field, out string value) ? value : fallback;

    public string GetString(string field, int level, int dataPointer = 0, string fallback = "")
    {
        if (field.Length == 5 && field.StartsWith("Data", StringComparison.OrdinalIgnoreCase)
            && char.ToUpperInvariant(field[4]) is >= 'A' and <= 'I')
        { dataPointer = char.ToUpperInvariant(field[4]) - 'A' + 1; field = "Data"; }
        if (LevelFields.TryGetValue(new(field, level, dataPointer), out string value)) return value;
        if (level != 0 && LevelFields.TryGetValue(new(field, 0, dataPointer), out value)) return value;
        if (level > 0 && this is AbilityTypeData)
        {
            if (dataPointer is >= 1 and <= 9 && field.Equals("Data", StringComparison.OrdinalIgnoreCase))
            {
                var dataAKey = (dataPointer, level);
                if (!DataAKeys.TryGetValue(dataAKey, out string dataA))
                    DataAKeys[dataAKey] = dataA = $"Data{(char)('A' + dataPointer - 1)}{level}";
                if (Fields.TryGetValue(dataA, out value)) return value;
                var dataLevelKey = (level, dataPointer);
                if (!DataLevelKeys.TryGetValue(dataLevelKey, out string dataLevel))
                    DataLevelKeys[dataLevelKey] = dataLevel = $"Data{level}{dataPointer}";
                if (Fields.TryGetValue(dataLevel, out value)) return value;
            }
            else if (Fields.TryGetValue(CompositeFieldKey(field, level), out value)) return value;
            // Game.dll 6998d0 normalizes the SLK fallback suffix to min(n,4).
            // Exact map overrides above still win, including explicit empty/zero
            // cells. Flattened maps can declare ten levels with only DataA1..4.
            if (level > 4 && (dataPointer is >= 1 and <= 9
                && field.Equals("Data", StringComparison.OrdinalIgnoreCase)
                || LevelColumns.Contains(field, StringComparer.OrdinalIgnoreCase)))
                return GetString(field, 4, dataPointer, fallback);
        }
        return GetString(field, fallback);
    }

    public float GetFloat(string field, int level, int dataPointer, float fallback) =>
        OpenWC3.RuntimeObjectNumbers.ReadFloat(GetString(field, level, dataPointer, null), fallback);

    public int GetInt(string field, int level, int dataPointer, int fallback) =>
        OpenWC3.RuntimeObjectNumbers.ReadInt(GetString(field, level, dataPointer, null), fallback);
}

public sealed class AbilityTypeData : War3TypeData;
public sealed class BuffTypeData : War3TypeData;
public sealed class ItemTypeData : War3TypeData;
public sealed class DestructableTypeData : War3TypeData;
public sealed class UpgradeTypeData : War3TypeData
{
    internal static bool IsLevelText(string field) => field is
        "Name" or "Tip" or "Ubertip" or "Art" or "Hotkey";

    // W3Q overrides are one-based, while native TXT profiles store quoted lists.
    public string GetLevelText(string field, int level, string fallback = "")
    {
        level = Math.Max(1, level);
        if (LevelFields.TryGetValue(new(field, level), out string value)) return value;
        if (!Fields.TryGetValue(field, out value)) return fallback;
        var values = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        foreach (char character in value)
        {
            if (character == '"') { quoted = !quoted; continue; }
            if (character == ',' && !quoted)
            { values.Add(current.ToString()); current.Clear(); continue; }
            current.Append(character);
        }
        values.Add(current.ToString());
        return values[Math.Clamp(level - 1, 0, values.Count - 1)];
    }
}
