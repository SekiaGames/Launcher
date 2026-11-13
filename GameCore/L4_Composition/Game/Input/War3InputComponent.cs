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
                _scene._camera.Zoom(-1);
                return;
            }
            if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
            {
                _scene._camera.Zoom(1);
                return;
            }
            if (button.ButtonIndex == MouseButton.Middle)
            {
                _middleDown = button.Pressed;
                Input.MouseMode = button.Pressed
                    ? Input.MouseModeEnum.Captured
                    : (_scene._cursor?.Active == true
                        ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible);
                if (!button.Pressed)
                    Input.WarpMouse(_scene.GetViewport().GetVisibleRect().Size / 2f);
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
                _scene._camera.Pan(new Vector2(-motion.Relative.X, -motion.Relative.Y) * 1.6f);
                return;
            }
            _scene._worldInteraction?.PointerMove(motion.Position);
        }

        internal void HandleKeyboardPan(float delta)
        {
            if (_scene._camera == null) return;

            float forwardInput = 0f;
            float rightInput = 0f;
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) forwardInput += 1f;
            if (Input.IsKeyPressed(Key.S) && _scene.PanKeyAvailable(Key.S)
                || Input.IsKeyPressed(Key.Down)) forwardInput -= 1f;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) rightInput += 1f;
            if (Input.IsKeyPressed(Key.A) && _scene.PanKeyAvailable(Key.A)
                || Input.IsKeyPressed(Key.Left)) rightInput -= 1f;
            if (forwardInput == 0f && rightInput == 0f) return;

            Vector3 forward = -_scene._camera.Camera.GlobalTransform.Basis.Z;
            forward.Y = 0f;
            forward = forward.Normalized();
            Vector3 right = _scene._camera.Camera.GlobalTransform.Basis.X;
            right.Y = 0f;
            right = right.Normalized();

            Vector3 move = forward * forwardInput + right * rightInput;
            _scene._camera.Pan(new Vector2(move.X, move.Z) * (1200f * delta));
        }
    }
}