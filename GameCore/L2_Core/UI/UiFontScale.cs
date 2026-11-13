namespace OpenWC3.UI;

/// <summary>
/// 全局 UI 字号倍率 (操作设置 → UI字体大小, 1.0~2.0)。
/// 只作用于游戏内 FDF 帧文本 (StringFrame / TextAreaFrame) 及依赖字号的排版;
/// ImGui 面板与 Android 按键名称走各自的文本通道, 不受影响。
/// ★ 默认值见 <see cref="Default"/>(2026-11-08 起 1.2)。倍率恰好为 1 时
///   <see cref="Apply"/> 原样返回入参, 玩家手动调回 1.0 仍与控制台逐像素一致。
/// </summary>
public static class UiFontScale
{
    public const float Min = 1f;
    public const float Max = 2f;

    /// <summary>出厂默认倍率。★ 唯一来源, 设置/面板/场景兜底都读这里。</summary>
    public const float Default = 1.2f;

    public static float Factor { get; private set; } = Default;

    public static void Set(float value)
        => Factor = float.IsFinite(value) ? Math.Clamp(value, Min, Max) : Default;

    /// <summary>按当前倍率放大; 倍率为 1 时原样返回 (显式 if, 不引入乘法误差)。</summary>
    public static float Apply(float value) => Factor == 1f ? value : value * Factor;
}
