namespace OpenWC3.UI;

// UI 根: 管理器即根 Frame (对齐 Warsmash GameUI), 渲染前统一求解布局
public sealed class UiRoot : UiFrame
{
    public UiRoot(string name = "Root") : base(name) { }

    // 根 Frame 填满视口; 尺寸没变就不重算整棵树的布局
    public void UpdateViewport(float width, float height)
    {
        if (RenderBounds.Width == width && RenderBounds.Height == height) return;
        RenderBounds = new UiRect(0, 0, width, height);
        InvalidateLayout();
    }

    public override void Render(IUiRenderDevice device)
    {
        Position();
        base.Render(device);
    }
}
