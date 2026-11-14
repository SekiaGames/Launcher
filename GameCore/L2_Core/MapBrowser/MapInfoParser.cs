using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using OpenWC3.War3Formats.Text;
namespace OpenWC3.Game;

public sealed record MapPlayerInfo(
    int Id,
    int Controller,
    int Race,
    int Flags,
    string Name,
    float StartX,
    float StartY,
    int LowPriorityAllies,
    int HighPriorityAllies)
{
    public const int FixedStartPositionFlag = 1;

    public bool FixedStartPosition => (Flags & FixedStartPositionFlag) != 0;
}

public sealed record MapForceInfo(int Flags, uint PlayerMask, string Name)
{
    public const int AlliedFlag = 1;
    public const int AlliedVictoryFlag = 2;
    public const int SharedVisionFlag = 8;
    public const int SharedControlFlag = 16;
    public const int SharedAdvancedControlFlag = 32;

    public bool ContainsPlayer(int playerId)
    {
        return playerId is >= 0 and < 32 && (PlayerMask & (1u << playerId)) != 0;
    }
}

public sealed record W3iItemDrop(string ItemId, int Chance);

/// <summary>w3i 的随机物品表：一个表里若干组，每组按 chance 掷一件。</summary>
public sealed class W3iItemTable
{
    public string Name { get; init; } = "";
    public readonly List<List<W3iItemDrop>> Sets = new();
}

public sealed class MapInfo
{
    public const int MeleeMapFlag = 0x0004;
    public const int MaskedAreasPartiallyVisibleFlag = 0x0010;
    public const int FixedPlayerSettingsFlag = 0x0020;
    public const int CustomForcesFlag = 0x0040;

    public int FormatVersion { get; private set; }
    public int SaveCount { get; private set; }
    public int EditorVersion { get; private set; }

    public bool HasGameVersion => FormatVersion > 27;
    public int GameMajor { get; private set; }
    public int GameMinor { get; private set; }
    public int GamePatch { get; private set; }
    public int GameBuild { get; private set; }
    public int GameVersionNumber => GameMajor * 100 + GameMinor;

    public string NameRef { get; private set; }
    public string AuthorRef { get; private set; }
    public string DescriptionRef { get; private set; }
    public string PlayersRef { get; private set; }

    public float[] CameraBounds { get; private set; } = new float[8];
    public int[] CameraBoundsComplements { get; private set; } = new int[4];

    public int PlayableWidth { get; private set; }
    public int PlayableHeight { get; private set; }
    public int Flags { get; private set; }
    public char Tileset { get; private set; }
    public int CampaignBackground { get; private set; }

    public string LoadingScreenModel { get; private set; } = "";
    public string LoadingScreenText { get; private set; } = "";
    public string LoadingScreenTitle { get; private set; } = "";
    public string LoadingScreenSubtitle { get; private set; } = "";
    public int GameDataSet { get; private set; }
    public string PrologueScreenModel { get; private set; } = "";
    public string PrologueText { get; private set; } = "";
    public string PrologueTitle { get; private set; } = "";
    public string PrologueSubtitle { get; private set; } = "";

    public int UseTerrainFog { get; private set; }
    public float[] FogHeight { get; } = new float[2];
    public float FogDensity { get; private set; }
    public byte[] FogColor { get; private set; } = new byte[4];
    public int GlobalWeather { get; private set; }
    public char LightEnvTileset { get; private set; }
    public byte[] WaterVertexColor { get; private set; } = new byte[4];

    public string MinimapTgaPath { get; private set; } = "";
    public int MinimapTgaWidth { get; private set; }
    public int MinimapTgaHeight { get; private set; }

    public List<MapPlayerInfo> Players { get; } = new();
    public List<MapForceInfo> Forces { get; } = new();
    public List<W3iItemTable> ItemTables { get; } = new();

    public byte[] RawBytes { get; private set; }

    public bool Missing { get; private set; }

    public bool IsMelee => (Flags & MeleeMapFlag) != 0;
    public bool UsesCustomForces => (Flags & CustomForcesFlag) != 0;
    public bool HasFixedPlayerSettings => (Flags & FixedPlayerSettingsFlag) != 0;
    public bool MaskedAreasPartiallyVisible => (Flags & MaskedAreasPartiallyVisibleFlag) != 0;

    public static MapInfo CreateMissing()
    {
        return new MapInfo
        {
            Missing = true,
            NameRef = "",
            AuthorRef = "",
            DescriptionRef = "",
            PlayersRef = "",
        };
    }

