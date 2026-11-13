using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

// War3 九宫格背板。边纹理是横向 8 等分图集：L,R,T,B,UL,UR,BL,BR。
// 对齐 Warsmash BackdropFrame：边以 CornerSize 为固定单元逐格绘制，末格裁 UV，不能整边缩放。
public class BackdropFrame : UiFrame
{
    public ITexture BackgroundTexture;  // 中心背景
    public ITexture EdgeTexture;        // 边纹理 (8 等分竖条图集)
    public float CornerSize = 12f;      // 角/边单元尺寸 (设计单位)
    public float BackgroundSize;        // BackdropBackgroundSize: 平铺单元尺寸 (设计单位, 0=用纹理尺寸)
    public UiRect Insets;               // 背景内缩 (Left, Top, Right, Bottom)
    public Vector4 Color = Vector4.One;
    public bool TileBackground;
    public bool Mirrored;               // BackdropMirrored: 背景水平镜像
    public HashSet<string> CornerFlags; // null=全画, 否则只画含的 (UL/UR/BL/BR/T/L/B/R)

    public BackdropFrame(string name = null) : base(name) { }

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        var b = RenderBounds;
        if (BackgroundTexture != null) DrawBackground(device, b);
        if (EdgeTexture != null) DrawEdges(device, b);
        base.Render(device);
    }

    void DrawBackground(IUiRenderDevice device, UiRect b)
    {
        var inner = new UiRect(b.X + Insets.X, b.Y + Insets.Y,
            Math.Max(0, b.Width - Insets.X - Insets.Width),
            Math.Max(0, b.Height - Insets.Y - Insets.Height));
        if (inner.Width <= 0 || inner.Height <= 0) return;
        if (TileBackground && BackgroundSize > 0)
        {
            // Warsmash 会从 BackgroundSize 中扣除两轴 Insets 的平均总量。
            float unit = BackgroundSize
                - (Insets.X + Insets.Y + Insets.Width + Insets.Height) * .5f;
            if (unit > 0)
            {
                for (float y = 0; y < inner.Height; y += unit)
                {
                    float tileHeight = MathF.Min(unit, inner.Height - y);
                    float v = tileHeight / unit;
                    for (float x = 0; x < inner.Width; x += unit)
                    {
                        float tileWidth = MathF.Min(unit, inner.Width - x);
                        float u = tileWidth / unit;
                        var uv = Mirrored
                            ? new UiRect(u, 0, -u, v)
                            : new UiRect(0, 0, u, v);
                        device.DrawTexturedQuad(new UiRect(inner.X + x, inner.Y + y,
                            tileWidth, tileHeight), BackgroundTexture, Color, uv);
                    }
                }
                return;
            }
        }
        device.DrawTexturedQuad(inner, BackgroundTexture, Color,
            Mirrored ? new UiRect(1, 0, -1, 1) : new UiRect(0, 0, 1, 1));
    }

    void DrawEdges(IUiRenderDevice device, UiRect b)
    {
        float cs = CornerSize;
        if (cs <= 0) return;
        cs = MathF.Min(cs, MathF.Min(b.Width, b.Height) * 0.5f);

        bool ul = Has("UL"), ur = Has("UR"), bl = Has("BL"), br = Has("BR");
        DrawCorner(device, b.X, b.Y, cs, 4, ul);
        DrawCorner(device, b.Right - cs, b.Y, cs, 5, ur);
        DrawCorner(device, b.X, b.Bottom - cs, cs, 6, bl);
        DrawCorner(device, b.Right - cs, b.Bottom - cs, cs, 7, br);

        float edgeX = b.X + (ul || bl ? cs : 0);
        float edgeWidth = MathF.Max(0, b.Width
            - (ul || bl ? cs : 0) - (ur || br ? cs : 0));
        float edgeY = b.Y + (ul || ur ? cs : 0);
        float edgeHeight = MathF.Max(0, b.Height
            - (ul || ur ? cs : 0) - (bl || br ? cs : 0));

        if (Has("L")) DrawStrip(device, new UiRect(b.X, edgeY, cs, edgeHeight), cs, 0, false);
        if (Has("R")) DrawStrip(device, new UiRect(b.Right - cs, edgeY, cs, edgeHeight), cs, 1, false);
        if (Has("T")) DrawStrip(device, new UiRect(edgeX, b.Y, edgeWidth, cs), cs, 2, true);
        if (Has("B")) DrawStrip(device, new UiRect(edgeX, b.Bottom - cs, edgeWidth, cs), cs, 3, true);
    }

    bool Has(string flag) => CornerFlags == null || CornerFlags.Contains(flag);

    void DrawCorner(IUiRenderDevice device, float x, float y, float size, int atlasIndex, bool visible)
    {
        if (!visible) return;
        const float k = 1f / 8f;
        device.DrawTexturedQuad(new UiRect(x, y, size, size), EdgeTexture, Color,
            new UiRect(atlasIndex * k, 0, k, 1));
    }

    void DrawStrip(IUiRenderDevice device, UiRect bounds, float unit, int atlasIndex, bool horizontal)
    {
        float length = horizontal ? bounds.Width : bounds.Height;
        if (length <= 0) return;
        const float k = 1f / 8f;
        for (float offset = 0; offset < length; offset += unit)
        {
            float extent = MathF.Min(unit, length - offset);
            float ratio = extent / unit;
            var rect = horizontal
                ? new UiRect(bounds.X + offset, bounds.Y, extent, bounds.Height)
                : new UiRect(bounds.X, bounds.Y + offset, bounds.Width, extent);
            device.DrawTexturedQuadTiled(rect, EdgeTexture, Color,
                new UiRect(atlasIndex * k, 0, k, ratio), horizontal);
        }
    }
}
