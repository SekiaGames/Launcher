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

    private void HandleKeyboardPan(float delta)
    {
        if (_camera == null) return;

        float forwardInput = 0f;
        float rightInput = 0f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) forwardInput += 1f;
        if (Input.IsKeyPressed(Key.S) && PanKeyAvailable(Key.S) || Input.IsKeyPressed(Key.Down)) forwardInput -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) rightInput += 1f;
        if (Input.IsKeyPressed(Key.A) && PanKeyAvailable(Key.A) || Input.IsKeyPressed(Key.Left)) rightInput -= 1f;
        if (forwardInput == 0f && rightInput == 0f) return;

        Vector3 forward = -_camera.Camera.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.Normalized();
        Vector3 right = _camera.Camera.GlobalTransform.Basis.X;
        right.Y = 0f;
        right = right.Normalized();

        Vector3 move = forward * forwardInput + right * rightInput;
        float speed = 1200f * delta;
        _camera.Pan(new Vector2(move.X, move.Z) * speed);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            if (!keyEvent.Pressed)
            {
                ReleaseCommandKey(keyEvent.Keycode);
            }
            else if (!keyEvent.Echo)
            {
                if (keyEvent.Keycode == Key.Escape)
                {
                    if (_log != null && _log.InputActive) _log.CancelInput();
                    else if (CastPending) CancelCast();
                    else if (_escMenu != null && _escMenu.Visible) _escMenu.Close();
                    else _escMenu?.Open();
                    return;
                }

                if (keyEvent.Keycode is Key.Enter or Key.KpEnter)
                {
                    _log?.BeginInput();
                    return;
                }

                if (keyEvent.Keycode is Key.F1 or Key.F2 or Key.F3)
                {
                    if (TrySelectHeroByIndex((int)(keyEvent.Keycode - Key.F1))) return;
                }

                if (keyEvent.Keycode == Key.F8)
                {
                    SelectIdleWorker();
                    return;
                }

                if (TrySelectionHotkey(keyEvent.Keycode)) return;

                if (TryInventoryHotkey(keyEvent.Keycode)) return;

                if (keyEvent.Keycode == Key.Tab)
                {
                    CycleSubgroup();
                    return;
                }

                int group = (int)(keyEvent.Keycode - Key.Key0);
                if (group is >= 0 and <= 9)
                {
                    HandleGroupKey(group, keyEvent.CtrlPressed, keyEvent.ShiftPressed);
                    return;
                }
            }
        }

        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed) _camera.Zoom(-1);
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed) _camera.Zoom(1);
            else if (button.ButtonIndex == MouseButton.Middle)
            {
                _middleDown = button.Pressed;
                Input.MouseMode = button.Pressed
                    ? Input.MouseModeEnum.Captured
                    : (_cursor?.Active == true ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible);
                if (!button.Pressed) Input.WarpMouse(GetViewport().GetVisibleRect().Size / 2f);
            }
            else if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.AltPressed && _placingId == null)
                {
                    if (button.Pressed && TryGroundPoint(button.Position, out Vector3 pingPoint))
                    {
                        _hud?.MinimapFrame.Ping(pingPoint);
                        PingMinimap(new System.Numerics.Vector2(-pingPoint.X, pingPoint.Z));
                    }
                    return;
                }

                _leftDown = button.Pressed;
                if (button.Pressed)
                {
                    _dragStart = button.Position;
                    _hud?.SetSelectionBox(new Rect2(), false);
                }
                else if (_placingId != null)
                {
                    if (button.Position.DistanceTo(_dragStart) < 8f) TryPlace(button.Position);
                }
                else if (button.Position.DistanceTo(_dragStart) < 8f)
                {
                    _hud?.SetSelectionBox(new Rect2(), false);
                    HandleClick(button.Position, button.ShiftPressed);
                }
                else
                {
                    _hud?.SetSelectionBox(new Rect2(), false);
                    BoxSelect(_dragStart, button.Position, button.ShiftPressed);
                }
            }
            else if (button.ButtonIndex == MouseButton.Right && button.Pressed)
            {
                if (_placingId != null) CancelPlacement();
                else if (CastPending) CancelCast();
                else OrderAt(button.Position, button.ShiftPressed);
            }
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            if (_middleDown)
            {
                _camera.Pan(new Vector2(-motion.Relative.X, -motion.Relative.Y) * 1.6f);
                return;
            }

            if (_leftDown && _placingId == null && motion.Position.DistanceTo(_dragStart) >= 8f)
            {
                _hud?.SetSelectionBox(BoxRect(_dragStart, motion.Position), true);
            }
        }
    }

    private float _groupPressedAt = -1f;
    private int _groupPressed = -1;
    private float _clickTime = -1f;
    private Unit _clickUnit;

    private static Rect2 BoxRect(Vector2 a, Vector2 b)
    {
        float x = Mathf.Min(a.X, b.X);
        float y = Mathf.Min(a.Y, b.Y);
        return new Rect2(x, y, Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }

    private void HandleClick(Vector2 position, bool additive)
    {
        if (TryCommandAt(position)) return;
        if (TryCastAt(position)) return;

        float now = Time.GetTicksMsec() / 1000f;
        Unit picked = PickUnit(position, ViewPlayer);

        if (picked != null && _clickUnit == picked && now - _clickTime < 0.4f)
        {
            SelectSameType(picked);
            _clickUnit = null;
            _clickTime = -1f;
            return;
        }

        SelectAt(position, additive);
        _clickUnit = picked;
        _clickTime = now;
    }

    /// <summary>双击：把屏幕上所有同类型单位一起选上（客户端口径）。</summary>
    private void SelectSameType(Unit reference)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        List<Unit> units = _picking?.UnitsInScreenRect(Vector2.Zero, size,
            unit => unit.Player == ViewPlayer
                && string.Equals(unit.Type.Id, reference.Type.Id, StringComparison.OrdinalIgnoreCase))
            ?? new List<Unit>();
        _selection?.SelectRect(units, false);
        if (units.Count > 0)
            _hud?.SetHint($"已选中 {_selected.Count} 个 {reference.Type.Name}");
    }

    private void BoxSelect(Vector2 from, Vector2 to, bool additive)
    {
        Rect2 rect = BoxRect(from, to);
        if (rect.Size.X < 4f || rect.Size.Y < 4f) return;

        List<Unit> units = _picking?.UnitsInScreenRect(rect.Position, rect.End,
            unit => unit.Player == ViewPlayer) ?? new List<Unit>();
        _selection?.SelectRect(units, additive);
        if (units.Count > 0) _hud?.SetHint($"框选 {_selected.Count} 个单位");
    }

    private void HandleGroupKey(int group, bool ctrl, bool shift)
    {
        if (_selection == null) return;
        if (ctrl)
        {
            _selection.SetControlGroup(group);
            _hud?.SetHint($"编队 {group} 已保存（{_selected.Count} 个单位）");
            return;
        }
        if (shift)
        {
            _selection.AddToControlGroup(group);
            _hud?.SetHint($"已并入编队 {group}");
            return;
        }

        _selection.RecallControlGroup(group);
        if (_selected.Count == 0) return;

        Vector3 sum = Vector3.Zero;
        foreach (Unit unit in _selected) sum += unit.Position;

        float now = Time.GetTicksMsec() / 1000f;
        bool center = _groupPressed == group && now - _groupPressedAt < 0.5f;
        _groupPressed = group;
        _groupPressedAt = now;
        if (center) _camera.Focus(sum / _selected.Count);
    }

    private void SelectAt(Vector2 screenPosition, bool additive)
    {
        Unit picked = PickUnit(screenPosition, ViewPlayer);
        if (picked == null)
        {
            if (!additive) _selection?.Clear();
            return;
        }
        _selection?.SelectClicked(picked, additive, mouseChannel: true);
    }

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
