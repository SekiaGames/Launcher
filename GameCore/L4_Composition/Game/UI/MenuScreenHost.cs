using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game.UI;

internal sealed class MenuScreenHost : IDisposable
{
    readonly Node _parent;
    readonly Dictionary<string, Action> _commands =
        new(StringComparer.OrdinalIgnoreCase);
    Node _current;

    public MenuScreenHost(Node parent) => _parent = parent;

    public Node Current => _current;
    public event Action QuitRequested;

    public void RegisterCommand(string command, Action handler) =>
        _commands[command] = handler;

    public bool Execute(string command)
    {
        if (string.IsNullOrEmpty(command)) return false;
        if (!_commands.TryGetValue(command, out Action handler)) return false;
        handler();
        return true;
    }

    public void Show(Node screen)
    {
        if (ReferenceEquals(_current, screen)) return;
        Node previous = _current;
        _current = screen;
        if (screen != null && screen.GetParent() == null) _parent.AddChild(screen);
        previous?.QueueFree();
    }

    public void Adopt(Node screen) => _current = screen;

    public void Clear()
    {
        _current?.QueueFree();
        _current = null;
    }

    public void Dispose()
    {
        _current?.QueueFree();
        _current = null;
        _commands.Clear();
    }
}