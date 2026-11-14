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
    public List<W3iPlayer> Players { get; } = new();

    public string Name => Package?.Name ?? "unknown";

    public static MapSource Open(string gmapPath)
    {
        GmapPackage package = GmapPackage.Mount(gmapPath);
        if (package == null) return null;

        var source = new MapSource { Package = package };

        byte[] w3e = package.ReadMapFile("war3map.w3e");
        if (w3e == null)
        {
            GD.PushError("[MAP] gmap 里没有 war3map.w3e");
            return null;
        }
        source.Terrain = TerrainData.Parse(w3e);

        byte[] w3i = package.ReadMapFile("war3map.w3i");
        if (w3i != null)
        {
            source.Info = MapInfo.Parse(w3i);
            source.Players.AddRange(source.Info.Players);
        }

        byte[] units = package.ReadMapFile("war3mapUnits.doo");
        if (units != null)
        {
            int version = source.Info?.GameVersionNumber ?? 0;
            source.Units = War3MapUnits.Parse(units, IsItem, version);
        }

        byte[] doodads = package.ReadMapFile("war3map.doo");
        if (doodads != null)
        {
            source.Doodads = War3MapDoodads.Parse(doodads);
        }

        byte[] pathing = package.ReadMapFile("war3map.wpm");
        if (pathing != null)
        {
            source.Pathing = PathingData.Parse(pathing);
            if (source.Pathing == null)
            {
                GD.PushWarning("[MAP] wpm 解析失败");
            }
        }

        GD.Print($"[MAP] {source.Name} 地形 {source.Terrain.Width}x{source.Terrain.Height} 单位 {source.Units?.Entries.Count ?? 0} "
            + $"装饰物 {source.Doodads?.Entries.Count ?? 0} 玩家 {source.Players.Count} "
            + $"寻路 {source.Pathing?.Width ?? 0}x{source.Pathing?.Height ?? 0}");
        return source;
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