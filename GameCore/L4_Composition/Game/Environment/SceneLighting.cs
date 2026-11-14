using Godot;

namespace OpenWC3.Game;

public readonly record struct SceneLight(
    Vector3 Direction,
    Vector3 DiffuseColor,
    float DiffuseIntensity,
    Vector3 AmbientColor,
    float AmbientIntensity)
{
    public static readonly SceneLight Fallback = new(
        new Vector3(-0.3f, 0.3f, 0.25f).Normalized(),
        Vector3.One, 1f, Vector3.One, 0.3f);
}
