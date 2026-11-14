using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class MapSource
{
    public GmapPackage Package { get; private set; }
    public TerrainData Terrain { get; private set; }
    public MapInfo Info { get; private set; }
    public War3MapUnits Units { get; private set; }
    public War3MapDoodads Doodads { get; private set; }
    public PathingData Pathing { get; private set; }
    public int SkyboxType { get; private set; }
    public List<MapPlayerInfo> Players { get; } = new();

    public string Name => Package?.Name ?? "unknown";

    public static MapSource Mount(string gmapPath)
    {
        GmapPackage package = GmapPackage.Mount(gmapPath);
        if (package == null) return null;

        var source = new MapSource { Package = package };

        byte[] w3i = package.ReadMapFile("war3map.w3i");
        if (w3i != null)
        {
            source.Info = MapInfo.Parse(w3i);
            source.Players.AddRange(source.Info.Players);
        }

        byte[] misc = package.ReadMapFile("war3mapMisc.txt");
        if (misc != null) source.SkyboxType = MiscInt(misc, "SkyboxType");
        return source;
    }

    public bool LoadTerrainAndObjects()
    {
        byte[] w3e = Package.ReadMapFile("war3map.w3e");
        if (w3e == null)
        {
            GD.PushError("[MAP] gmap 里没有 war3map.w3e");
            return false;
        }
        Terrain = TerrainData.Parse(w3e);

        byte[] units = Package.ReadMapFile("war3mapUnits.doo");
        if (units != null)
        {
            int version = Info?.GameVersionNumber ?? 0;
            Units = War3MapUnits.Parse(units, IsItem, version);
        }

        byte[] doodads = Package.ReadMapFile("war3map.doo");
        if (doodads != null)
        {
            Doodads = War3MapDoodads.Parse(doodads);
        }

        byte[] pathing = Package.ReadMapFile("war3map.wpm");
        if (pathing != null)
        {
            Pathing = PathingData.Parse(pathing);
            if (Pathing == null)
            {
                GD.PushWarning("[MAP] wpm 解析失败");
            }
        }

        GD.Print($"[MAP] {Name} 地形 {Terrain.Width}x{Terrain.Height} 单位 {Units?.Entries.Count ?? 0} "
            + $"装饰物 {Doodads?.Entries.Count ?? 0} 玩家 {Players.Count} "
            + $"寻路 {Pathing?.Width ?? 0}x{Pathing?.Height ?? 0}");
        return true;
    }

    public static MapSource Open(string gmapPath)
    {
        MapSource source = Mount(gmapPath);
        if (source == null) return null;
        return source.LoadTerrainAndObjects() ? source : null;
    }

    private static int MiscInt(byte[] text, string key)
    {
        string body = System.Text.Encoding.UTF8.GetString(text);
        foreach (string line in body.Split('\n'))
        {
            int equals = line.IndexOf('=');
            if (equals <= 0) continue;
            if (!line[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            return int.TryParse(line[(equals + 1)..].Trim(), out int value) ? value : 0;
        }
        return 0;
    }

    private static bool IsItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        SlkTable items = War3SlkReader.ItemData();
        if (items.FindRow("itemID", id) >= 0) return true;

        string baseId = id.Length == 4 ? id[..3] + "0" : id;
        return baseId != id && items.FindRow("itemID", baseId) >= 0;
    }
}
