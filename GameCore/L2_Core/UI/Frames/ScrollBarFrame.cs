using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

/// <summary>
/// 滚动条: 对齐原版 FDF <c>Frame "SCROLLBAR"</c> / <c>Frame "SLIDER"</c>。
///
/// 原版模板 <c>EscMenuScrollBarTemplate</c> (Static/MpqOut/War3Patch/UI/FrameDef/UI/EscMenuTemplates.fdf:211)
/// 结构固定为三件套:
///   Width 0.012 / SliderLayoutVertical      -> 竖向, 轨宽 0.012
///   ControlBackdrop "EscMenuScrollBarBackdrop"
///     BackdropBackground "EscMenuSliderBackground" / CornerSize 0.006
///     BackgroundSize 0.006 / Insets 0.0025 x4 / EdgeFile "EscMenuSliderBorder"
///   SliderThumbButtonFrame "EscMenuScrollThumbButton"
///     Width 0.016 Height 0.016 / Backdrop "EscMenuSliderThumbButton"
///
/// 原版滑块是真实 <c>BUTTON</c>, 拖动时整条轨道跟随, 因此这里把拇指做成
/// 独立可拖拽按钮, 而不是把滚动条整体做成可拖拽矩形。
/// 轨道点击 = 向上/向下翻一整页 (原版 ScrollBar 的 page-step 行为)。
/// </summary>
public class ScrollBarFrame : UiFrame, IScrollTarget
{
    public bool Vertical = true;
    /// <summary>拇指长度 (设计单位)。原版 EscMenuScrollBarTemplate 为 0.016。</summary>
    public float ThumbSize;
    /// <summary>可见比例 0..1; 1 时轨道不滚动, 拇指占满整条。</summary>
    public float PageSize = 1f;
    float _min, _max = 1f, _value;

    public event Action<float> ValueChanged;

