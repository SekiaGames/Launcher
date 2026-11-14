using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 战争迷雾 uniform 的统一上传入口：只维护雾纹理、世界范围到 UV 的换算和开关。
/// 地形、悬崖、水面、模型在材质创建时调用 Register 挂接收者，FogOfWar 每次重算纹理后 Publish。
/// </summary>
public static class FogOfWarUniforms
{
    private static readonly HashSet<ShaderMaterial> Receivers = new();

    public static Texture2D Texture { get; private set; }

    public static Vector4 Uvp { get; private set; }

    public static bool Enabled { get; private set; }

    public static void Publish(Texture2D texture, Vector4 uvp, bool enabled)
    {
        bool isEnabled = enabled && texture != null;
        if (Texture == texture && Uvp == uvp && Enabled == isEnabled) return;
        Texture = texture;
        Uvp = uvp;
        Enabled = isEnabled;
        foreach (ShaderMaterial material in Receivers)
        {
            Upload(material);
        }
    }

    public static void Reset()
    {
        Receivers.Clear();
        Texture = null;
        Uvp = Vector4.Zero;
        Enabled = false;
    }

    public static void Register(ShaderMaterial material)
    {
        if (material == null) return;
        if (!Receivers.Add(material)) return;
        Upload(material);
    }

    public static void Upload(ShaderMaterial material)
    {
        if (material?.Shader == null) return;
        material.SetShaderParameter("fog_of_war_uvp", Uvp);
        material.SetShaderParameter("fog_of_war_enable", Enabled ? 1f : 0f);
        if (Texture == null) return;
        material.SetShaderParameter("fog_of_war_texture", Texture);
    }
}