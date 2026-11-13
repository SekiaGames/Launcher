using System;
using System.Collections.Generic;

namespace OpenWC3.Game.Generated;

internal readonly record struct DayNightKey(
    double Hour,
    float DirX, float DirY, float DirZ,
    float SunR, float SunG, float SunB, float SunIntensity,
    float AmbR, float AmbG, float AmbB, float AmbIntensity);

internal static class DayNight
{
    internal static readonly Dictionary<string, (DayNightKey[] Terrain, DayNightKey[] Unit)> Environments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DNCAshenVale"] = (Lighting.DNCAshenVale.Terrain, Lighting.DNCAshenVale.Unit),
        ["DNCDalaran"] = (Lighting.DNCDalaran.Terrain, Lighting.DNCDalaran.Unit),
        ["DNCDungeon"] = (Lighting.DNCDungeon.Terrain, Lighting.DNCDungeon.Unit),
        ["DNCFelWood"] = (Lighting.DNCFelWood.Terrain, Lighting.DNCFelWood.Unit),
        ["DNCLordaeron"] = (Lighting.DNCLordaeron.Terrain, Lighting.DNCLordaeron.Unit),
        ["DNCUnderground"] = (Lighting.DNCUnderground.Terrain, Lighting.DNCUnderground.Unit),
    };
}
