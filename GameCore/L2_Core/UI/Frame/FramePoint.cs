namespace OpenWC3.UI;

// Frame 锚点: 对齐 FDF FramePoint (9 个基本点)
public enum FramePoint
{
    Center,
    TopLeft, Top, TopRight,
    Left, Right,
    BottomLeft, Bottom, BottomRight,
}

// 锚点工具
public static class FramePoints
{
    // 锚点在矩形上的归一化位置 (0..1)
    public static (float U, float V) AnchorOffset(FramePoint p) => p switch
    {
        FramePoint.Center => (0.5f, 0.5f),
        FramePoint.TopLeft => (0f, 0f),
        FramePoint.Top => (0.5f, 0f),
        FramePoint.TopRight => (1f, 0f),
        FramePoint.Left => (0f, 0.5f),
        FramePoint.Right => (1f, 0.5f),
        FramePoint.BottomLeft => (0f, 1f),
        FramePoint.Bottom => (0.5f, 1f),
        FramePoint.BottomRight => (1f, 1f),
        _ => (0.5f, 0.5f),
    };
}
