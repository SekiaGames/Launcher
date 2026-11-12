using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game.UI;

/// <summary>
/// 左下角消息区（客户端 ChatFrame 的精简版）：滚动的系统播报 + Enter 输入的一句聊天。
/// 单机没有网络，聊天只写进本地日志，但输入/回车的交互与原版一致。
/// </summary>
public sealed class ChatFrame
{
    private const int MaxLines = 7;

    private readonly List<string> _lines = new();
    private readonly Label _text;
    private readonly LineEdit _input;

    public bool InputActive => _input.Visible;

    public ChatFrame(Node parent)
    {
        var layer = new CanvasLayer { Name = "GameLog", Layer = 3 };
        parent.AddChild(layer);

        _text = new Label
        {
            Position = new Vector2(224f, 560f),
            Size = new Vector2(620f, 110f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Bottom,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        _text.AddThemeFontSizeOverride("font_size", 14);
        _text.AddThemeColorOverride("font_color", new Color(0.82f, 0.86f, 0.9f));
        _text.AddThemeColorOverride("font_outline_color", Colors.Black);
        _text.AddThemeConstantOverride("outline_size", 5);
        layer.AddChild(_text);

        _input = new LineEdit
        {
            Position = new Vector2(224f, 622f),
            Size = new Vector2(460f, 26f),
            PlaceholderText = "输入消息，回车发送，Esc 取消",
            Visible = false,
            MaxLength = 120,
        };
        _input.AddThemeFontSizeOverride("font_size", 14);
        _input.TextSubmitted += text =>
        {
            Send(text);
            CancelInput();
        };
        layer.AddChild(_input);
    }

    public void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _lines.Add(line);
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);
        _text.Text = string.Join("\n", _lines);
    }

    public void BeginInput()
    {
        _input.Visible = true;
        _input.Text = "";
        _input.GrabFocus();
    }

    public void CancelInput()
    {
        _input.Visible = false;
        _input.ReleaseFocus();
        _input.Text = "";
    }

    private void Send(string text)
    {
        string body = (text ?? "").Trim();
        if (body.Length == 0) return;
        Add($"[玩家] {body}");
        GD.Print($"[CHAT] {body}");
    }
}