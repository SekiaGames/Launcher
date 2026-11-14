using Godot;

namespace OpenWC3.Game;

/// <summary>ReplaceableTextures/TeamColor/TeamColor00..15：每个是一张纯色贴图，直接替换材质里的队伍色纹理。</summary>
public static class TeamColorTextures
{
    public static Texture2D Get(int index)
    {
        int slot = Mathf.Clamp(index, 0, 15);
        return _textures[slot] ??= Assets.LoadTexture(
            Assets.Res($"ReplaceableTextures/TeamColor/TeamColor{slot:00}.png"));
    }

    private static readonly Texture2D[] _textures = new Texture2D[16];
}
