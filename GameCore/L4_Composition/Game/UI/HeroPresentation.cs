namespace OpenWC3.Game.UI;

internal static class HeroPresentation
{
    internal const int MaxHeroButtons = 7;

    internal static bool ShowInHeroBar(Unit unit)
        => unit != null && unit.IsHero && !unit.IsIllusion
            && unit.Type != null && !unit.Type.HideInHeroBar;

    internal static bool AnnounceDeath(Unit unit)
        => unit != null && unit.IsHero && !unit.IsIllusion
            && unit.Type != null && !unit.Type.HideHeroDeathMsg;

    internal static string DeathSound(Unit unit, int localPlayer, Func<int, int, bool> allied)
    {
        if (!AnnounceDeath(unit)) return null;
        if (unit.Player == localPlayer) return "HeroDiesSound";
        return allied != null && allied(localPlayer, unit.Player) ? "AllyHeroDiesSound" : null;
    }
}