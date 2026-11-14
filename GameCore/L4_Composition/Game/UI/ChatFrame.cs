using Godot;
using System;
using System.Collections.Generic;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed class ChatFrame : UiContainer
{
    private const float ChatLeft = 8f;
    private const float ChatWidth = 990.4f;
    private const float ChatBottom = 512f;
    private const float ChatFont = 16.64f;
    private const float BaseLifetimeSeconds = 3f;
    private const float PerCharacterSeconds = 0.1f;
    private const float FadeSeconds = 3f;
    private const int Capacity = 30;
    private const int HistoryLines = 128;
    private const float InputWidth = 500f;
    private const float InputHeight = 20f;

    private sealed class ChatLine
    {
        public string Text = "";
        public List<string> Rows = new();
        public float WrapWidth = -1f;
        public int WrapRevision = -1;
        public double Expires;
        public double FadeStart = -1;
        public readonly List<StringFrame> Frames = new();
    }

    private readonly List<ChatLine> _messages = new();
    private readonly Stack<StringFrame> _pool = new();
    private readonly List<StringFrame> _visible = new();
    private readonly SolidColorFrame _inputBg;
    private readonly StringFrame _inputText;
    private readonly LineEdit _input;
    private readonly Node _inputHost;
    private IUiRenderDevice _device;
    private bool _inputActive;
    private int _createdRows;
    private double _now;

    public bool InputActive => _inputActive;

    public ChatFrame(Node inputHost) : base("Chat")
    {
        MouseTransparent = true;
        _inputHost = inputHost;
        _inputBg = new SolidColorFrame("ChatInputBg", new NVector4(0f, 0f, 0f, 0.5f))
        {
            Visible = false,
        };
        _inputText = new StringFrame("ChatInput")
        {
            FontSize = ChatFont,
            Color = NVector4.One,
            VerticalAlignment = TextVerticalAlignment.Top,
            FontShadowColor = new NVector4(0f, 0f, 0f, 0.9f),
            FontShadowOffset = new NVector2(1.28f, -1.28f),
            MouseTransparent = true,
            Visible = false,
        };
        AddChild(_inputBg);
        AddChild(_inputText);

        _input = new LineEdit
        {
            Visible = true,
            Modulate = new Color(1f, 1f, 1f, 0f),
            Position = new Vector2(-100f, -100f),
            Size = new Vector2(2f, 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            MaxLength = 159,
        };
        _input.TextSubmitted += text =>
        {
            Send(text);
            CancelInput();
        };
        inputHost.AddChild(_input);
    }

    public void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var entry = new ChatLine
        {
            Text = line,
            Expires = Now() + BaseLifetimeSeconds + PerCharacterSeconds * line.Length,
        };
        _messages.Add(entry);
        while (_messages.Count > HistoryLines)
        {
            Recycle(_messages[0]);
            _messages.RemoveAt(0);
        }
    }

    public void BeginInput()
    {
        _inputActive = true;
        _input.Text = "";
        _input.GrabFocus();
        PositionInputRow();
    }

    public void CancelInput()
    {
        _inputActive = false;
        _input.ReleaseFocus();
        _input.Text = "";
    }

    public void Update(float viewportWidth, float viewportHeight, IUiRenderDevice device)
    {
        _device = device;
        _now = Now();
        for (int index = _messages.Count - 1; index >= 0; index--)
        {
            ChatLine line = _messages[index];
            if (line.FadeStart < 0 && _now >= line.Expires)
            {
                line.FadeStart = _now;
            }
            else if (line.FadeStart >= 0 && _now - line.FadeStart >= FadeSeconds)
            {
                Recycle(line);
                _messages.RemoveAt(index);
            }
        }

        Visible = _messages.Count > 0 || _inputActive;
        Layout();
    }

    private void Layout()
    {
        if (!Visible) return;
        float step = MathF.Max(1f, UiFontScale.Apply(ChatFont));
        Reflow();
        int totalRows = 0;
        foreach (ChatLine line in _messages) totalRows += Mathf.Max(1, line.Rows.Count);
        while (_messages.Count > 0 && totalRows > Capacity)
        {
            ChatLine oldest = _messages[0];
            totalRows -= Mathf.Max(1, oldest.Rows.Count);
            Recycle(oldest);
            _messages.RemoveAt(0);
        }

        _visible.Clear();
        float cursor = ChatBottom - 8f;
        for (int index = _messages.Count - 1; index >= 0; index--)
        {
            ChatLine entry = _messages[index];
            int rows = entry.Rows.Count;
            float alpha = entry.FadeStart >= 0
                ? Mathf.Clamp(1f - (float)((_now - entry.FadeStart) / FadeSeconds), 0f, 1f)
                : 1f;
            for (int row = rows - 1; row >= 0; row--)
            {
                StringFrame frame = RentFrame(entry, row);
                frame.Text = entry.Rows[row];
                frame.FontSize = ChatFont;
                frame.UseFontLineHeight = true;
                frame.Color = new NVector4(0.9f, 0.9f, 0.9f, alpha);
                frame.FontShadowColor = new NVector4(0f, 0f, 0f, 0.9f * alpha);
                frame.FontShadowOffset = new NVector2(1.28f, -1.28f);
                frame.RenderBounds = new UiRect(ChatLeft, cursor - step, ChatWidth, step);
                frame.Visible = true;
                _visible.Add(frame);
                cursor -= step;
            }
            HideExtraFrames(entry, rows);
        }

        _inputBg.Visible = _inputActive;
        _inputText.Visible = _inputActive;
        if (!_inputActive) return;
        _inputBg.RenderBounds = new UiRect(ChatLeft, ChatBottom, InputWidth, InputHeight);
        _inputText.RenderBounds = new UiRect(ChatLeft + 8f, ChatBottom + 3f, InputWidth - 16f, step);
        _inputText.Text = "聊天: " + (_input.Text ?? "") + "_";
        PositionInputRow();
    }

    private void PositionInputRow()
    {
        if (_inputHost is not Control host || host.Size.X <= 0f || host.Size.Y <= 0f) return;
        if (host is GameHudScreen hud)
        {
            Vector2 topLeft = hud.UiToScreen(new NVector2(ChatLeft, ChatBottom));
            Vector2 bottomRight = hud.UiToScreen(new NVector2(ChatLeft + InputWidth, ChatBottom + InputHeight));
            _input.Position = topLeft;
            _input.Size = new Vector2(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
            return;
        }
        _input.Position = new Vector2(ChatLeft * host.Size.X / War3UiViewport.BaseWidth,
            ChatBottom * host.Size.Y / War3UiViewport.BaseHeight);
        _input.Size = new Vector2(InputWidth * host.Size.X / War3UiViewport.BaseWidth,
            InputHeight * host.Size.Y / War3UiViewport.BaseHeight);
    }

    private void Reflow()
    {
        int revision = _device?.TextMeasurementRevision ?? -1;
        foreach (ChatLine line in _messages)
        {
            if (line.Rows.Count > 0 && line.WrapWidth == ChatWidth && line.WrapRevision == revision)
                continue;
            line.Rows = UiText.WrapToWidth(line.Text, ChatWidth, Measure);
            if (line.Rows.Count == 0) line.Rows.Add("");
            line.WrapWidth = ChatWidth;
            line.WrapRevision = revision;
        }
    }

    private float Measure(string text)
    {
        float size = UiFontScale.Apply(ChatFont);
        return _device?.MeasureTextLineWidth(text, size) ?? EstimateWidth(text, size);
    }

    private static float EstimateWidth(string text, float size)
    {
        string plain = UiText.Strip(text);
        float width = 0f;
        foreach (char character in plain)
            width += character > 0x2e80 ? size : size * 0.55f;
        return width;
    }

    private StringFrame RentFrame(ChatLine line, int index)
    {
        while (line.Frames.Count <= index)
        {
            StringFrame frame = _pool.Count > 0 ? _pool.Pop() : NewRow();
            frame.Visible = false;
            line.Frames.Add(frame);
        }
        return line.Frames[index];
    }

    private static void HideExtraFrames(ChatLine line, int used)
    {
        for (int index = used; index < line.Frames.Count; index++) line.Frames[index].Visible = false;
    }

    private StringFrame NewRow()
    {
        var frame = new StringFrame($"ChatHistory{_createdRows++}")
        {
            Color = new NVector4(0.9f, 0.9f, 0.9f, 1f),
            Alignment = TextAlignment.Left,
            VerticalAlignment = TextVerticalAlignment.Top,
            FontShadowColor = new NVector4(0f, 0f, 0f, 0.9f),
            FontShadowOffset = new NVector2(1.28f, -1.28f),
            MouseTransparent = true,
            Visible = false,
        };
        AddChild(frame);
        return frame;
    }

    private void Recycle(ChatLine line)
    {
        foreach (StringFrame frame in line.Frames)
        {
            frame.Visible = false;
            frame.Text = "";
            _pool.Push(frame);
        }
        line.Frames.Clear();
    }

    private void Send(string text)
    {
        string body = (text ?? "").Trim();
        if (body.Length == 0) return;
        Add($"[玩家] {body}");
        GD.Print($"[CHAT] {body}");
    }

    private static double Now() => Time.GetTicksMsec() / 1000.0;
}