using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

// 纹理 Frame: 绘制一张纹理 quad (可选 UV 裁剪/着色)
public class TextureFrame : UiFrame
{
    public ITexture Texture;
    public Vector4 Color = Vector4.One;
    public UiRect UV = new(0, 0, 1, 1);
    public bool RotatedClockwise;
    public bool Additive;
    /// <summary>
    /// 屏幕空间保持正方形: UI 画布 4:3 拉到 16:9 时把绘制宽度按
    /// <see cref="IUiRenderDevice.SquareWidthRatio"/> 收窄, 圆形贴图不会变椭圆。
    /// 命中/裁剪仍使用 RenderBounds。
    /// </summary>
    public bool KeepSquare;

    // 纹理帧是纯装饰, 不参与命中
    public TextureFrame(string name = null) : base(name) => MouseTransparent = true;

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        if (Texture != null)
        {
            UiRect rect = RenderBounds;
            if (KeepSquare && device.SquareWidthRatio > 0f && rect.Width > 0f && rect.Height > 0f)
            {
                float width = MathF.Min(rect.Width, rect.Height * device.SquareWidthRatio);
                rect = new UiRect(rect.X + (rect.Width - width) * .5f, rect.Y, width, rect.Height);
            }
            if (Additive) device.DrawTexturedQuadAdd(rect, Texture, Color, UV);
            else device.DrawTexturedQuad(rect, Texture, Color, UV);
        }
        base.Render(device);
    }
}
