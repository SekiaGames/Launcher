using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

// 状态条 Frame: 对齐 Warsmash SimpleStatusBarFrame (HP/MP/经验条)
// 填充同时按 ratio 裁 UV 与宽度; BorderBelow 决定边框叠放次序, BarInset 为填充内缩
public class StatusBarFrame : UiFrame
{
    public float Ratio = 1f;            // 0..1
    public Vector4 FillColor = new(0.2f, 1f, 0.2f, 1f);
    public Vector4 BackgroundColor = new(0, 0, 0, 0.6f);
    public bool Vertical;
    public ITexture FillTexture;        // 填充纹理 (可选, 替代纯色)
    public ITexture BorderTexture;      // 边框纹理 (可选)
    public bool BorderBelow;            // true = 边框画在填充之下
    public float BarInset;              // 填充相对整体的内缩 (设计单位)

    public StatusBarFrame(string name = null) : base(name) { }

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        var b = RenderBounds;
        if (BackgroundColor.W > 0)
            device.DrawTexturedQuad(b, device.WhiteTexture, BackgroundColor);
        if (BorderBelow && BorderTexture != null)
            device.DrawTexturedQuad(b, BorderTexture, Vector4.One, default);
        DrawFill(device, b);
        if (!BorderBelow && BorderTexture != null)
            device.DrawTexturedQuad(b, BorderTexture, Vector4.One, default);
        base.Render(device);
    }

    void DrawFill(IUiRenderDevice device, UiRect b)
    {
        float ratio = Math.Clamp(Ratio, 0f, 1f);
        if (ratio <= 0) return;
        var track = new UiRect(b.X + BarInset, b.Y + BarInset,
            Math.Max(0, b.Width - BarInset * 2), Math.Max(0, b.Height - BarInset * 2));
        var fill = Vertical
            ? new UiRect(track.X, track.Bottom - track.Height * ratio, track.Width, track.Height * ratio)
            : new UiRect(track.X, track.Y, track.Width * ratio, track.Height);
        if (FillTexture != null)
        {
            // 填充纹理由 FillColor 调制 (对齐 warsmash barFrame.setColor), 不是原色直贴
            var uv = Vertical ? new UiRect(0, 1 - ratio, 1, ratio) : new UiRect(0, 0, ratio, 1);
            device.DrawTexturedQuad(fill, FillTexture, FillColor, uv);
        }
        else
        {
            device.DrawTexturedQuad(fill, device.WhiteTexture, FillColor);
        }
    }
}
