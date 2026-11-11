using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

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
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) forwardInput -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) rightInput += 1f;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) rightInput -= 1f;
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
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape)
            {
                if (CastPending) CancelCast();
                else Back();
                return;
            }

            // 英雄热键：小键盘 1~6 用物品，字母键按物编热键施法（主键盘 1~0 仍是编队）
            if (TryHeroHotkey(key.Keycode)) return;

            int group = (int)(key.Keycode - Key.Key0);
            if (group is >= 0 and <= 9)
            {
                HandleGroupKey(group, key.CtrlPressed);
                return;
            }
        }

        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed) _camera.Zoom(-1);
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed) _camera.Zoom(1);
            else if (button.ButtonIndex == MouseButton.Middle)
            {
                _middleDown = button.Pressed;
                Input.MouseMode = button.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
                if (!button.Pressed) Input.WarpMouse(GetViewport().GetVisibleRect().Size / 2f);
            }
            else if (button.ButtonIndex == MouseButton.Left)
            {
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
                else OrderAt(button.Position);
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

    private readonly Dictionary<int, List<Unit>> _groups = new();
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
        Camera3D camera = _camera.Camera;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;

        ClearSelection();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer) continue;
            if (!string.Equals(unit.Type.Id, reference.Type.Id, StringComparison.OrdinalIgnoreCase)) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;
            Vector2 screen = camera.UnprojectPosition(point);
            if (screen.X < 0f || screen.Y < 0f || screen.X > viewport.X || screen.Y > viewport.Y) continue;

            _selected.Add(unit);
            unit.View?.SetSelected(true);
        }

        if (_selected.Count == 0) return;
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        _hud?.SetHint($"已选中 {_selected.Count} 个 {reference.Type.Name}");
        PlaySelectionVoice();
    }

    private void BoxSelect(Vector2 from, Vector2 to, bool additive)
    {
        Rect2 rect = BoxRect(from, to);
        if (rect.Size.X < 4f || rect.Size.Y < 4f) return;

        Camera3D camera = _camera.Camera;
        if (!additive) ClearSelection();

        int added = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;

            Vector2 screen = camera.UnprojectPosition(point);
            if (!rect.HasPoint(screen)) continue;
            if (_selected.Contains(unit)) continue;

            _selected.Add(unit);
            unit.View?.SetSelected(true);
            added++;
        }

        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        if (added > 0)
        {
            _hud?.SetHint($"框选 {_selected.Count} 个单位");
            PlaySelectionVoice();
        }
    }

    private void HandleGroupKey(int group, bool assign)
    {
        if (assign)
        {
            _groups[group] = new List<Unit>(_selected);
            _hud?.SetHint($"编队 {group} 已保存（{_selected.Count} 个单位）");
            return;
        }

        if (!_groups.TryGetValue(group, out List<Unit> members)) return;
        members.RemoveAll(unit => unit == null || !unit.Alive);
        if (members.Count == 0) return;

        ClearSelection();
        Vector3 sum = Vector3.Zero;
        foreach (Unit unit in members)
        {
            _selected.Add(unit);
            unit.View?.SetSelected(true);
            sum += unit.Position;
        }
        _hud?.SetSelection(_selected);
        UpdateCommandCard();

        float now = Time.GetTicksMsec() / 1000f;
        bool center = _groupPressed == group && now - _groupPressedAt < 0.5f;
        _groupPressed = group;
        _groupPressedAt = now;

        if (center)
        {
            _camera.Focus(sum / members.Count);
        }
    }

    private void SelectAt(Vector2 screenPosition, bool additive)
    {
        Unit picked = PickUnit(screenPosition, ViewPlayer);
        if (picked == null)
        {
            if (!additive) ClearSelection();
            return;
        }

        if (!additive) ClearSelection();
        if (_selected.Contains(picked))
        {
            picked.View?.SetSelected(false);
            _selected.Remove(picked);
        }
        else
        {
            _selected.Add(picked);
            picked.View?.SetSelected(true);
        }
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        PlaySelectionVoice();
    }

    private void ClearSelection()
    {
        foreach (Unit unit in _selected) unit.View?.SetSelected(false);
        _selected.Clear();
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
    }

    /// <summary>指针下的单位；anyPlayer=true 时不限阵营（技能选目标用），includeCorpses=true 时连尸体一起算。</summary>
    private Unit PickUnit(Vector2 screenPosition, int player, bool anyPlayer = false, bool includeCorpses = false)
    {
        Camera3D camera = _camera.Camera;
        Unit best = null;
        float bestDistance = 55f;

        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive && !(includeCorpses && unit.IsCorpse)) continue;
            if (!anyPlayer && unit.Player != player) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;

            Vector2 screen = camera.UnprojectPosition(point);
            float distance = screen.DistanceTo(screenPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }
}
