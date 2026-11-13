using System.Numerics;

namespace OpenWC3.UI;

/// <summary>
/// 可竖直滚动区域: 让"在区域内按下并上下滑动"变成滚动, 而不是点击。
///
/// 原版 Windows 端用鼠标滚轮翻列表, 拇指不需要滑动; 安卓端没有滚轮,
/// 原版等价行为是手指在列表上上下滑。因此这里把两者收敛到同一个接口:
/// 上层只管把"滚动量"喂进来, 具体是滚轮还是触摸滑动由 <see cref="UiEventRouter"/>
/// 决定, 帧本身不关心输入设备。
/// </summary>
public interface IScrollTarget
{
    /// <summary>区域当前是否可滚动 (内容装得下时为 false)</summary>
    bool CanScroll { get; }
    /// <summary>累计滚动量 (设计单位)。正数 = 内容向下滚 = 看后面的内容</summary>
    void ScrollBy(float delta);
    /// <summary>把累计滚动量钳回合法范围, 由帧在布局前调用</summary>
    void ClampScroll();
    /// <summary>手势结束 (手指抬起 / 滚轮停止), 用于清除惯性或高亮</summary>
    void EndScrollGesture() { }
}

/// <summary>UiFrame 上的滚动目标查询扩展</summary>
public static class UiFrameScrollExtensions
{
    /// <summary>沿 Parent 链向上找最近的滚动目标 (子帧按下也应能带动所属列表)</summary>
    public static IScrollTarget FindScrollTarget(this UiFrame frame)
    {
        for (var f = frame; f != null; f = f.Parent)
        {
            if (f is IScrollTarget target) return target;
            if (f.ScrollTarget != null) return f.ScrollTarget;
        }
        return null;
    }
}