    public static MapInfo Parse(byte[] data)
    {
        if (data == null || data.Length < 16)
        {
            throw new InvalidDataException("war3map.w3i 太短");
        }

        var reader = new W3xBinaryReader(data);
        var info = new MapInfo
        {
            RawBytes = data,
            FormatVersion = reader.I32(),
            SaveCount = reader.I32(),
            EditorVersion = reader.I32(),
        };

        if (info.HasGameVersion)
        {
            info.GameMajor = reader.I32();
            info.GameMinor = reader.I32();
            info.GamePatch = reader.I32();
            info.GameBuild = reader.I32();
        }

        info.NameRef = reader.NullString(Encoding.UTF8);
        info.AuthorRef = reader.NullString(Encoding.UTF8);
        info.DescriptionRef = reader.NullString(Encoding.UTF8);
        info.PlayersRef = reader.NullString(Encoding.UTF8);

        for (int i = 0; i < 8; i++)
        {
            info.CameraBounds[i] = reader.F32();
        }
        for (int i = 0; i < 4; i++)
        {
            info.CameraBoundsComplements[i] = reader.I32();
        }

        info.PlayableWidth = reader.I32();
        info.PlayableHeight = reader.I32();
        info.Flags = reader.I32();
        info.Tileset = (char)reader.U8();
        info.CampaignBackground = reader.I32();

        if (info.FormatVersion > 24)
        {
            info.LoadingScreenModel = reader.NullString(Encoding.UTF8);
        }
        info.LoadingScreenText = reader.NullString(Encoding.UTF8);
        info.LoadingScreenTitle = reader.NullString(Encoding.UTF8);
        info.LoadingScreenSubtitle = reader.NullString(Encoding.UTF8);

        info.GameDataSet = reader.I32();
        if (info.FormatVersion > 24)
        {
            info.PrologueScreenModel = reader.NullString(Encoding.UTF8);
        }
        info.PrologueText = reader.NullString(Encoding.UTF8);
        info.PrologueTitle = reader.NullString(Encoding.UTF8);
        info.PrologueSubtitle = reader.NullString(Encoding.UTF8);

        if (info.FormatVersion > 24)
        {
            ReadTerrainFog(reader, info);
        }

        if (info.FormatVersion >= 28)
        {
            reader.I32();
        }
        if (info.FormatVersion >= 31)
        {
            reader.I32();
            reader.I32();
        }
        if (info.FormatVersion >= 32)
        {
            reader.I32();
            reader.I32();
        }
        if (info.FormatVersion >= 33)
        {
            reader.I32();
        }

        ReadMinimap(reader, info);
        ReadPlayers(reader, info);
        ReadForces(reader, info);
        ReadDropTables(reader, info);
        return info;
    }

    /// <summary>势力之后是升级/科技可用性，再往后是随机单位表与随机物品表；布局不符就整体放弃，不影响地图打开。</summary>
    private static void ReadDropTables(W3xBinaryReader reader, MapInfo info)
    {
        if (reader.Remaining < 4)
        {
            return;
        }

        try
        {
            SkipAvailability(reader);
            SkipAvailability(reader);
            // RoC 老图的 w3i 到「升级/科技可用性」两段就结束，后面没有随机单位表与随机物品表
            if (reader.Remaining < 4) return;
            SkipRandomTables(reader);
            if (reader.Remaining < 4) return;
            ReadRandomItemTables(reader, info);
        }
        catch (InvalidDataException)
        {
            info.ItemTables.Clear();
        }
        catch (EndOfStreamException)
        {
            info.ItemTables.Clear();
        }

        foreach (W3iItemTable table in info.ItemTables)
        {
            if (!LooksLikeRawcode(table.Name) || table.Sets.Exists(set => set.Exists(drop => !LooksLikeRawcode(drop.ItemId))))
            {
                info.ItemTables.Clear();
                break;
            }
        }
    }

