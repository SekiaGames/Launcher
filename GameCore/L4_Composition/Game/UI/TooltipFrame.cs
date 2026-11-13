using System.Collections.Generic;
using System.Numerics;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class TooltipFrame : UiContainer
{
    const float Width = 280f;
    const float Padding = 8f;
    const float LineHeight = 17f;

    readonly SolidColorFrame _background;
    readonly StringFrame _title;
    readonly StringFrame _body;
    float _height = 54f;

    public TooltipFrame() : base("Tooltip")
    {
        _background = new SolidColorFrame("TooltipBackdrop", new Vector4(16f / 255f, 24f / 255f,
            36f / 255f, .96f));
        _title = new StringFrame("TooltipTitle")
        {
            FontSize = 14.08f,
            Color = Vector4.One,
            FontShadowColor = new Vector4(0, 0, 0, 1),
            FontShadowOffset = new Vector2(1, 1),
        };
        _body = new StringFrame("TooltipBody")
        {
            FontSize = 14.08f,
            Color = Vector4.One,
            VerticalAlignment = TextVerticalAlignment.Top,
            FontShadowColor = new Vector4(0, 0, 0, 1),
            FontShadowOffset = new Vector2(1, 1),
        };
        AddChild(_background);
        AddChild(_title);
        AddChild(_body);
        Visible = false;
    }

    public void Show(ButtonFrame button)
    {
        string title = button?.TooltipTitle;
        string body = button?.TooltipBody;
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            Visible = false;
            return;
        }
        string costs = FormatCosts(button?.TooltipCosts);
        _title.Text = title ?? "";
        _title.Visible = !string.IsNullOrWhiteSpace(title);
        string text = Wrap(body ?? "", 34);
        _body.Text = string.IsNullOrEmpty(costs) ? text
            : (text.Length > 0 ? text + "\n" : "") + costs;
        _body.Visible = _body.Text.Length > 0;
        int titleLines = _title.Visible ? 1 : 0;
        int bodyLines = _body.Visible ? CountLines(_body.Text) : 0;
        _height = Padding * 2f + titleLines * LineHeight + bodyLines * LineHeight;
        Visible = true;
    }

    public void Hide() => Visible = false;

    static string FormatCosts(int[] costs)
    {
        if (costs == null) return "";
        string[] names = { "法力", "金币", "木材", "人口" };
        var parts = new List<string>();
        for (int i = 0; i < costs.Length && i < names.Length; i++)
            if (costs[i] != 0) parts.Add($"{names[i]} {costs[i]}");
        return string.Join("  ", parts);
    }

    static string Wrap(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var result = new List<string>();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (char c in paragraph)
            {
                if (line.Length >= width)
                {
                    result.Add(line);
                    line = "";
                }
                line += c;
            }
            result.Add(line);
        }
        return string.Join("\n", result);
    }

    static int CountLines(string text) =>
        string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float x = viewportWidth - Width - 12.8f;
        float y = viewportHeight - 225.28f - _height - 12.8f;
        RenderBounds = new UiRect(x, y, Width, _height);
        _background.RenderBounds = RenderBounds;
        _title.RenderBounds = new UiRect(x + Padding, y + Padding,
            Width - Padding * 2, LineHeight);
        _body.RenderBounds = new UiRect(x + Padding, y + Padding + LineHeight,
            Width - Padding * 2, _height - Padding * 2 - LineHeight);
        _body.LayoutValid = true;
    }
}