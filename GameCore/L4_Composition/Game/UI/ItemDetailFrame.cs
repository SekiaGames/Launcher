using System;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

/// <summary>
/// 物品详情面板：选中地面物品后显示 图标 + 名称 + 说明 + 次数。
/// 与单位信息面板互斥（同一块位置，单位选中时收起）。
/// </summary>
internal sealed class ItemDetailFrame : UiContainer
{
    private const float PanelWidth = 230.4f;
    private const float IconSize = 48f;
    private const float RowHeight = 15f;
    private const float Padding = 10f;
    private const float PanelMinHeight = 84f;
    private const float NameFont = 16.64f;
    private const float BodyFont = 10.64f;

    private readonly TextureFrame _icon;
    private readonly StringFrame _name;
    private readonly StringFrame _description;
    private readonly StringFrame _charges;
    private readonly Func<string, ITexture> _iconResolver;
    private float _height = PanelMinHeight;

    public ItemDetailFrame(Func<string, ITexture> iconResolver) : base("ItemDetailPanel")
    {
        _iconResolver = iconResolver;
        var shadow = new Vector4(0, 0, 0, 0.9f);
        _icon = new TextureFrame("ItemDetailIcon");
        _name = new StringFrame("ItemDetailName")
        {
            FontSize = NameFont, Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            FontShadowColor = shadow, FontShadowOffset = new Vector2(2.56f, 2.56f),
        };
        _description = new StringFrame("ItemDetailText")
        {
            FontSize = BodyFont, Alignment = TextAlignment.Left,
            VerticalAlignment = TextVerticalAlignment.Top,
            FontShadowColor = shadow, FontShadowOffset = new Vector2(2.56f, 2.56f),
        };
        _charges = new StringFrame("ItemDetailCharges")
        {
            FontSize = BodyFont, Alignment = TextAlignment.Right,
            VerticalAlignment = TextVerticalAlignment.Middle,
            FontShadowColor = shadow, FontShadowOffset = new Vector2(2.56f, 2.56f),
        };
        AddChild(_icon);
        AddChild(_name);
        AddChild(_description);
        AddChild(_charges);
        Visible = false;
    }

    public void Update(ItemType type, int charges)
    {
        if (type == null)
        {
            Visible = false;
            return;
        }

        _icon.Texture = _iconResolver(type.IconArt);
        _name.Text = type.Name ?? type.Id ?? "";

        string body = type.Ubertip ?? "";
        if (!string.IsNullOrEmpty(type.BonusText) && type.BonusText != "-")
            body += (body.Length > 0 ? "\n" : "") + type.BonusText;
        _charges.Text = charges > 0 ? charges.ToString() : "";
        _charges.Visible = charges > 0;

        string text = type.GoldCost > 0 || type.LumberCost > 0
            ? body + (body.Length > 0 ? "\n" : "") + CostLine(type.GoldCost, type.LumberCost)
            : body;
        _description.Text = War3Text.Describe(text);

        int lines = Math.Max(1, UiText.Strip(body, newlineAsSpace: false).Split('\n').Length);
        _height = Math.Max(PanelMinHeight, Padding * 2 + IconSize + 8f + lines * RowHeight);
        Visible = true;
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float panelX = (viewportWidth - PanelWidth) * 0.5f;
        float panelY = Math.Max(0f, viewportHeight - _height - 12f);
        _icon.RenderBounds = new UiRect(panelX + Padding, panelY + Padding, IconSize, IconSize);
        _name.RenderBounds = new UiRect(panelX + Padding + IconSize + 8f, panelY + Padding,
            PanelWidth - Padding * 2 - IconSize - 8f - (_charges.Visible ? 40f : 0f), IconSize);
        _charges.RenderBounds = new UiRect(panelX + PanelWidth - Padding - 40f,
            panelY + Padding, 40f, RowHeight);
        float textTop = panelY + Padding + IconSize + 8f;
        _description.RenderBounds = new UiRect(panelX + Padding, textTop,
            PanelWidth - Padding * 2, _height - (textTop - panelY) - Padding);
    }

    private static string CostLine(int gold, int lumber)
    {
        if (gold > 0 && lumber > 0) return $"金币: {gold}  木材: {lumber}";
        return gold > 0 ? $"金币: {gold}" : $"木材: {lumber}";
    }
}
