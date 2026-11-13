using System.Numerics;

namespace OpenWC3.UI;

// UI 矩形: 坐标使用 0.8x0.6 FDF 比例坐标 (等价 1024x768 设计单位)
public readonly record struct UiRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool Contains(float x, float y) =>
        x >= X && x < Right && y >= Y && y < Bottom;
    public static UiRect FromBounds(float left, float top, float right, float bottom) =>
        new(left, top, right - left, bottom - top);
}
