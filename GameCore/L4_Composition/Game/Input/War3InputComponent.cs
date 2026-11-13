using Godot;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal sealed class War3InputComponent
    {
        private readonly War3GameScene _scene;
        private bool _middleDown;

        internal War3InputComponent(War3GameScene scene) => _scene = scene;

        internal void HandleEvent(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent)
            {
                HandleKey(keyEvent);
                return;
            }

            if (@event is InputEventMouseButton button)
            {
                HandleMouseButton(button);
                return;
            }

            if (@event is InputEventMouseMotion motion)
                HandleMouseMotion(motion);
        }

        private void HandleKey(InputEventKey keyEvent)
        {
            if (!keyEvent.Pressed)
            {
                _scene.ReleaseCommandKey(keyEvent.Keycode);
                return;
            }
            if (keyEvent.Echo) return;

            if (keyEvent.Keycode == Key.Escape)
            {
                if (_scene._log != null && _scene._log.InputActive) _scene._log.CancelInput();
                else if (_scene.CastPending) _scene.CancelCast();
                else if (_scene._escMenu != null && _scene._escMenu.Visible) _scene._escMenu.Close();
                else _scene._escMenu?.Open();
                return;
            }

            if (keyEvent.Keycode is Key.Enter or Key.KpEnter)
            {
                _scene._log?.BeginInput();
                return;
            }

            if (keyEvent.Keycode is Key.F1 or Key.F2 or Key.F3)
            {
                if (_scene.TrySelectHeroByIndex((int)(keyEvent.Keycode - Key.F1))) return;
            }

            if (keyEvent.Keycode == Key.F8)
            {
                _scene.SelectIdleWorker();
                return;
            }

            if (_scene.TrySelectionHotkey(keyEvent.Keycode)) return;
            if (_scene.TryInventoryHotkey(keyEvent.Keycode)) return;

            if (keyEvent.Keycode == Key.Tab)
            {
                _scene.CycleSubgroup();
                return;
            }

            int group = (int)(keyEvent.Keycode - Key.Key0);
            if (group is >= 0 and <= 9)
            {
                _scene._worldInteraction?.HandleGroupKey(group,
                    keyEvent.CtrlPressed, keyEvent.ShiftPressed);
            }
        }

        private void HandleMouseButton(InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
            {
                _scene._cameraComponent?.Scroll(button.Factor > 0f ? button.Factor : 1f);
                return;
            }
            if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
            {
                _scene._cameraComponent?.Scroll(button.Factor < 0f ? button.Factor : -1f);
                return;
            }
            if (button.ButtonIndex == MouseButton.Middle)
            {
                _middleDown = button.Pressed;
                if (button.Pressed) _scene._cameraComponent?.BeginDrag(button.Position);
                else _scene._cameraComponent?.EndDrag();
                return;
            }

            if (button.Pressed)
                _scene._worldInteraction?.PointerDown(button.Position, button.ButtonIndex);
            else
                _scene._worldInteraction?.PointerUp(button.Position, button.ButtonIndex);
        }

        private void HandleMouseMotion(InputEventMouseMotion motion)
        {
            if (_middleDown)
            {
                _scene._cameraComponent?.ContinueDrag(motion.Position);
                return;
            }
            _scene._worldInteraction?.PointerMove(motion.Position);
        }
    }
}