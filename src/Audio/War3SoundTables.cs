using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Launcher;

/// <summary>
/// 单位语音/音效库。数据来自 extract 的 UISoundInfo* 表：
///   SoundName → FileNames + DirectoryBase + Volume + MinDistance/MaxDistance
/// 音频是原始 wav（pck 里没有 Godot 导入产物），所以自己解析 RIFF 头再交给 AudioStreamWav。
/// </summary>
public static class War3SoundTables
{
    private sealed class Entry
    {
        public string[] Files = Array.Empty<string>();
        public string Directory = "";
        public float Volume = 1f;
        public float MinDistance = 3000f;
        public float MaxDistance = 10000f;
        public bool RandomPitch;
    }

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AudioStream> Streams = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Random Rng = new(20261111);
    private static bool _loaded;

    public static int Count => Entries.Count;

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        LoadTable("UISoundInfoUnitAckSounds");
        LoadTable("UISoundInfoUnitCombatSounds");
        LoadTable("UISoundInfoAbilitySounds");
        LoadTable("UISoundInfoUISounds");
        GD.Print($"[AUDIO] 音效条目 {Entries.Count}");
    }

    private static void LoadTable(string tableName)
    {
        Variant parsed = Assets.ReadJson($"res://extract/Tables/{tableName}.json");
        if (parsed.VariantType != Variant.Type.Dictionary) return;
        Godot.Collections.Dictionary json = parsed.AsGodotDictionary();
        if (json.Count == 0) return;

        if (json["columns"].VariantType != Variant.Type.Array || json["rows"].VariantType != Variant.Type.Array) return;

        var columns = new List<string>();
        foreach (Variant column in json["columns"].AsGodotArray()) columns.Add(column.AsString());
        int nameIndex = IndexOf(columns, "SoundName");
        int filesIndex = IndexOf(columns, "FileNames");
        if (nameIndex < 0 || filesIndex < 0) return;

        int dirIndex = IndexOf(columns, "DirectoryBase");
        int volumeIndex = IndexOf(columns, "Volume");
        int minIndex = IndexOf(columns, "MinDistance");
        int maxIndex = IndexOf(columns, "MaxDistance");
        int flagsIndex = IndexOf(columns, "Flags");

        foreach (Variant rowVariant in json["rows"].AsGodotArray())
        {
            Godot.Collections.Array row = rowVariant.AsGodotArray();
            if (row.Count <= filesIndex) continue;

            string name = Cell(row, nameIndex);
            string files = Cell(row, filesIndex);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(files)) continue;

            var entry = new Entry
            {
                Files = files.Split(',', StringSplitOptions.RemoveEmptyEntries),
                Directory = Cell(row, dirIndex).Replace('\\', '/').Trim('/'),
                Volume = Mathf.Clamp(ParseFloat(Cell(row, volumeIndex), 127f) / 127f, 0f, 1f),
                MinDistance = ParseFloat(Cell(row, minIndex), 3000f),
                MaxDistance = ParseFloat(Cell(row, maxIndex), 10000f),
                RandomPitch = Cell(row, flagsIndex).Contains("RANDOMPITCH", StringComparison.OrdinalIgnoreCase),
            };
            for (int i = 0; i < entry.Files.Length; i++) entry.Files[i] = entry.Files[i].Trim();
            Entries[name] = entry;
        }
    }

    private static int IndexOf(List<string> columns, string name)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private static string Cell(Godot.Collections.Array row, int index)
    {
        if (index < 0 || index >= row.Count) return "";
        return row[index].AsString().Trim();
    }

    private static float ParseFloat(string text, float fallback)
    {
        return float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    public static bool Has(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return false;
        Load();
        return Entries.ContainsKey(soundName);
    }

    public static float MinDistance(string soundName)
    {
        Load();
        return Entries.TryGetValue(soundName ?? "", out Entry entry) ? entry.MinDistance : 3000f;
    }

    public static float MaxDistance(string soundName)
    {
        Load();
        return Entries.TryGetValue(soundName ?? "", out Entry entry) ? entry.MaxDistance : 10000f;
    }

    /// <summary>取一条可播放的音频流；同一 SoundName 每次随机挑一个变体。</summary>
    public static AudioStream Pick(string soundName, out float volume)
    {
        volume = 1f;
        if (string.IsNullOrEmpty(soundName)) return null;

        Load();
        if (!Entries.TryGetValue(soundName, out Entry entry) || entry.Files.Length == 0) return null;

        volume = entry.Volume;
        int start = Rng.Next(entry.Files.Length);
        for (int i = 0; i < entry.Files.Length; i++)
        {
            string file = entry.Files[(start + i) % entry.Files.Length];
            string resPath = $"res://extract/{entry.Directory}/{file}";
            if (Streams.TryGetValue(resPath, out AudioStream cached))
            {
                if (cached != null) return cached;
                continue;
            }

            AudioStream stream = LoadStream(resPath);
            Streams[resPath] = stream;
            if (stream != null) return stream;
        }
        return null;
    }

    /// <summary>
    /// extract.pck 是 Godot 导出包（wav 走 .remap），所以优先让 ResourceLoader 解；
    /// 只有拿不到时才退回去自己解析 RIFF（例如直接读磁盘目录）。
    /// </summary>
    private static AudioStream LoadStream(string resPath)
    {
        if (ResourceLoader.Exists(resPath))
        {
            AudioStream stream = ResourceLoader.Load<AudioStream>(resPath);
            if (stream != null) return stream;
        }
        return LoadWav(resPath);
    }

    /// <summary>解析 RIFF/WAVE（PCM）为 AudioStreamWav。</summary>
    public static AudioStreamWav LoadWav(string resPath)
    {
        byte[] raw = Assets.ReadBytes(resPath);
        if (raw == null || raw.Length < 44) return null;
        if (raw[0] != (byte)'R' || raw[1] != (byte)'I' || raw[2] != (byte)'F' || raw[3] != (byte)'F') return null;
        if (raw[8] != (byte)'W' || raw[9] != (byte)'A' || raw[10] != (byte)'V' || raw[11] != (byte)'E') return null;

        int channels = 1;
        int rate = 22050;
        int bits = 16;
        int dataOffset = -1;
        int dataLength = 0;

        int pos = 12;
        while (pos + 8 <= raw.Length)
        {
            string id = Encoding.ASCII.GetString(raw, pos, 4);
            int size = BitConverter.ToInt32(raw, pos + 4);
            int body = pos + 8;
            if (size < 0 || body + size > raw.Length) size = raw.Length - body;

            if (id == "fmt " && size >= 16)
            {
                channels = BitConverter.ToInt16(raw, body + 2);
                rate = BitConverter.ToInt32(raw, body + 4);
                bits = BitConverter.ToInt16(raw, body + 14);
            }
            else if (id == "data")
            {
                dataOffset = body;
                dataLength = size;
                break;
            }

            pos = body + size + (size & 1);
        }

        if (dataOffset < 0 || dataLength <= 0) return null;

        var pcm = new byte[dataLength];
        Buffer.BlockCopy(raw, dataOffset, pcm, 0, dataLength);

        return new AudioStreamWav
        {
            Data = pcm,
            Format = bits == 8 ? AudioStreamWav.FormatEnum.Format8Bits : AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Mathf.Max(4000, rate),
            Stereo = channels >= 2,
        };
    }
}
