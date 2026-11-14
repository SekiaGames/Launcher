using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 大气雾总闸：w3i 解析出来的雾只存这一份，材质创建时和场景光照刷新时各写一次。
/// 客户端是每个世界渲染器各自把 uFogColor / uFogParams 传进着色器，这里用同一个值统一写，
/// 免得地形、悬崖、水面、模型各拿一份参数走散。
/// </summary>
public static class SceneFog
{
    public static War3FogConfig Config { get; private set; } = War3FogConfig.None;

    private static readonly HashSet<ShaderMaterial> _fogOfWarReceivers = new();
    private static Texture2D _fogOfWar;
    private static Vector4 _fogOfWarUv;
    private static bool _fogOfWarEnabled;

    public static void Set(War3FogConfig config)
    {
        Config = config;
        _fogOfWarReceivers.Clear();
        _fogOfWar = null;
        _fogOfWarUv = Vector4.Zero;
        _fogOfWarEnabled = false;
    }

    public static void SetFogOfWar(Texture2D texture, Vector4 uv, bool enabled)
    {
        bool isEnabled = enabled && texture != null;
        if (_fogOfWar == texture && _fogOfWarUv == uv && _fogOfWarEnabled == isEnabled) return;
        _fogOfWar = texture;
        _fogOfWarUv = uv;
        _fogOfWarEnabled = isEnabled;
        foreach (ShaderMaterial material in _fogOfWarReceivers)
        {
            WriteFogOfWar(material);
        }
    }

    public static void RegisterFogReceiver(ShaderMaterial material)
    {
        if (material == null) return;
        if (!_fogOfWarReceivers.Add(material)) return;
        WriteFogOfWar(material);
    }

    public static void Write(ShaderMaterial material)
    {
        if (material?.Shader == null) return;
        War3FogConfig config = Config;
        material.SetShaderParameter("fog_color",
            new Vector4(config.Tint.R, config.Tint.G, config.Tint.B, 1f));
        material.SetShaderParameter("fog_params", config.Params);
        RegisterFogReceiver(material);
    }

    private static void WriteFogOfWar(ShaderMaterial material)
    {
        material.SetShaderParameter("fog_of_war_uvp", _fogOfWarUv);
        material.SetShaderParameter("fog_of_war_enable", _fogOfWarEnabled ? 1f : 0f);
        if (_fogOfWar == null) return;
        material.SetShaderParameter("fog_of_war_texture", _fogOfWar);
    }
}
