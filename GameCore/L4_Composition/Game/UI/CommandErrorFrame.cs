using Godot;
using System;
using OpenWC3.UI;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace OpenWC3.Game.UI;

/// <summary>命令/建造类提示（客户端 CommandErrorFrame / CGameUI+0x3E4 WorldFrameMessage）。</summary>
internal sealed class CommandErrorFrame : UiContainer
{
    const float U = FdfScale.ToDesignUnits;
    const float ScreenH = War3UiViewport.BaseHeight;
    const float MessageWidth = 0.38f * U;
    const float MessageHeight = 0.05f * U;
    const float MessageLeft = 0.21f * U;
    const float MessageTop = ScreenH - 0.23f * U;
    const float MessageBottom = MessageTop + MessageHeight;
    const float MessageFont = 0.015f * U;
    const float BaseLifetimeSeconds = 3f;
    const float PerCharacterSeconds = .1f;
    const float FadeSeconds = 3f;
    static readonly NVector2 ShadowOffset = new(0.001f * U, -0.001f * U);

    readonly Func<long> _now;
    readonly StringFrame _message;
    long _shownAt;
    long _lifetimeMilliseconds;

    internal string Message => _message.Text;

    public CommandErrorFrame(Func<long> now = null) : base("SmashErrorMessageFrame")
    {
        _now = now ?? (() => (long)Time.GetTicksMsec());
        _message = new StringFrame("SmashErrorMessageText")
        {
            Color = new NVector4(1f, .8f, 0f, 1f),
            Alignment = TextAlignment.Left,
            VerticalAlignment = TextVerticalAlignment.Top,
            FontShadowColor = new NVector4(0f, 0f, 0f, .9f),
            MouseTransparent = true,
        };
        AddChild(_message);
        MouseTransparent = true;
        Visible = false;
    }

    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _message.Text = message;
        _message.Color = new NVector4(1f, .8f, 0f, 1f);
        _lifetimeMilliseconds = (long)((BaseLifetimeSeconds + PerCharacterSeconds * message.Length) * 1000);
        _shownAt = _now();
        Visible = true;
        Layout();
    }

    public void Update()
    {
        if (!Visible) return;
        long elapsed = Math.Max(0, _now() - _shownAt);
        long fadeMilliseconds = (long)(FadeSeconds * 1000);
        if (elapsed >= _lifetimeMilliseconds + fadeMilliseconds)
        {
            Visible = false;
            return;
        }
        float alpha = elapsed <= _lifetimeMilliseconds ? 1f
            : Math.Clamp(1f - (elapsed - _lifetimeMilliseconds) / (float)fadeMilliseconds, 0f, 1f);
        _message.Color = new NVector4(1f, .8f, 0f, alpha);
    }

    public void Layout()
    {
        RenderBounds = new UiRect(MessageLeft, MessageTop, MessageWidth, MessageHeight);
        _message.FontSize = MessageFont;
        _message.UseFontLineHeight = true;
        _message.FontShadowOffset = ShadowOffset;
        float step = MathF.Max(1f, UiFontScale.Apply(MessageFont));
        _message.RenderBounds = new UiRect(MessageLeft, MessageBottom - step, MessageWidth, step);
    }
}