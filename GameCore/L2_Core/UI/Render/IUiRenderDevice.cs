using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

// UI 渲染设备: 高层 2D 绘图原语抽象, 不泄漏 GL
// 坐标使用 0.8x0.6 FDF 比例坐标 (UiRect), 由实现负责转换到帧缓冲
public interface IUiRenderDevice : IDisposable
{
    // scaleX/scaleY: 设计单位 -> 帧缓冲像素 (非等比, 对齐 open-realm 0.8x0.6 拉伸)
    void Begin(float viewportWidth, float viewportHeight, float scaleX, float scaleY);
    void End();

    /// <summary>
    /// 把正方形控件画成屏幕正方形时, 宽度要乘的比例 (= scaleY/scaleX)。
    /// 4:3 UI 画布被拉到 16:9 时该值 &lt; 1; 文字已有同样的 kx 修正,
    /// 圆形/方形控件(单选钮、滑条宝珠、勾选框)也应使用它避免变成椭圆。
    /// </summary>
    float SquareWidthRatio => 1f;

    void DrawTexturedQuad(UiRect rect, ITexture texture, Vector4 color, UiRect uv = default);
    void DrawTexturedQuadAdd(UiRect rect, ITexture texture, Vector4 color, UiRect uv = default);  // ADD blend 叠加 (原版 AlphaMode ADD 高亮)
    // tile=true 时纹理 wrap 切 Repeat (UV 可超出 0..1); transpose=true 把竖条 UV 旋转 90 度贴到横边
    void DrawTexturedQuadTiled(UiRect rect, ITexture texture, Vector4 color, UiRect uv, bool transpose = false);
    /// <summary>
    /// 按剩余比例 sweep(0..1)绘制命令按钮冷却模型，从 12 点开始顺时针消隐。
    /// 模型缺失时使用方形扫描；innerRatio 保留兼容调用，不挖空图标中心。
    /// </summary>
    void DrawCooldownSweep(UiRect rect, float sweep, float innerRatio, Vector4 color);
    double AnimationTimeSeconds { get; }
    /// <summary>播放冷却完成的 Death 动画；动画结束或资源不存在时返回 false。</summary>
    bool DrawCooldownCompletion(UiRect rect,float elapsedSeconds);
    bool DrawMinimapCamp(UiRect rect, bool tough, float elapsedSeconds, Vector4 color) => false;
    bool DrawAutoCast(UiRect rect,object button,float elapsedSeconds) => false;
    void DrawText(UiRect rect, string text, float fontSize, Vector4 color, TextAlignment align,
        Vector4? shadowColor = null, Vector2 shadowOffset = default,
        TextVerticalAlignment verticalAlign = TextVerticalAlignment.Top);
    float MeasureTextWidth(string text, float fontSize) =>
        UiText.Strip(text).Sum(c => c > 0x2e80 ? fontSize : fontSize * .55f);
    void DrawTextLine(UiRect rect, string text, float lineHeight, Vector4 color, TextAlignment align,
        Vector4? shadowColor = null, Vector2 shadowOffset = default,
        TextVerticalAlignment verticalAlign = TextVerticalAlignment.Top) =>
        DrawText(rect, text, lineHeight, color, align, shadowColor, shadowOffset, verticalAlign);
    float MeasureTextLineWidth(string text, float lineHeight) => MeasureTextWidth(text, lineHeight);
    int TextMeasurementRevision => 0;
    void PushScissor(UiRect rect);
    void PopScissor();

    // 1x1 白色纹理 (用于纯色矩形)
    ITexture WhiteTexture { get; }
    // 中心不透明、边缘渐隐的白色圆形纹理 (用于小地图英雄等柔边标记)
    ITexture RadialCircleTexture { get; }
}

public enum TextAlignment { Left, Center, Right }

public enum TextVerticalAlignment { Top, Middle, Bottom }
