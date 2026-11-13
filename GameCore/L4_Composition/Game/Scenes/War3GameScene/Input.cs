using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private void PushClick(Vector2 position, MouseButton button)
    {
        var press = new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = true,
            Position = position,
            GlobalPosition = position,
        };
        var release = new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = false,
            Position = position,
            GlobalPosition = position,
        };
        GetViewport().PushInput(press);
        GetViewport().PushInput(release);
    }

    public override void _UnhandledInput(InputEvent @event) => _inputComponent?.HandleEvent(@event);

    private void ClearSelection()
    {
        if (_selection != null)
        {
            _selection.Clear();
            return;
        }
        foreach (Unit unit in _selected) unit.View?.SetSelected(false);
        _selected.Clear();
        _subgroupPool.Clear();
        _subgroupType = "";
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
    }

    private Unit PickUnit(Vector2 screenPosition, int player, bool anyPlayer = false,
        bool includeCorpses = false)
        => _picking?.PickUnit(screenPosition, player, anyPlayer, includeCorpses);
}