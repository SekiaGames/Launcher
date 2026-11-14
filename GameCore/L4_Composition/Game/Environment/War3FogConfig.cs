using Godot;

namespace OpenWC3.Game;

public readonly record struct War3FogConfig(int Style, Color Tint, float Start, float End, float Density)
{
    public static readonly War3FogConfig None = new(0, new Color(0f, 0f, 0f, 1f), 0f, 0f, 0f);

    public bool Enabled => Style > 0;

    public Vector4 Params => new(Style, Start, End, Density);

    public static War3FogConfig FromEnvironment(W3iEnvironment environment)
    {
        if (environment == null || !environment.UseTerrainFog)
        {
            return None;
        }

        byte[] bgra = environment.FogColorBgra;
        if (bgra == null || bgra.Length < 4)
        {
            return None;
        }

        return new War3FogConfig(1,
            new Color(bgra[2] / 255f, bgra[1] / 255f, bgra[0] / 255f, bgra[3] / 255f),
            environment.FogHeightStart, environment.FogHeightEnd, environment.FogDensity);
    }

    public string Describe() => Enabled
        ? $"线性 {Start:0}/{End:0}，色 ({Tint.R:0.00},{Tint.G:0.00},{Tint.B:0.00})"
        : "关（w3i useTerrainFog=0）";
}
