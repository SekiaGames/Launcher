using System;

namespace OpenWC3.Game.UI;

internal enum MinimapIconKind
{
    Unit = 0,
    Hero,
    Resource,
    EntangledResource,
    HauntedResource,
    Neutral,
    Cop,
    Item,
    CreepCamp,
    StartLocation,
}

internal enum MinimapFogLevel : byte
{
    Masked = 0,
    Dark = 1,
    Fogged = 2,
    Visible = 3,
}

internal readonly record struct MinimapUnitView(
    int SimulationId, float X, float Y, int PlayerId, int Team,
    bool IsHero, bool IsBuilding,
    MinimapIconKind Icon = MinimapIconKind.Unit,
    float CampStrength = 0f,
    MinimapFogLevel Fog = MinimapFogLevel.Visible,
    int ColorIndex = -1, string TexturePath = null, bool? Allied = null)
{
    public MinimapUnitView WithFog(MinimapFogLevel level) => this with { Fog = level };

    public static MinimapUnitView From(Unit unit, MinimapFogLevel fog, bool? allied = null)
    {
        return new MinimapUnitView(0, -unit.Position.X, unit.Position.Z, unit.Player, unit.Player,
            unit.IsHero, unit.IsBuilding, Classify(unit), 0f, fog, unit.Player, null, allied);
    }

    internal static MinimapIconKind Classify(Unit unit)
    {
        if (unit?.Type == null) return MinimapIconKind.Unit;
        string id = unit.Type.Id ?? "";
        if (id.Equals("ugol", StringComparison.OrdinalIgnoreCase)) return MinimapIconKind.HauntedResource;
        if (id.Equals("egol", StringComparison.OrdinalIgnoreCase)) return MinimapIconKind.EntangledResource;
        if (id.Equals("ngol", StringComparison.OrdinalIgnoreCase)) return MinimapIconKind.Resource;
        if (id.Equals("ncop", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ncp2", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ncp3", StringComparison.OrdinalIgnoreCase)) return MinimapIconKind.Cop;
        return unit.IsHero ? MinimapIconKind.Hero : MinimapIconKind.Unit;
    }
}

internal readonly record struct MinimapPingView(int Id, float X, float Y, float Remaining,
    float Duration, byte Red, byte Green, byte Blue, bool ExtraEffects);

internal readonly record struct MinimapSpecialIconView(float X, float Y, string TexturePath,
    System.Numerics.Vector4 Color, float Size,
    MinimapIconKind Kind = MinimapIconKind.Unit, int CampStrength = 0);