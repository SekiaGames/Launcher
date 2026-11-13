using System.Numerics;

namespace OpenWC3.UI;

// 事件路由: 对齐 Warsmash MeleeUI 的 mouseDownUIFrame 机制
// 根逆序命中 + 按下记录 + 松开同帧才 OnClick (防拖拽误触)
//
// 额外接管两类原版行为:
//  1) 滚轮: Wheel() 沿命中帧的滚动目标链分发 (Windows 端翻列表)
//  2) 竖直滑动: 位移超阈值后本次手势转为滚动, 不再触发点击/长按
//     (安卓端替代滚轮; Windows 端按住拖小地图等既有 IDraggable 行为不变)
public sealed class UiEventRouter
{
    readonly UiRoot _root;
    UiFrame _mouseDownFrame;
    IScrollTarget _scrollTarget;
    Vector2 _lastPointer;
    float _accumulatedY;
    long _mouseDownTimestamp;
    int _mouseDownButton;
    bool _longPressTriggered;
    bool _scrolling;
    public UiFrame Hovered { get; private set; }

    public event Action<UiFrame, UiCommand, int> CommandClicked;
    public event Action<UiFrame, UiCommand> CommandRightClicked;
    public event Action<UiFrame, UiCommand, int> CommandLongPressed;
    public event Action<UiFrame, UiCommand, Vector2> CommandDragMoved;
    public event Action<UiFrame, UiCommand, Vector2> CommandDragReleased;
    public event Action<UiFrame> HoverChanged;

    const int LeftButton = 0;  // Silk.NET MouseButton.Left
    // 竖直滑动转滚动阈值 (设计单位): 1280 宽的 0.8 空间里约 0.6% 视宽
    const float ScrollGestureThreshold = 8f;

    public UiEventRouter(UiRoot root) => _root = root;

    public void PointerDown(Vector2 pos, int button)
    {
        _root.Position();
        _mouseDownFrame = _root.HitTest(pos);
        _scrollTarget = null;
        _scrolling = false;
        _accumulatedY = 0;
        _lastPointer = pos;
        _mouseDownTimestamp=System.Diagnostics.Stopwatch.GetTimestamp();
        _mouseDownButton=button;
        _longPressTriggered=false;
        if (_mouseDownFrame is IClickable c) c.OnPointerDown(pos);
    }

    public void PointerUp(Vector2 pos, int button)
    {
        _root.Position();
        // 滑动已识别为滚动手势: 只收尾, 不产生点击
        if (_scrolling)
        {
            _scrollTarget?.EndScrollGesture();
            if (_mouseDownFrame is IClickable scrolled) scrolled.OnPointerUp(pos);
            Reset();
            return;
        }
        var up = _root.HitTest(pos);
        // Autocast switching remains available while the cast button is disabled.
        if(button==1&&_mouseDownButton==button&&up==_mouseDownFrame
            &&up is ButtonFrame rightButton&&rightButton.Command.HasValue)
        {
            rightButton.OnPointerUp(pos);
            CommandRightClicked?.Invoke(rightButton,rightButton.Command.Value);
        }
        else if(_longPressTriggered&&_mouseDownFrame is ButtonFrame dragged
            &&dragged.Command.HasValue)
        {
            if(_mouseDownFrame is IClickable dragReleased)dragReleased.OnPointerUp(pos);
            CommandDragReleased?.Invoke(dragged,dragged.Command.Value,pos);
        }
        else if (button == LeftButton && _mouseDownButton==button && _mouseDownFrame != null && up == _mouseDownFrame
            && up is IClickable clickable)
        {
            double held=(System.Diagnostics.Stopwatch.GetTimestamp()-_mouseDownTimestamp)
                /(double)System.Diagnostics.Stopwatch.Frequency;
            if (clickable is ButtonFrame btn && btn.Command.HasValue)
            {
                // 自动施法开关属于能力状态，不应因当前缺蓝或冷却而无法长按切换。
                if(held>=.45)
                {
                    clickable.OnPointerUp(pos);
                    _longPressTriggered=true;
                    CommandLongPressed?.Invoke(btn,btn.Command.Value,button);
                    CommandDragReleased?.Invoke(btn,btn.Command.Value,pos);
                }
                else if(clickable.Enabled)
                {
                    clickable.OnClick(button);
                    CommandClicked?.Invoke(btn,btn.Command.Value,button);
                }
                else clickable.OnPointerUp(pos);
            }
            else if(clickable.Enabled)clickable.OnClick(button);
            else clickable.OnPointerUp(pos);
        }
        else if (_mouseDownFrame is IClickable released) released.OnPointerUp(pos);
        Reset();
    }

