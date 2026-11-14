using System;

namespace OpenWC3.Game;

internal static class UnitStatusBarGeometry
{
    internal static (float Width, float Height, float Inset) Pixels(float diameter, int width, int height)
        => (diameter * .0005f * War3UiViewport.FitPixelWidth(width, height) / .8f,
            MathF.Max(1, .004f * Math.Max(1, height) / .6f),
            .0006f * Math.Max(1, height) / .6f);

    internal static float ManaHeight(int height)
        => MathF.Max(1, .0028f * Math.Max(1, height) / .6f);
}
