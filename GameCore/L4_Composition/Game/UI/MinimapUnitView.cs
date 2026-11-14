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

    public static MinimapUnitView From(Unit unit, MinimapFogLevel fog, bool? allied = null,
        int observerPlayerId = -1)
    {
        return new MinimapUnitView(unit.SimulationId, -unit.Position.X, unit.Position.Z,
            unit.Player, unit.Player, unit.IsHero, unit.IsBuilding,
            Classify(unit, observerPlayerId), 0f, fog, unit.Player, null, allied);
    }

    internal static MinimapIconKind Classify(Unit unit, int observerPlayerId = -1)
    {
        if (unit?.Type == null) return MinimapIconKind.Unit;
        string mineCode = MineAbilityCode(unit);
        if (mineCode != null)
        {
            // 客户端 GoldMineIdentity：闹鬼/缠绕只对拥有者显示，其他观察者看普通金矿。
            if (observerPlayerId < 0 || unit.Player == observerPlayerId)
            {
                if (mineCode.Equals("Abgm", StringComparison.OrdinalIgnoreCase))
                    return MinimapIconKind.HauntedResource;
                if (mineCode.Equals("Aegm", StringComparison.OrdinalIgnoreCase))
                    return MinimapIconKind.EntangledResource;
            }
            return MinimapIconKind.Resource;
        }
        string id = unit.Type.Id ?? "";
        if (id.Equals("ncop", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ncp2", StringComparison.OrdinalIgnoreCase)
            || id.Equals("ncp3", StringComparison.OrdinalIgnoreCase)) return MinimapIconKind.Cop;
        if (unit.Type.MinimapIcon != 0) return MinimapIconKind.Neutral;
        return unit.IsHero && !unit.Type.HideHeroMinimap
            ? MinimapIconKind.Hero : MinimapIconKind.Unit;
    }

    private static string MineAbilityCode(Unit unit)
    {
        foreach (Ability ability in unit.Abilities.Items)
        {
            string code = ability.Type?.Code;
            if (string.IsNullOrEmpty(code)) code = ability.Id;
            if (code.Equals("Agld", StringComparison.OrdinalIgnoreCase)
                || code.Equals("Abgm", StringComparison.OrdinalIgnoreCase)
                || code.Equals("Aegm", StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }
        return null;
    }
}

internal readonly record struct MinimapPingView(int Id, float X, float Y, float Remaining,
    float Duration, byte Red, byte Green, byte Blue, bool ExtraEffects);

internal readonly record struct MinimapSpecialIconView(float X, float Y, string TexturePath,
    System.Numerics.Vector4 Color, float Size,
    MinimapIconKind Kind = MinimapIconKind.Unit, int CampStrength = 0);