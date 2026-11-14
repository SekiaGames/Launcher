using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

/// <summary>
/// 右下信息面板 tooltip（客户端 CToolTipWar3）：名称 / 成本行 / 描述 / Ex 扩展段。
/// 宽度按渲染端实测折行，面板跟随悬停控件并钳在屏幕内。
/// </summary>
internal sealed class TooltipFrame : UiContainer
{
    const float TextEm = 0.011f * FdfScale.ToDesignUnits;
    const float TextLineHeight = 0.013375f * FdfScale.ToDesignUnits;
    const float Padding = 0.005f * FdfScale.ToDesignUnits;
    const float Border = 0.01f * FdfScale.ToDesignUnits;
    const float CostIconSize = 0.009375f * FdfScale.ToDesignUnits;
    const float CostIconGap = 0.00625f * FdfScale.ToDesignUnits;
    const float CostTextGap = 0.003125f * FdfScale.ToDesignUnits;
    const float CostRowDrop = 0.004f * FdfScale.ToDesignUnits;
    const float DescMaxWidth = 0.2f * FdfScale.ToDesignUnits;
    const float ExMinWidth = 0.21f * FdfScale.ToDesignUnits;
    const float ConsoleBarHeight = 0.176f * FdfScale.ToDesignUnits;
    const float ScreenEdgeInset = 0.01f * FdfScale.ToDesignUnits;

    static readonly Vector4 NameColor = new(1f, 1f, 1f, 1f);
    static readonly Vector4 DescColor = new(1f, 1f, 1f, 1f);
    static readonly Vector4 CostColor = new(254f / 255f, 211f / 255f, 18f / 255f, 1f);
    static readonly Vector4 BackdropSolidColor = new(16f / 255f, 24f / 255f, 36f / 255f, 1f);

    static readonly string[] CostIconKeys =
    {
        "ToolTipManaIcon", "ToolTipGoldIcon", "ToolTipLumberIcon", "ToolTipSupplyIcon",
    };

    static readonly string[] CostIconDefaults =
    {
        @"UI\Widgets\ToolTips\Human\ToolTipManaIcon.blp",
        @"UI\Widgets\ToolTips\Human\ToolTipGoldIcon.blp",
        @"UI\Widgets\ToolTips\Human\ToolTipLumberIcon.blp",
        @"UI\Widgets\ToolTips\Human\ToolTipSupplyIcon.blp",
    };

    readonly IUiTextureResolver _textures;
    readonly War3UiSkin _skin;
    readonly SolidColorFrame _solid;
    readonly BackdropFrame _backdrop;
    readonly StringFrame _title;
    readonly StringFrame _body;
    readonly StringFrame _ext;
    readonly TextureFrame[] _costIcons = new TextureFrame[4];
    readonly StringFrame[] _costTexts = new StringFrame[4];

    UiFrame _anchor;
    string _titleText = "", _bodyText = "", _extText = "";
    IReadOnlyList<int> _costs;
    float _viewportWidth = War3UiViewport.BaseWidth;
    float _viewportHeight = War3UiViewport.BaseHeight;

    /// <summary>宿主注入的渲染端度量（与绘制同一字号口径）。为空时按字符宽估算。</summary>
    public Func<string, float, float> TextMeasure;
    public Func<string, float, float> TextHeightMeasure;

    public TooltipFrame(IUiTextureResolver textures, War3UiSkin skin) : base("Tooltip")
    {
        _textures = textures;
        _skin = skin;

        _solid = new SolidColorFrame("TooltipSolid", BackdropSolidColor);
        _backdrop = new BackdropFrame("TooltipBackdrop")
        {
            BackgroundTexture = textures.Resolve(skin.Get("ToolTipBackground",
                @"UI\Widgets\ToolTips\Human\human-tooltip-background.blp")),
            EdgeTexture = textures.Resolve(skin.Get("ToolTipEdge",
                @"UI\Widgets\ToolTips\Human\human-tooltip-border.blp")),
            CornerSize = 12.8f,
            Insets = new UiRect(3.2f, 3.2f, 3.2f, 3.2f),
            TileBackground = true,
            BackgroundSize = 51.2f,
        };
        _title = CreateText("TooltipTitle");
        _body = CreateText("TooltipBody");
        _ext = CreateText("TooltipExt");
        _title.MouseTransparent = true;
        _body.MouseTransparent = true;
        _ext.MouseTransparent = true;
        _backdrop.MouseTransparent = true;
        AddChild(_solid);
        AddChild(_backdrop);
        AddChild(_title);
        AddChild(_body);
        AddChild(_ext);
        for (int i = 0; i < 4; i++)
        {
            _costIcons[i] = new TextureFrame($"TooltipCostIcon{i}") { Visible = false };
            _costTexts[i] = CreateText($"TooltipCostText{i}");
            _costTexts[i].Color = CostColor;
            _costTexts[i].Visible = false;
            _costTexts[i].MouseTransparent = true;
            AddChild(_costIcons[i]);
            AddChild(_costTexts[i]);
        }
        MouseTransparent = true;
        Visible = false;
    }

