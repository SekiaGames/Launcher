using System.Numerics;

namespace OpenWC3.Game;

/// <summary>
/// Warcraft III 的 FDF 画布是 0.8 x 0.6，这里用等价的 1024 x 768 设计单位表达。
/// UI 设计矩形恒为 1024 x 768，并与 4:3 世界画面一起非等比拉伸；物理视口
/// 超过 16:9 时 UI 仍只使用居中的 16:9 区域，世界可以继续向两侧扩展。
/// </summary>
public sealed class War3UiViewport
{
    public const float BaseWidth = 1024f;
    public const float BaseHeight = 768f;

    public int FramebufferWidth { get; private set; }
    public int FramebufferHeight { get; private set; }
    public float ScaleX { get; private set; } = 1f;
    public float ScaleY { get; private set; } = 1f;
    public int PixelOffsetX { get; private set; }
    public int ActivePixelWidth { get; private set; } = 1;
    public float Width => BaseWidth;
    public float Height => BaseHeight;

    internal static int FitPixelWidth(int width, int height) => Math.Min(Math.Max(1, width),
        Math.Max(1, (int)MathF.Round(Math.Max(1, height) * (16f / 9f))));

    public void Update(int framebufferWidth, int framebufferHeight)
    {
        FramebufferWidth = Math.Max(1, framebufferWidth);
        FramebufferHeight = Math.Max(1, framebufferHeight);
        ActivePixelWidth = FitPixelWidth(FramebufferWidth, FramebufferHeight);
        PixelOffsetX = (FramebufferWidth - ActivePixelWidth) / 2;
        ScaleX = MathF.Max(.0001f, ActivePixelWidth / BaseWidth);
        ScaleY = MathF.Max(.0001f, FramebufferHeight / BaseHeight);
    }

    // 窗口像素 -> 设计坐标: 对齐 open-realm ui_main.c 的 fdf = px / window.w * 0.8
    public Vector2 WindowToUi(Vector2 point, int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0) return Vector2.Zero;
        float framebufferX = point.X / windowWidth * FramebufferWidth;
        return new Vector2((framebufferX - PixelOffsetX) / ActivePixelWidth * BaseWidth,
            point.Y / windowHeight * BaseHeight);
    }

    public Vector2 UiToWindow(Vector2 point, int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0) return Vector2.Zero;
        return new Vector2((PixelOffsetX + point.X / BaseWidth * ActivePixelWidth)
            / FramebufferWidth * windowWidth, point.Y / BaseHeight * windowHeight);
    }

    // 设计矩形 -> 帧缓冲像素矩形。GL 的 Y 自下向上, 故返回的 Y 是矩形底边到画面底部的距离。
    // 供 scissor 与 3D 头像子视口共用, 避免各处重复写这套换算和翻转。
    public (int X, int Y, int Width, int Height) ToPixels(float x, float y, float width, float height)
    {
        int px = PixelOffsetX + (int)MathF.Round(x * ScaleX);
        int pw = (int)MathF.Round(width * ScaleX);
        int ph = (int)MathF.Round(height * ScaleY);
        int top = (int)MathF.Round(y * ScaleY);
        return (px, Math.Max(0, FramebufferHeight - top - ph), pw, ph);
    }
}
