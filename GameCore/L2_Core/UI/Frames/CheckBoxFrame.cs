using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

/// <summary>
/// 复选框: 对齐原版 FDF <c>Frame "GLUECHECKBOX"</c>。
///
/// 原版 <c>EscMenuCheckBoxTemplate</c> (EscMenuTemplates.fdf:334) 由四态背板
/// (Normal / Disabled / Pushed) 加一个勾选高亮组成:
///   CheckBoxCheckHighlight "EscMenuCheckHighlightTemplate"
///     HighlightAlphaFile "EscMenuCheckBoxCheckHighlight"  HighlightAlphaMode "BLEND"
///
/// 勾号本身是一张 BLEND 贴图, 勾中时叠在背板上 (不是自绘矢量)。因此这里只是
/// 给 <see cref="ButtonFrame"/> 补一个"勾中"显隐位和勾号贴图槽位。
/// </summary>
public class CheckBoxFrame : ButtonFrame
{
    /// <summary>EscMenuCheckBoxCheckHighlight 的贴图; Checked 时以 BLEND 叠加</summary>
    public ITexture CheckHighlightTexture;
    /// <summary>
    /// 屏幕空间保持正方形: 官方单选钮/勾选框在 4:3 画布上是方形的,
    /// 拉到 16:9 时按 <see cref="IUiRenderDevice.SquareWidthRatio"/> 收窄绘制宽度。
    /// </summary>
    public bool KeepSquare;
    public bool Checked { get => _checked; set { if (_checked == value) return; _checked = value; } }
    bool _checked;

    public CheckBoxFrame(string name = null) : base(name) { }

    protected override UiRect VisualBounds(IUiRenderDevice device)
    {
        if (!KeepSquare) return RenderBounds;
        float ratio = device.SquareWidthRatio;
        if (!(ratio > 0f)) return RenderBounds;
        float width = MathF.Min(RenderBounds.Width, RenderBounds.Height * ratio);
        return new UiRect(RenderBounds.X + (RenderBounds.Width - width) * .5f,
            RenderBounds.Y, width, RenderBounds.Height);
    }

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        base.Render(device);
        if (!_checked || CheckHighlightTexture == null) return;
        // 原版 HighlightAlphaMode "BLEND": 勾号按贴图 alpha 覆盖
        device.DrawTexturedQuad(VisualBounds(device), CheckHighlightTexture, Vector4.One);
    }
}