    static StringFrame CreateText(string name) => new(name)
    {
        FontSize = TextEm,
        Color = NameColor,
        VerticalAlignment = TextVerticalAlignment.Top,
        FontShadowColor = new Vector4(0f, 0f, 0f, 1f),
        FontShadowOffset = new Vector2(1.28f, 1.28f),
        UseFontLineHeight = true,
    };

    public void Show(UiFrame anchor, string title, string body, float viewportWidth, float viewportHeight)
        => ShowEx(anchor, title, body, null, null, viewportWidth, viewportHeight);

    public void Show(UiFrame anchor, string title, string body, IReadOnlyList<int> costs,
        float viewportWidth, float viewportHeight)
        => ShowEx(anchor, title, body, null, costs, viewportWidth, viewportHeight);

    public void ShowEx(UiFrame anchor, string title, string body, string ext, IReadOnlyList<int> costs,
        float viewportWidth, float viewportHeight)
    {
        _anchor = anchor;
        _titleText = title ?? "";
        _bodyText = body ?? "";
        _extText = ext ?? "";
        _costs = costs;
        _viewportWidth = viewportWidth <= 0f ? War3UiViewport.BaseWidth : viewportWidth;
        _viewportHeight = viewportHeight <= 0f ? War3UiViewport.BaseHeight : viewportHeight;
        if (string.IsNullOrWhiteSpace(UiText.Strip(_titleText, newlineAsSpace: false)))
        {
            Visible = false;
            return;
        }
        ApplyLayout();
    }

    public void Hide() => Visible = false;

    internal string TitleText => _titleText;
    internal string BodyText => _bodyText;

    public void Layout(float viewportWidth, float viewportHeight)
    {
        if (!Visible) return;
        _viewportWidth = viewportWidth <= 0f ? War3UiViewport.BaseWidth : viewportWidth;
        _viewportHeight = viewportHeight <= 0f ? War3UiViewport.BaseHeight : viewportHeight;
        ApplyLayout();
    }

