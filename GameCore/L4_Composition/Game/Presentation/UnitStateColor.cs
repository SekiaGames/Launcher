using Godot;

namespace OpenWC3.Game;

internal static class UnitStateColor
{
    private static readonly Color EtherealTint = new(.75f, 1f, .5f, .5f);
    private const float InvisibleAlpha = 127f / 255f;

    internal static Color Get(Unit unit)
    {
        Vector4 rgba = unit.VertexColorRgba;
        var color = new Color(rgba.X, rgba.Y, rgba.Z, rgba.W);
        if (unit.Ethereal)
        {
            color = new Color(color.R * EtherealTint.R, color.G * EtherealTint.G,
                color.B * EtherealTint.B, color.A * EtherealTint.A);
        }
        if (unit.Invisible && unit.Alive) color.A = Mathf.Min(color.A, InvisibleAlpha);
        return color;
    }
}