    private static bool LooksLikeRawcode(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 4) return false;
        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character)) return false;
        }
        return true;
    }

    private static void SkipAvailability(W3xBinaryReader reader)
    {
        int count = reader.Count(4096);
        for (int i = 0; i < count; i++)
        {
            reader.I32();
            reader.FourCc();
            reader.I32();
            reader.I32();
        }
    }

    private static void SkipRandomTables(W3xBinaryReader reader)
    {
        int tables = reader.Count(4096);
        for (int t = 0; t < tables; t++)
        {
            reader.FourCc();
            int sets = reader.Count(4096);
            for (int s = 0; s < sets; s++)
            {
                int units = reader.Count(4096);
                for (int u = 0; u < units; u++)
                {
                    reader.FourCc();
                    reader.I32();
                }
            }
        }
    }

    private static void ReadRandomItemTables(W3xBinaryReader reader, MapInfo info)
    {
        int tables = reader.Count(4096);
        for (int t = 0; t < tables; t++)
        {
            var table = new W3iItemTable { Name = reader.FourCc() };
            int sets = reader.Count(4096);
            for (int s = 0; s < sets; s++)
            {
                var set = new List<W3iItemDrop>();
                int items = reader.Count(4096);
                for (int i = 0; i < items; i++)
                {
                    string id = reader.FourCc();
                    set.Add(new W3iItemDrop(id, reader.I32()));
                }
                table.Sets.Add(set);
            }
            info.ItemTables.Add(table);
        }
    }

    private static void ReadTerrainFog(W3xBinaryReader reader, MapInfo info)
    {
        info.UseTerrainFog = reader.I32();
        info.FogHeight[0] = reader.F32();
        info.FogHeight[1] = reader.F32();
        info.FogDensity = reader.F32();
        info.FogColor = reader.Bytes(4);
        info.GlobalWeather = reader.I32();
        reader.NullString(Encoding.UTF8);
        info.LightEnvTileset = (char)reader.U8();
        info.WaterVertexColor = reader.Bytes(4);
    }

    private static void ReadMinimap(W3xBinaryReader reader, MapInfo info)
    {
        if (reader.AtEnd)
        {
            return;
        }
        int probe = reader.Position;
        string path = reader.NullString(Encoding.UTF8);
        if (!string.IsNullOrEmpty(path)
            && path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
            && reader.Remaining >= 8)
        {
            info.MinimapTgaPath = path;
            info.MinimapTgaWidth = reader.I32();
            info.MinimapTgaHeight = reader.I32();
            return;
        }
        reader.Position = probe;
    }

    private static void ReadPlayers(W3xBinaryReader reader, MapInfo info)
    {
        if (reader.Remaining < 4)
        {
            return;
        }
        int count = reader.Count(32);
        for (int i = 0; i < count; i++)
        {
            int id = reader.I32();
            int controller = reader.I32();
            int race = reader.I32();
            int flags = reader.I32();
            string name = reader.NullString(Encoding.UTF8);
            float startX = reader.F32();
            float startY = reader.F32();
            int lowPriorityAllies = reader.I32();
            int highPriorityAllies = reader.I32();
            if (info.FormatVersion >= 31)
            {
                reader.I32();
                reader.I32();
            }
            info.Players.Add(new MapPlayerInfo(id, controller, race, flags, name, startX, startY, lowPriorityAllies, highPriorityAllies));
        }
    }

    private static void ReadForces(W3xBinaryReader reader, MapInfo info)
    {
        if (reader.Remaining < 4)
        {
            return;
        }
        int count = reader.Count(32);
        for (int i = 0; i < count; i++)
        {
            int flags = reader.I32();
            uint mask = reader.U32();
            string name = reader.NullString(Encoding.UTF8);
            info.Forces.Add(new MapForceInfo(flags, mask, name));
        }
    }

    public static int ExtractStringId(string reference)
    {
        const string prefix = "TRIGSTR_";
        if (reference != null && reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(reference[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            return id;
        }
        return -1;
    }

    public string ResolveName(WtsDocument strings)
    {
        return Resolve(NameRef, strings);
    }

    public static string Resolve(string reference, WtsDocument strings)
    {
        int id = ExtractStringId(reference);
        return id >= 0 ? strings?.Get(id) ?? reference : reference;
    }

    public void ResolveStrings(WtsDocument strings)
    {
        if (strings == null)
        {
            return;
        }
        LoadingScreenText = Resolve(LoadingScreenText, strings);
        LoadingScreenTitle = Resolve(LoadingScreenTitle, strings);
        LoadingScreenSubtitle = Resolve(LoadingScreenSubtitle, strings);
        PrologueText = Resolve(PrologueText, strings);
        PrologueTitle = Resolve(PrologueTitle, strings);
        PrologueSubtitle = Resolve(PrologueSubtitle, strings);
    }

    public static int NameStringId(byte[] w3i)
    {
        return ExtractStringId(Parse(w3i).NameRef);
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException("war3map.w3i 写入尚未实现");
    }
}
