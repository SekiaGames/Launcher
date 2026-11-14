using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class W3xObjectModification
{
    public string FieldId = "";
    public int Type;
    public int Level;
    public int DataPointer;
    public byte[] ValueBytes = Array.Empty<byte>();
    public byte[] EndToken = Array.Empty<byte>();

    public bool IsText => Type == 3;

    public int IntegerValue => ValueBytes.Length >= 4 ? BitConverter.ToInt32(ValueBytes, 0) : 0;

    public float RealValue => ValueBytes.Length >= 4 ? BitConverter.ToSingle(ValueBytes, 0) : 0f;

    public string TextValue
    {
        get
        {
            if (Type != 3 || ValueBytes.Length == 0)
            {
                return "";
            }
            int length = ValueBytes.Length;
            if (ValueBytes[length - 1] == 0)
            {
                length--;
            }
            return Encoding.UTF8.GetString(ValueBytes, 0, length);
        }
    }
}

public sealed class W3xObjectDefinition
{
    public string BaseId = "";
    public string NewId = "";
    public byte[] FlagsBytes = Array.Empty<byte>();
    public List<W3xObjectModification> Modifications { get; } = new();

    public string Id => string.IsNullOrEmpty(NewId) ? BaseId : NewId;

    public bool IsCustom => !string.Equals(Id, BaseId, StringComparison.Ordinal);

    public W3xObjectModification Find(string fieldId, int level = 0, int dataPointer = 0)
    {
        for (int i = Modifications.Count - 1; i >= 0; i--)
        {
            W3xObjectModification modification = Modifications[i];
            if (string.Equals(modification.FieldId, fieldId, StringComparison.Ordinal)
                && modification.Level == level
                && modification.DataPointer == dataPointer)
            {
                return modification;
            }
        }
        return null;
    }
}

public sealed class War3ObjectData
{
    const string AbilityTable = "UnitsAbilityData";
    const string BuffTable = "UnitsAbilityBuffData";

    // *AbilityFunc/String.txt 同时承载 [Axxx] 技能段和 [Bxxx] Buff 段，客户端
    // War3ObjectData.MergeAbilityText 按 AbilityBuffData.slk 已声明的 ID 分流。
    static readonly string[] AbilityTextTables =
    {
        "UnitsHumanAbilityFunc", "UnitsHumanAbilityStrings",
        "UnitsOrcAbilityFunc", "UnitsOrcAbilityStrings",
        "UnitsUndeadAbilityFunc", "UnitsUndeadAbilityStrings",
        "UnitsNightElfAbilityFunc", "UnitsNightElfAbilityStrings",
        "UnitsNeutralAbilityFunc", "UnitsNeutralAbilityStrings",
        "UnitsCampaignAbilityFunc", "UnitsCampaignAbilityStrings",
        "UnitsCommonAbilityFunc", "UnitsCommonAbilityStrings",
        "UnitsItemAbilityFunc", "UnitsItemAbilityStrings",
    };

    static Dictionary<string, AbilityTypeData> _abilities;
    static Dictionary<string, BuffTypeData> _buffs;
    static HashSet<string> _textIds;
    static bool _merged;

    /// <summary>技能/Buff 物编：AbilityData/BuffData 的结构列 + 16 张技能文本表的全部字段。</summary>
    static void EnsureMerged()
    {
        if (_merged) return;
        _merged = true;

        _buffs = new Dictionary<string, BuffTypeData>(StringComparer.OrdinalIgnoreCase);
        MergeSlk(_buffs, BuffTable);

        var text = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string table in AbilityTextTables)
            CollectText(table, text);

        _abilities = new Dictionary<string, AbilityTypeData>(StringComparer.OrdinalIgnoreCase);
        MergeSlk(_abilities, AbilityTable);