    void ApplyLayout()
    {
        float em = UiFontScale.Apply(TextEm);
        float lineH = MathF.Max(UiFontScale.Apply(TextLineHeight), TextHeightMeasure?.Invoke("M", em) ?? 0f);
        float pad = UiFontScale.Apply(Padding);
        float border = UiFontScale.Apply(Border);
        float iconSize = UiFontScale.Apply(CostIconSize);
        float iconGap = UiFontScale.Apply(CostIconGap);
        float textGap = UiFontScale.Apply(CostTextGap);
        float costDrop = UiFontScale.Apply(CostRowDrop);
        float descCap = UiFontScale.Apply(DescMaxWidth);
        float exMinWidth = UiFontScale.Apply(ExMinWidth);

        List<string> titleLines = UiText.WrapToWidth(_titleText, 0f, null);
        List<string> bodySource = UiText.WrapToWidth(_bodyText, 0f, null);
        List<string> extSource = UiText.WrapToWidth(_extText, 0f, null);
        bool hasBody = HasContent(bodySource);
        bool hasExt = HasContent(extSource);

        float titleWidth = 0f;
        foreach (string line in titleLines) titleWidth = MathF.Max(titleWidth, Measure(line, em));
        float bodyNatural = 0f;
        foreach (string line in bodySource) bodyNatural = MathF.Max(bodyNatural, Measure(line, em));

        int[] costValues = new int[4];
        int costCount = 0;
        float costRowWidth = 0f;
        for (int slot = 0; slot < 4; slot++)
        {
            costValues[slot] = _costs != null && slot < _costs.Count ? _costs[slot] : 0;
            if (costValues[slot] == 0) continue;
            costCount++;
            costRowWidth += Measure(costValues[slot].ToString(CultureInfo.InvariantCulture), em)
                + iconSize + textGap + (costCount > 1 ? iconGap : 0f);
        }

        float contentWidth = MathF.Max(titleWidth, MathF.Max(costRowWidth,
            MathF.Min(descCap, bodyNatural)));
        float wrapWidth = contentWidth;
        if (hasExt)
        {
            contentWidth = MathF.Max(titleWidth, exMinWidth);
            wrapWidth = exMinWidth;
        }
        contentWidth = MathF.Max(1f, contentWidth);

        List<string> bodyLines = hasBody
            ? UiText.WrapToWidth(_bodyText, wrapWidth, text => Measure(text, em))
            : new List<string>();
        List<string> extLines = hasExt
            ? UiText.WrapToWidth(_extText, wrapWidth, text => Measure(text, em))
            : new List<string>();

        float costRowHeight = costCount > 0 ? MathF.Max(lineH, iconSize) : 0f;
        float panelWidth = contentWidth + pad * 2f + border;
        float panelHeight = pad
            + titleLines.Count * lineH
            + (costCount > 0 ? costDrop + costRowHeight : 0f)
            + (hasBody ? pad + bodyLines.Count * lineH : 0f)
            + (hasExt ? pad + extLines.Count * lineH : 0f)
            + pad;

        var (x, y) = Place(_anchor, panelWidth, panelHeight, _viewportWidth, _viewportHeight);
        RenderBounds = new UiRect(x, y, panelWidth, panelHeight);
        _solid.RenderBounds = RenderBounds;
        _backdrop.RenderBounds = RenderBounds;

        _title.FontSize = em;
        _title.Color = NameColor;
        _title.Text = string.Join("\n", titleLines);
        _title.RenderBounds = new UiRect(x + pad, y + pad, contentWidth + 1f, titleLines.Count * lineH);

        float cursorY = y + pad + titleLines.Count * lineH;
        int compact = 0;
        if (costCount > 0)
        {
            float costTop = cursorY + costDrop;
            float cursorX = x + pad;
            for (int slot = 0; slot < 4; slot++)
            {
                if (costValues[slot] == 0) continue;
                string value = costValues[slot].ToString(CultureInfo.InvariantCulture);
                float valueWidth = Measure(value, em);
                TextureFrame icon = _costIcons[compact];
                StringFrame label = _costTexts[compact];
                icon.Texture = _textures.Resolve(_skin.Get(CostIconKeys[slot], CostIconDefaults[slot]));
                icon.RenderBounds = new UiRect(cursorX, costTop, iconSize, iconSize);
                icon.Visible = icon.Texture != null;
                label.FontSize = em;
                label.Color = CostColor;
                label.Text = value;
                label.RenderBounds = new UiRect(cursorX + iconSize + textGap,
                    costTop + MathF.Max(0f, (costRowHeight - lineH) * 0.5f), valueWidth + 1f, lineH);
                label.Visible = true;
                cursorX += iconSize + textGap + valueWidth + iconGap;
                compact++;
            }
            cursorY = costTop + costRowHeight;
        }
        for (int i = compact; i < 4; i++)
        {
            _costIcons[i].Visible = false;
            _costTexts[i].Visible = false;
        }

        if (hasBody)
        {
            cursorY += pad;
            _body.FontSize = em;
            _body.Color = DescColor;
            _body.Text = string.Join("\n", bodyLines);
            _body.RenderBounds = new UiRect(x + pad, cursorY, contentWidth + 1f, bodyLines.Count * lineH);
            _body.Visible = true;
            cursorY += bodyLines.Count * lineH;
        }
        else _body.Visible = false;

        if (hasExt)
        {
            cursorY += pad;
            _ext.FontSize = em;
            _ext.Color = NameColor;
            _ext.Text = string.Join("\n", extLines);
            _ext.RenderBounds = new UiRect(x + pad, cursorY, contentWidth + 1f, extLines.Count * lineH);
            _ext.Visible = true;
        }
        else _ext.Visible = false;

        Visible = true;
    }

    static bool HasContent(List<string> lines)
    {
        foreach (string line in lines)
            if (!string.IsNullOrWhiteSpace(line)) return true;
        return false;
    }

    float Measure(string text, float em)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        if (TextMeasure != null) return TextMeasure(text, em);
        float width = 0f;
        foreach (char c in text) width += c > 0x2e80 ? em : em * .5f;
        return width;
    }

    static (float X, float Y) Place(UiFrame anchor, float width, float height,
        float viewportWidth, float viewportHeight)
    {
        float maxX = MathF.Max(ScreenEdgeInset, viewportWidth - width - ScreenEdgeInset);
        float maxY = MathF.Max(ScreenEdgeInset, viewportHeight - height - ScreenEdgeInset);
        UiRect bounds = anchor?.RenderBounds ?? default;
        if (anchor == null || (bounds.Width <= 0f && bounds.Height <= 0f))
        {
            float fallbackX = viewportWidth - width - ScreenEdgeInset;
            float fallbackY = viewportHeight - ConsoleBarHeight - height;
            return (Math.Clamp(fallbackX, ScreenEdgeInset, maxX), Math.Clamp(fallbackY, ScreenEdgeInset, maxY));
        }

        float x = Math.Clamp(bounds.X + bounds.Width - width, ScreenEdgeInset, maxX);
        float above = bounds.Y - height - 4f;
        float y = above >= ScreenEdgeInset ? above : bounds.Bottom + 4f;
        return (x, Math.Clamp(y, ScreenEdgeInset, maxY));
    }
}