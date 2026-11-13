namespace OpenWC3.Game.UI;

internal static class HeroPresentation
{
    internal const int MaxHeroButtons = 7;

    internal static bool ShowInHeroBar(Unit unit)
        => unit != null && unit.IsHero && !unit.IsIllusion
            && unit.Type != null && !unit.Type.HideInHeroBar;
}