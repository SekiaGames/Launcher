using System;
using Godot;

namespace OpenWC3.Game;

public static class IllusionPresentation
{
    public static bool IsIdentified(Unit unit, int observerPlayerId, Func<int, int, bool> allied)
    {
        if (unit?.IsIllusion != true) return false;
        if (unit.Player == observerPlayerId) return true;
        return allied != null && allied(observerPlayerId, unit.Player);
    }

    public static Color GetColor(Unit unit, int observerPlayerId, Func<int, int, bool> allied,
        Color normalColor)
        => IsIdentified(unit, observerPlayerId, allied)
            ? new Color(20f / 255f, 96f / 255f, 1f, normalColor.A)
            : normalColor;
}
