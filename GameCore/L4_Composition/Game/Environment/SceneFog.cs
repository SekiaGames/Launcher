using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 大气雾总闸：w3i 解析出来的雾只存这一份，材质创建时和场景光照刷新时各写一次。
/// 客户端是每个世界渲染器各自把 uFogColor / uFogParams 传进着色器，这里用同一个值统一写，
/// 免得地形、悬崖、水面、模型各拿一份参数走散。
/// </summary>
public static class SceneFog
{
    public static War3FogConfig Config { get; private set; } = War3FogConfig.None;

    public static void Set(War3FogConfig config)
    {
        Config = config;
    }

    public static void Write(ShaderMaterial material)
    {
        if (material?.Shader == null) return;
        material.SetShaderParameter("fog_color",
            new Vector4(Config.Tint.R, Config.Tint.G, Config.Tint.B, 1f));
        material.SetShaderParameter("fog_params", Config.Params);
    }
}
