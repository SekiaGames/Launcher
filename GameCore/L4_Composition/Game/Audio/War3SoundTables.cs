using Godot;
using OpenWC3.War3Formats;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenWC3.Game;

/// <summary>
/// 单位语音/音效库。数据来自 extract 的 UISoundInfo* 表：
///   SoundName → FileNames + DirectoryBase + Volume + Pitch + MinDistance/MaxDistance
/// 音频是原始 wav（pck 里没有 Godot 导入产物），所以自己解析 RIFF 头再交给 AudioStreamWav。
/// </summary>
public static class War3SoundTables
{
    public sealed class Entry
    {
        public string Name = "";
        public string[] Files = Array.Empty<string>();
        public string Directory = "";
        public float Volume = 1f;
        public float Pitch = 1f;
        public float PitchVariance;
        public float MinDistance = 3000f;
        public float MaxDistance = 10000f;
        public float DistanceCutoff = 3000f;
        public int Priority;
        public string Flags = "";
        public bool Looping;
        public bool RandomPitch;
    }

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AudioStream> Streams = new(StringComparer.OrdinalIgnoreCase);
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
        LoadTable("UISoundInfoAmbienceSounds");
        GD.Print($"[AUDIO] 音效条目 {Entries.Count}");
    }

    private static void LoadTable(string tableName)
    {
        SlkTable table = War3SlkReader.Get(tableName);
        if (!table.HasColumn("SoundName") || !table.HasColumn("FileNames")) return;

        bool hasDirectory = table.HasColumn("DirectoryBase");
        bool hasVolume = table.HasColumn("Volume");
        bool hasPitch = table.HasColumn("Pitch");
        bool hasVariance = table.HasColumn("PitchVariance");
        bool hasMin = table.HasColumn("MinDistance");
        bool hasMax = table.HasColumn("MaxDistance");
        bool hasCutoff = table.HasColumn("DistanceCutoff");
        bool hasFlags = table.HasColumn("Flags");
        bool hasPriority = table.HasColumn("Priority");

        foreach (int row in table.DataRows)
        {
            string name = Cell(table, row, "SoundName");
            string files = Cell(table, row, "FileNames");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(files)) continue;

            string flags = hasFlags ? Cell(table, row, "Flags") : "";
            var entry = new Entry
            {
                Name = name,
                Files = files.Split(',', StringSplitOptions.RemoveEmptyEntries),
                Directory = hasDirectory
                    ? Cell(table, row, "DirectoryBase").Replace('\\', '/').Trim('/') : "",
                Volume = Mathf.Clamp(ParseFloat(hasVolume ? Cell(table, row, "Volume") : "", 127f) / 127f, 0f, 1f),
                Pitch = ParseFloat(hasPitch ? Cell(table, row, "Pitch") : "", 1f),
                PitchVariance = ParseFloat(hasVariance ? Cell(table, row, "PitchVariance") : "", 0f),
                MinDistance = ParseFloat(hasMin ? Cell(table, row, "MinDistance") : "", 0f),
                MaxDistance = ParseFloat(hasMax ? Cell(table, row, "MaxDistance") : "", 10000f),
                DistanceCutoff = ParseFloat(hasCutoff ? Cell(table, row, "DistanceCutoff") : "", 3000f),
                Priority = (int)ParseFloat(hasPriority ? Cell(table, row, "Priority") : "", 0f),
                Flags = flags,
                Looping = flags.Contains("LOOPING", StringComparison.OrdinalIgnoreCase),
                RandomPitch = flags.Contains("RANDOMPITCH", StringComparison.OrdinalIgnoreCase),
            };
            for (int i = 0; i < entry.Files.Length; i++) entry.Files[i] = entry.Files[i].Trim();
            Entries[name] = entry;
        }
    }

    private static string Cell(SlkTable table, int row, string column) =>
        table.Get(row, column) ?? "";

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

    public static bool TryGet(string soundName, out Entry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(soundName)) return false;
        Load();
        return Entries.TryGetValue(soundName, out entry);
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
        return PickStream(entry, Random.Shared.Next(entry.Files.Length));
    }

    public static AudioStream PickStream(Entry entry, int start)
    {
        if (entry == null || entry.Files.Length == 0) return null;
        for (int i = 0; i < entry.Files.Length; i++)
        {
            string file = entry.Files[(start + i) % entry.Files.Length];
            AudioStream stream = LoadStream(FilePath(entry, file));
            if (stream != null) return stream;
        }
        return null;
    }

    public static string FilePath(Entry entry, string file)
        => ResolvePath($"{entry.Directory}/{file}");

    public static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("res://", StringComparison.Ordinal)) return path;
        return "res://extract/" + path.Replace('\\', '/').TrimStart('/');
    }

    public static bool ResExists(string path)
    {
        string resolved = ResolvePath(path);
        return resolved != null && ResourceLoader.Exists(resolved);
    }

    /// <summary>
    /// extract.pck 是 Godot 导出包（wav 走 .remap），所以优先让 ResourceLoader 解；
    /// 只有拿不到时才退回去自己解析 RIFF（例如直接读磁盘目录）。
    /// </summary>
    public static AudioStream LoadClip(string path)
        => LoadStream(ResolvePath(path));

    private static AudioStream LoadStream(string resPath)
    {
        if (Streams.TryGetValue(resPath, out AudioStream cached)) return cached;
        AudioStream stream = null;
        if (ResourceLoader.Exists(resPath))
        {
            stream = ResourceLoader.Load<AudioStream>(resPath);
            if (stream == null) stream = LoadWav(resPath);
        }
        else
        {
            stream = LoadWav(resPath);
        }
        Streams[resPath] = stream;
        return stream;
    }

    /// <summary>解析 RIFF/WAVE（PCM）为 AudioStreamWav。</summary>
    public static AudioStreamWav LoadWav(string path)
    {
        string resPath = ResolvePath(path);
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
