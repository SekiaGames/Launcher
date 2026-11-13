using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class SolidColorFrame : UiFrame
{
    readonly Vector4 _color;

    public SolidColorFrame(string name, Vector4 color) : base(name)
    {
        _color = color;
        MouseTransparent = true;
    }

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        device.DrawTexturedQuad(RenderBounds, device.WhiteTexture, _color);
        base.Render(device);
    }
}