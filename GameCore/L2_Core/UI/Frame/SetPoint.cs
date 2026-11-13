using System.Numerics;

namespace OpenWC3.UI;

// 锚点绑定: 本帧锚点 <-> 目标帧锚点 + 偏移 (对齐 FDF SetPoint)
// 偏移使用 0.8x0.6 比例坐标 (X 横向, Y 纵向)
public readonly record struct SetPoint(
    FramePoint Self,
    UiFrame Target,
    FramePoint TargetPoint,
    Vector2 Offset);
