using System;
using System.Collections.Generic;
using Godot;

namespace OpenWC3.Game.Generated;

internal sealed class DayNightTrack
{
    internal int Interpolation;
    internal float[] Frames;
    internal float[] Values;
    internal float[] InTans;
    internal float[] OutTans;
    internal int Width => Values.Length / Frames.Length;
}

internal sealed class DayNightLight
{
    internal float Start;
    internal float End;
    internal Vector3 Color;
    internal float Intensity;
    internal Vector3 AmbientColor;
    internal float AmbientIntensity;
    internal DayNightTrack Rotation;
    internal DayNightTrack ColorTrack;
    internal DayNightTrack IntensityTrack;
    internal DayNightTrack AmbientColorTrack;
    internal DayNightTrack AmbientIntensityTrack;
    internal DayNightTrack VisibilityTrack;
}

internal static class DayNight
{
    internal static readonly Dictionary<string, (DayNightLight Terrain, DayNightLight Unit)> Environments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DNCAshenVale"] = (Lighting.DNCAshenVale.Terrain, Lighting.DNCAshenVale.Unit),
        ["DNCDalaran"] = (Lighting.DNCDalaran.Terrain, Lighting.DNCDalaran.Unit),
        ["DNCDungeon"] = (Lighting.DNCDungeon.Terrain, Lighting.DNCDungeon.Unit),
        ["DNCFelWood"] = (Lighting.DNCFelWood.Terrain, Lighting.DNCFelWood.Unit),
        ["DNCLordaeron"] = (Lighting.DNCLordaeron.Terrain, Lighting.DNCLordaeron.Unit),
        ["DNCUnderground"] = (Lighting.DNCUnderground.Terrain, Lighting.DNCUnderground.Unit),
    };
}
