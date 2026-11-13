using System.Numerics;

namespace OpenWC3.UI;

// 文字 Frame: 对齐 Warsmash StringFrame (字号/颜色/对齐/|c颜色码)
// 字号是设计单位; FDF 的 0.8 比例值由 FdfInflator 经 FdfScale.S 换算后写入
public class StringFrame : UiFrame
{
    public string Text = "";
    public float FontSize = 12.8f;      // 设计单位 (= FDF 0.010 x 1280)
    public Vector4 Color = Vector4.One;
    public Vector4 DisabledColor = new(.4f, .5f, .6f, .7f);
    public bool Disabled;
    public Vector2 JustificationOffset;
    public bool UseFontLineHeight;
    public float TruncateWidth;
    /// <summary>世界飘字/加载页等自带尺寸计算的字形不参与「UI字体大小」缩放。</summary>
    public bool IgnoreUiFontScale;
    string _lastText, _displayText;
    float _lastFontSize, _lastTruncateWidth;
    int _lastMeasurementRevision = -1;
    IUiRenderDevice _lastDevice;

    // 实际绘制/度量字号: 默认 1.0 时 UiFontScale.Apply 原样返回, 与旧行为一致。
    float ScaledFontSize => IgnoreUiFontScale ? FontSize : UiFontScale.Apply(FontSize);

    public string DisplayText(IUiRenderDevice device)
    {
        if (TruncateWidth <= 0) return Text;
        float fontSize = ScaledFontSize;
        if (_lastText == Text && _lastFontSize == fontSize && _lastTruncateWidth == TruncateWidth
            && _lastMeasurementRevision == device.TextMeasurementRevision
            && ReferenceEquals(device, _lastDevice)) return _displayText;
        _lastText = Text; _lastFontSize = fontSize; _lastTruncateWidth = TruncateWidth;
        _lastMeasurementRevision = device.TextMeasurementRevision;
        _lastDevice = device;
        float Measure(string value) => UseFontLineHeight ? device.MeasureTextLineWidth(value, fontSize)
            : device.MeasureTextWidth(value, fontSize);
        _displayText = Text ?? "";
        if (Measure(_displayText) <= TruncateWidth) return _displayText;
        var cuts = new List<int> { 0 };
        for (int i = 0; i < _displayText.Length;)
        {
            if (_displayText[i] == '|' && i + 1 < _displayText.Length)
            {
                char code = char.ToLowerInvariant(_displayText[i + 1]);
                if (code == 'c' && i + 9 < _displayText.Length
                    && _displayText.Substring(i + 2, 8).All(Uri.IsHexDigit)) { i += 10; continue; }
                if (code == 'r') { i += 2; continue; }
                if (code == '|') { i += 2; cuts.Add(i); continue; }
            }
            i += char.IsHighSurrogate(_displayText[i]) && i + 1 < _displayText.Length
                && char.IsLowSurrogate(_displayText[i + 1]) ? 2 : 1;
            cuts.Add(i);
        }
        int low = 0, high = cuts.Count - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (Measure(_displayText[..cuts[mid]]) <= TruncateWidth) low = mid;
            else high = mid - 1;
        }
        // Native 6f2a2540 removes three visible characters from the fitting
        // prefix and appends the literal "...". Preserve embedded color tags.
        _displayText = _displayText[..cuts[Math.Max(0, low - 3)]] + "...";
        return _displayText;
    }
    public TextAlignment Alignment = TextAlignment.Left;
    public TextVerticalAlignment VerticalAlignment = TextVerticalAlignment.Top;
    public Vector4? FontShadowColor;  // 阴影色 (null=无阴影, 对齐原版 FontShadowColor)
    public Vector2 FontShadowOffset = new(1, 1);  // 阴影偏移 (设计单位)

    public StringFrame(string name = null) : base(name) { }

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        if (!string.IsNullOrEmpty(Text))
        {
            float fontSize = ScaledFontSize;
            // 尺寸未解出时不裁剪, 否则 0x0 的 scissor 会把文字整段裁掉
            bool clip = RenderBounds.Width > 0 && RenderBounds.Height > 0;
            if (clip)
            {
                // 字号放大后旧帧高可能装不下字形。只在非 1.0 且确有必要时按对齐方式
                // 纵向扩出裁剪区, 避免半截字; 横向仍按原宽度裁剪以保留截断语义。
                float extra = fontSize - FontSize;
                if (extra > 0)
                {
                    float above = VerticalAlignment switch
                    {
                        TextVerticalAlignment.Middle => extra * .5f,
                        TextVerticalAlignment.Bottom => extra,
                        _ => 0f,
                    };
                    device.PushScissor(new UiRect(RenderBounds.X, RenderBounds.Y - above,
                        RenderBounds.Width, RenderBounds.Height + extra));
                }
                else device.PushScissor(RenderBounds);
            }
            var textBounds = new UiRect(RenderBounds.X + JustificationOffset.X,
                RenderBounds.Y + JustificationOffset.Y,
                Math.Max(0, RenderBounds.Width - JustificationOffset.X), RenderBounds.Height);
            string text = DisplayText(device);
            if (UseFontLineHeight)
                device.DrawTextLine(textBounds, text, fontSize, Disabled ? DisabledColor : Color, Alignment,
                    FontShadowColor, FontShadowOffset, VerticalAlignment);
            else device.DrawText(textBounds, text, fontSize, Disabled ? DisabledColor : Color, Alignment,
                FontShadowColor, FontShadowOffset, VerticalAlignment);
            if (clip) device.PopScissor();
        }
        base.Render(device);
    }
}