    void Reset()
    {
        _mouseDownFrame = null;
        _scrollTarget = null;
        _scrolling = false;
        _accumulatedY = 0;
        _longPressTriggered = false;
    }

    /// <summary>
    /// 立即清除悬停态 (清掉上一个悬停按钮的 <see cref="ButtonFrame.IsHovered"/> 与它的高亮)。
    ///
    /// <see cref="PointerMove"/> 的增量 diff 只在"指针真的移动了"时才刷新悬停; 触屏没有
    /// 常驻指针 —— 抬手之后不会再产生任何移动事件, 于是 <see cref="Hovered"/> 会永远停在
    /// 刚才被按住的那个按钮上, 高亮(<c>ActiveHighlight</c>)和悬浮提示都一直挂着
    /// (实机报告: 点一下空闲农民条, 相机跳走后图标永远亮着)。
    /// 窗口失焦、HUD 整层隐藏时同样应该调用。
    /// </summary>
    public void ClearHover()
    {
        if (Hovered is ButtonFrame previousButton) previousButton.IsHovered = false;
        Hovered = null;
        // 让悬浮提示也跟着收起来 (MeleeHudController 的 HoverChanged 处理器对 null 走 else 分支)
        HoverChanged?.Invoke(null);
    }

    /// <summary>
    /// 鼠标滚轮: 原版语义是"指针悬停在可滚动区域上时翻该区域, 否则缩放镜头",
    /// 所以返回是否被 UI 消费, 由上层决定要不要继续缩放。
    /// </summary>
    public bool Wheel(Vector2 pos, float delta)
    {
        if (_mouseDownFrame != null) return false;
        _root.Position();
        var target = _root.HitTest(pos)?.FindScrollTarget();
        if (target is not { CanScroll: true }) return false;
        target.ScrollBy(delta);
        return true;
    }

    public void PointerMove(Vector2 pos)
    {
        _root.Position();
        // 竖直滑动优先: 命中链上有可滚动目标时, 累计竖直位移超阈值即转滚动手势,
        // 并屏蔽本次点击与长按 (安卓端靠这个区分"点条目"和"翻列表")
        if (_mouseDownFrame != null)
        {
            float dy = pos.Y - _lastPointer.Y;
            if (_scrolling)
            {
                _scrollTarget?.ScrollBy(dy);
            }
            else if (!_longPressTriggered)
            {
                _accumulatedY += dy;
                if (MathF.Abs(_accumulatedY) > ScrollGestureThreshold)
                {
                    var target = _mouseDownFrame.FindScrollTarget();
                    if (target is { CanScroll: true })
                    {
                        _scrolling = true;
                        _scrollTarget = target;
                        target.ScrollBy(_accumulatedY);
                        _accumulatedY = 0;
                        if (_mouseDownFrame is IClickable released) released.OnPointerUp(pos);
                    }
                    else _scrollTarget = null;
                }
            }
        }
        _lastPointer = pos;
        // 按住不放时把移动持续喂给按下的那一帧, 即使指针已经移出它的矩形
        if (!_scrolling && _mouseDownFrame is IDraggable dragging) dragging.OnPointerDrag(pos);
        if(_mouseDownButton==LeftButton&&!_longPressTriggered&&!_scrolling&&_mouseDownFrame is ButtonFrame pressed
            &&pressed.Command.HasValue)
        {
            double held=(System.Diagnostics.Stopwatch.GetTimestamp()-_mouseDownTimestamp)
                /(double)System.Diagnostics.Stopwatch.Frequency;
            // Inventory items and autocast buttons both require a deliberate
            // long press. Merely sliding a finger across a slot must not pick
            // the item up while the player is scrolling/repositioning a touch.
            if(held>=.45)
            {
                _longPressTriggered=true;
                CommandLongPressed?.Invoke(pressed,pressed.Command.Value,LeftButton);
            }
        }
        if(_longPressTriggered&&_mouseDownFrame is ButtonFrame dragButton
            &&dragButton.Command.HasValue)
            CommandDragMoved?.Invoke(dragButton,dragButton.Command.Value,pos);
        var hit = _root.HitTest(pos);
        if (hit != Hovered)
        {
            if (Hovered is ButtonFrame previousButton) previousButton.IsHovered = false;
            Hovered = hit;
            if (Hovered is ButtonFrame currentButton) currentButton.IsHovered = true;
            HoverChanged?.Invoke(hit);
        }
    }
}