    public float Min { get => _min; set { _min = value; ClampValue(); } }
    public float Max
    {
        get => _max;
        set { _max = value; ClampValue(); }
    }
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, _min, _max);
            if (Math.Abs(clamped - _value) < 1e-4f) return;
            _value = clamped;
            ValueChanged?.Invoke(_value);
        }
    }

    public ScrollBarFrame(string name = null) : base(name) { }

    /// <summary>可视窗口尺寸 / 总内容尺寸。&lt;=1 时视为无需滚动。</summary>
    public void SetPage(float visible, float total)
    {
        float ratio = total > 1e-4f ? Math.Clamp(visible / total, 0f, 1f) : 1f;
        if (Math.Abs(ratio - PageSize) > 1e-4f)
        {
            PageSize = ratio;
            ClampValue();
        }
    }

    void ClampValue()
    {
        float span = _max - _min;
        // 拇指占满时没有可滚动的余量, 但值域仍要合法, 否则上游按 0..1 归一化会得到 NaN
        _value = Math.Clamp(_value, _min, _max);
        if (span <= 1e-6f) return;
    }

    // 拇指子帧: 由 WireUp 或 FdfInflater (SliderThumbButtonFrame) 绑定
    public ButtonFrame Thumb { get; set; }
    /// <summary>轨道点击的翻页量 (设计单位), 由 VisibleExtent 推导</summary>
    float PageStep => Math.Max(1f, VisibleExtent() * (1f - PageSize));

    float VisibleExtent() => Vertical
        ? RenderBounds.Height
        : RenderBounds.Width;

    /// <summary>
    /// 绑定拇指按钮。由 <see cref="WireUp"/> 在首次布局前自动调用;
    /// 也允许外部先塞好 Thumb 再布局 (FdfInflator 的 SliderThumbButtonFrame 走这条路)。
    /// </summary>
    public void WireUp()
    {
        if (Thumb == null)
            Thumb = FindThumb(Children);
        if (Thumb is ScrollThumbButton st) st.Owner = this;
    }

    internal static ButtonFrame FindThumb(List<UiFrame> children)
    {
        foreach (var child in children)
        {
            if (child is ButtonFrame button) return button;
            var nested = FindThumb(child.Children);
            if (nested != null) return nested;
        }
        return null;
    }

    public override void Position()
    {
        WireUp();
        base.Position();
        LayoutThumb();
    }

    void LayoutThumb()
    {
        if (Thumb == null) return;
        var b = RenderBounds;
        float extent = VisibleExtent();
        if (extent <= 0)
        {
            Thumb.Visible = false;
            return;
        }
        // 原版: 内容装得下时滚动条不显示 (thumb 占满 == 禁用态)
        float size = ThumbSize > 0 ? ThumbSize : (Vertical ? Thumb.Width ?? 0 : Thumb.Height ?? 0);
        if (size <= 0) size = FdfScale.S(0.016f);
        float page = Math.Clamp(PageSize, 0f, 1f);
        float thumbLength = Math.Min(size, extent);
        bool scrollable = page < 0.999f && _max > _min;
        Thumb.Visible = scrollable;
        if (!scrollable) return;

        float span = _max - _min;
        float t = span > 1e-6f ? (_value - _min) / span : 0f;
        float travel = extent - thumbLength;
        if (Vertical)
        {
            // 设计空间 +Y 朝下: 顶部 t=0, 底部 t=1
            float y = b.Y + travel * t;
            Thumb.RenderBounds = new UiRect(b.X, y, b.Width, size);
        }
        else
        {
            float x = b.X + travel * t;
            Thumb.RenderBounds = new UiRect(x, b.Y, size, b.Height);
        }
        Thumb.SetPoints.Clear();
        Thumb.InvalidateLayout();
        Thumb.Position();
    }

    /// <summary>拇指按下: 交给 Thumb 自身处理 (记录抓取偏移)</summary>
    internal void OnThumbDown(Vector2 pos)
    {
        if (Thumb == null) return;
        if (Vertical) _grabOffset = pos.Y - Thumb.RenderBounds.Y;
        else _grabOffset = pos.X - Thumb.RenderBounds.X;
    }

    float _grabOffset;

    internal void OnThumbDrag(Vector2 pos)
    {
        if (Thumb == null) return;
        var b = RenderBounds;
        float extent = VisibleExtent();
        float size = Thumb.RenderBounds.Height;
        float travel = extent - size;
        if (travel <= 1e-3f) return;
        float t;
        if (Vertical) t = (pos.Y - _grabOffset - b.Y) / travel;
        else t = (pos.X - _grabOffset - b.X) / travel;
        float span = _max - _min;
        Value = _min + Math.Clamp(t, 0f, 1f) * span;
    }

    /// <summary>轨道点击: 朝点击方向翻一页</summary>
    public void PageBy(bool forward)
    {
        Value = _value + (forward ? 1 : -1) * PageStep;
    }

    /// <summary>滚轮 / 触摸滑动累积量 (设计单位, 向下为正)</summary>
    public void ScrollBy(float delta)
    {
        if (Thumb == null || !Thumb.Visible) return;
        Value = _value + delta;
    }

    // IScrollTarget: 指针悬停在本滚动条上时, 滚轮/滑动直接驱动它
    bool IScrollTarget.CanScroll => Thumb != null && Thumb.Visible && _max > _min;
    void IScrollTarget.ScrollBy(float delta) => ScrollBy(delta);
    void IScrollTarget.ClampScroll() => ClampValue();

    /// <summary>当前归一化位置 0..1, 供外部把滚动量换算成内容偏移</summary>
    public float Normalized => _max > _min ? Math.Clamp((_value - _min) / (_max - _min), 0f, 1f) : 0f;
}

/// <summary>
/// 滚动条拇指: 原版的 <c>SliderThumbButtonFrame</c> 是真按钮, 按下后即使指针
/// 移出拇指也要继续跟随, 所以额外实现 <see cref="IDraggable"/>。
/// </summary>
public sealed class ScrollThumbButton : ButtonFrame, IClickable, IDraggable
{
    internal ScrollBarFrame Owner;

    public ScrollThumbButton(string name = null) : base(name) { }

    // 必须把 IClickable 再列一次并显式实现: ButtonFrame 的隐式实现会继续占用
    // 接口映射, 只写 IClickable.OnPointerDown 而不重新声明接口是编译错误;
    // 用 new 隐藏则路由器拿不到"抓取偏移"这一步。
    void IClickable.OnPointerDown(Vector2 pos)
    {
        base.OnPointerDown(pos);
        Owner?.OnThumbDown(pos);
    }

    public void OnPointerDrag(Vector2 pos) => Owner?.OnThumbDrag(pos);
}