        _textIds = text.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Dictionary<string, string>> section in text)
        {
            War3TypeData target = _buffs.TryGetValue(section.Key, out BuffTypeData buff)
                ? buff
                : AbilityEntry(section.Key);
            foreach (KeyValuePair<string, string> field in section.Value)
                target.SetField(field.Key, field.Value);
        }
        GD.Print($"[OBJ] 技能物编 {_abilities.Count} 条，Buff 物编 {_buffs.Count} 条，文本表 {_textIds.Count} 条");
    }

    static AbilityTypeData AbilityEntry(string id)
    {
        if (_abilities.TryGetValue(id, out AbilityTypeData found)) return found;
        _abilities[id] = found = new AbilityTypeData { Id = id, BaseId = id };
        return found;
    }

    static void MergeSlk<T>(Dictionary<string, T> target, string tableName)
        where T : War3TypeData, new()
    {
        SlkTable table = War3SlkReader.Get(tableName);
        if (table == null) return;
        foreach (int row in table.DataRows)
        {
            string id = (table.Get(row, "alias") ?? "").Trim();
            if (id.Length == 0) continue;
            if (!target.TryGetValue(id, out T data))
                target[id] = data = new T { Id = id, BaseId = id };
            foreach (string column in table.Columns.Keys)
                data.SetField(column, table.Get(row, column));
        }
    }

    static void CollectText(string tableName, Dictionary<string, Dictionary<string, string>> into)
    {
        Variant json = GeneratedTexts.Get(tableName);
        if (json.VariantType != Variant.Type.Dictionary) return;
        Godot.Collections.Dictionary root = json.AsGodotDictionary();
        if (!root.ContainsKey("keys")) return;
        Godot.Collections.Dictionary keys = root["keys"].AsGodotDictionary();
        foreach (Variant keyVariant in keys.Keys)
        {
            string key = keyVariant.AsString();
            int dot = key.IndexOf('.');
            if (dot <= 0 || dot == key.Length - 1) continue;
            string id = key[..dot];
            string field = key[(dot + 1)..];
            if (!into.TryGetValue(id, out Dictionary<string, string> fields))
                into[id] = fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            fields.TryAdd(field, keys[keyVariant].AsString());
        }
    }

    public static AbilityTypeData GetAbility(string typeId)
    {
        EnsureMerged();
        return string.IsNullOrEmpty(typeId) ? null : _abilities.GetValueOrDefault(typeId);
    }

    public static BuffTypeData GetBuffType(string typeId)
    {
        EnsureMerged();
        return string.IsNullOrEmpty(typeId) ? null : _buffs.GetValueOrDefault(typeId);
    }

    /// <summary>技能文本表里出现过的所有 ID（技能段 + Buff 段），技能美术探针按它遍历。</summary>
    public static IReadOnlyCollection<string> TextAbilityIds
    {
        get
        {
            EnsureMerged();
            return _textIds;
        }
    }

    public BuffTypeData GetBuff(string typeId) => GetBuffType(typeId);

    public ObjectDataBuff CreateBuff(string typeId, int level, Unit caster = null,
        Ability sourceAbility = null, float? duration = null)
    {
        BuffTypeData data = GetBuff(typeId);
        if (data == null) return null;
        var buff = new ObjectDataBuff();
        buff.BindBuffObjectData(typeId, data, level, caster, sourceAbility);
        if (duration.HasValue) buff.Duration = Math.Max(0f, duration.Value);
        return buff;
    }

    public const string UnitFile = "war3map.w3u";
    public const string DoodadFile = "war3map.w3d";
    public const string AbilityFile = "war3map.w3a";
    public const string UpgradeFile = "war3map.w3q";
    public const string ItemFile = "war3map.w3t";
    public const string BuffFile = "war3map.w3b";

    public static readonly string[] AllFiles =
    {
        UnitFile, DoodadFile, AbilityFile, UpgradeFile, ItemFile, BuffFile,
    };

    public int Version { get; private set; }
    public string FileName { get; private set; }
    public bool Extended { get; private set; }
    public byte[] TrailingBytes { get; private set; } = Array.Empty<byte>();
    public byte[] RawBytes { get; private set; }

    public List<W3xObjectDefinition> OriginalObjects { get; } = new();
    public List<W3xObjectDefinition> CustomObjects { get; } = new();

    public IEnumerable<W3xObjectDefinition> Objects
    {
        get
        {
            foreach (W3xObjectDefinition definition in OriginalObjects)
            {
                yield return definition;
            }
            foreach (W3xObjectDefinition definition in CustomObjects)
            {
                yield return definition;
            }
        }
    }

    public static bool UsesExtendedFields(string fileName)
    {
        return string.Equals(fileName, DoodadFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, AbilityFile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, UpgradeFile, StringComparison.OrdinalIgnoreCase);
    }

    public W3xObjectDefinition Find(string id)
    {
        W3xObjectDefinition found = null;
        foreach (W3xObjectDefinition definition in Objects)
        {
            if (string.Equals(definition.Id, id, StringComparison.Ordinal))
            {
                found = definition;
            }
        }
        return found;
    }

    public string ResolveBaseId(string id, int depth = 0)
    {
        if (depth > 16)
        {
            return id;
        }
        W3xObjectDefinition definition = Find(id);
        if (definition == null || !definition.IsCustom)
        {
            return id;
        }
        return ResolveBaseId(definition.BaseId, depth + 1);
    }

    public static War3ObjectData Parse(byte[] data, string fileName)
    {
        if (data == null || data.Length < 8)
        {
            throw new InvalidDataException($"{fileName} 太短");
        }

        var reader = new W3xBinaryReader(data);
        var result = new War3ObjectData
        {
            RawBytes = data,
            FileName = fileName,
            Extended = UsesExtendedFields(fileName),
            Version = reader.I32(),
        };
        if (result.Version < 1 || result.Version > 3)
        {
            throw new InvalidDataException($"{fileName} 物体数据版本 {result.Version} 不受支持");
        }

        result.ReadSet(reader, result.OriginalObjects);
        result.ReadSet(reader, result.CustomObjects);
        if (reader.Remaining > 0)
        {
            result.TrailingBytes = reader.Bytes(reader.Remaining);
        }
        return result;
    }

    private void ReadSet(W3xBinaryReader reader, List<W3xObjectDefinition> target)
    {
        int count = reader.Count(100000);
        for (int i = 0; i < count; i++)
        {
            var definition = new W3xObjectDefinition
            {
                BaseId = reader.FourCc(),
                NewId = reader.FourCc(),
            };
            if (Version == 3)
            {
                int flagCount = reader.Count(64);
                definition.FlagsBytes = reader.Bytes(flagCount * 4);
            }

            int modifications = reader.Count(200000);
            for (int modification = 0; modification < modifications; modification++)
            {
                var entry = new W3xObjectModification
                {
                    FieldId = reader.FourCc(),
                    Type = reader.I32(),
                };
                if (entry.Type < 0 || entry.Type > 3)
                {
                    throw new InvalidDataException($"{FileName} 字段 {entry.FieldId} 的值类型 {entry.Type} 不受支持");
                }
                if (Extended)
                {
                    entry.Level = reader.I32();
                    entry.DataPointer = reader.I32();
                }
                entry.ValueBytes = entry.Type == 3 ? reader.NullStringBytes() : reader.Bytes(4);
                entry.EndToken = reader.Bytes(4);
                definition.Modifications.Add(entry);
            }
            target.Add(definition);
        }
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException($"{FileName} 写入尚未实现");
    }
}
