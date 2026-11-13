using System.Numerics;
using System.Text;

namespace OpenWC3.UI;

/// <summary>FDF TEXTAREA: wrapped lines, fixed line pitch, clipped pixel scrolling.</summary>
public class TextAreaFrame : UiFrame
{
    public string[] Lines = Array.Empty<string>();
    public float FontSize = 12.8f;
    public Vector4 Color = Vector4.One;
    public TextAlignment Alignment = TextAlignment.Left;
    public Vector4? FontShadowColor;
    public Vector2 FontShadowOffset = new(1, 1);
    public float LineHeight = 12.8f, LineGap, Inset;
    public int MaxLines;
    string _text = "";
    float _layoutWidth = -1, _layoutFontSize;
    IUiRenderDevice _layoutDevice;
    int _measurementRevision;
    ScrollBarFrame _scrollBar;

    // 实际字号/行高: 默认 1.0 时原样返回; 字形与行距必须一起放大, 否则多行会叠字。
    float ScaledFontSize => UiFontScale.Apply(FontSize);
    float ScaledLineHeight => UiFontScale.Apply(LineHeight);

    public TextAreaFrame(string name = null) : base(name) { }

    public void AttachScrollBar(ScrollBarFrame scrollBar)
    {
        if (ReferenceEquals(_scrollBar, scrollBar)) return;
        if (_scrollBar != null) _scrollBar.ValueChanged -= OnScroll;
        _scrollBar = scrollBar;
        if (_scrollBar != null) _scrollBar.ValueChanged += OnScroll;
        BindScroll();
    }

    void OnScroll(float _) => InvalidateLayout();

    public void SetText(string text)
    {
        text = (text ?? "").Replace("|n", "\n").Replace("|N", "\n")
            .Replace("\r\n", "\n").Replace('\r', '\n');
        if (_text == text) return;
        _text = text;
        _layoutWidth = -1;
        InvalidateLayout();
    }

    public void SetLines(IEnumerable<string> lines) =>
        SetText(lines == null ? "" : string.Join("\n", lines));

    public override void Position()
    {
        base.Position();
        Reflow(_layoutDevice);
        BindScroll();
    }

    void Reflow(IUiRenderDevice device)
    {
        float width = Math.Max(1, RenderBounds.Width - 2 * Inset);
        float fontSize = ScaledFontSize;
        if (_layoutWidth == width && _layoutFontSize == fontSize
            && ReferenceEquals(device, _layoutDevice)
            && _measurementRevision == (device?.TextMeasurementRevision ?? 0)) return;
        _layoutWidth = width;
        _layoutFontSize = fontSize;
        _layoutDevice = device;
        _measurementRevision = device?.TextMeasurementRevision ?? 0;
        float Measure(string text) => device?.MeasureTextLineWidth(text, fontSize)
            ?? text.Sum(c => c > 0x2e80 ? fontSize : fontSize * .55f);
        var lines = new List<string>();
        string color = "";
        if (_text.Length > 0)
            foreach (string paragraph in _text.Split('\n'))
            {
                var glyphs = ColoredGlyphs(paragraph, ref color);
                if (glyphs.Count == 0) { lines.Add(""); continue; }
                // Greedy line breaking preserves explicit blank lines and prefers
                // spaces for Latin words; CJK and oversized words break at glyphs.
                int start = 0;
                while (start < glyphs.Count)
                {
                    int end = start, lastSpace = -1;
                    while (end < glyphs.Count)
                    {
                        int next = end + 1;
                        string plain = string.Concat(glyphs.GetRange(start, next - start).Select(g => g.Plain));
                        if (end > start && Measure(plain) > width) break;
                        if (string.IsNullOrWhiteSpace(glyphs[end].Plain)) lastSpace = end;
                        end = next;
                    }
                    if (end < glyphs.Count && lastSpace > start) end = lastSpace;
                    var line = new StringBuilder();
                    string lastColor = "";
                    for (int i = start; i < end; i++)
                    {
                        var glyph = glyphs[i];
                        if (glyph.Color != lastColor)
                        { line.Append(glyph.Color.Length == 0 ? "|r" : glyph.Color); lastColor = glyph.Color; }
                        line.Append(glyph.Text);
                    }
                    lines.Add(line.ToString().TrimEnd());
                    start = end;
                    while (start < glyphs.Count && string.IsNullOrWhiteSpace(glyphs[start].Plain)) start++;
                }
            }
        Lines = lines.ToArray();
    }

    static List<(string Text, string Plain, string Color)> ColoredGlyphs(string text, ref string color)
    {
        var glyphs = new List<(string, string, string)>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '|' && i + 1 < text.Length)
            {
                if (char.ToLowerInvariant(text[i + 1]) == 'c' && i + 9 < text.Length
                    && text.Substring(i + 2, 8).All(Uri.IsHexDigit))
                { color = text.Substring(i, 10); i += 9; continue; }
                if (char.ToLowerInvariant(text[i + 1]) == 'r') { color = ""; i++; continue; }
                if (text[i + 1] == '|') { glyphs.Add(("||", "|", color)); i++; continue; }
            }
            int size = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
            string value = text.Substring(i, size);
            glyphs.Add((value, value, color)); i += size - 1;
        }
        return glyphs;
    }

    void BindScroll()
    {
        if (_scrollBar == null) return;
        float step = Math.Max(.001f, ScaledLineHeight + UiFontScale.Apply(LineGap));
        float view = Math.Max(0, RenderBounds.Height - 2 * Inset);
        float total = Lines.Length * step;
        _scrollBar.Min = 0;
        _scrollBar.Max = Math.Max(0, total - view);
        _scrollBar.SetPage(view, total);
        _scrollBar.Visible = _scrollBar.Max > 0;
    }

    public int VisibleLineCount() => Math.Max(1, (int)MathF.Floor(
        Math.Max(0, RenderBounds.Height - 2 * Inset)
            / Math.Max(.001f, ScaledLineHeight + UiFontScale.Apply(LineGap))));

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        Reflow(device);
        BindScroll();
        float fontSize = ScaledFontSize, lineHeight = ScaledLineHeight;
        float step = Math.Max(.001f, lineHeight + UiFontScale.Apply(LineGap));
        float scroll = _scrollBar?.Value ?? 0;
        int first = Math.Min(Lines.Length, (int)(scroll / step));
        var clip = new UiRect(RenderBounds.X + Inset, RenderBounds.Y + Inset,
            Math.Max(0, RenderBounds.Width - 2 * Inset), Math.Max(0, RenderBounds.Height - 2 * Inset));
        device.PushScissor(clip);
        for (int i = first; i < Lines.Length; i++)
        {
            float y = clip.Y + i * step - scroll;
            if (y >= clip.Bottom) break;
            device.DrawTextLine(new UiRect(clip.X, y, clip.Width, lineHeight),
                Lines[i], fontSize, Color, Alignment, FontShadowColor,
                FontShadowOffset, TextVerticalAlignment.Top);
        }
        device.PopScissor();
        base.Render(device);
    }
}
