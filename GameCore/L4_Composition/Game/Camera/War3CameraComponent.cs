using Godot;
using System;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal sealed class War3CameraComponent
    {
        readonly War3GameScene _runtime;
        bool _dragging;
        bool _dragPending;
        Vector2 _dragPendingScreen;
        float _dragPlaneY;
        Vector3 _dragLastPoint;

        internal War3CameraComponent(War3GameScene runtime) => _runtime = runtime;

        internal void Update(float deltaTime)
        {
            War3Camera camera = _runtime._camera;
            if (camera == null) return;
            if (_runtime.Paused)
            {
                _dragPending = false;
                camera.SetRotationKeys(false, false);
                return;
            }

            ApplyPendingDrag();
            bool up = Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W)
                || Input.IsKeyPressed(Key.Kp8);
            bool down = Input.IsKeyPressed(Key.Down)
                || Input.IsKeyPressed(Key.S) && _runtime.PanKeyAvailable(Key.S)
                || Input.IsKeyPressed(Key.Kp2);
            bool left = Input.IsKeyPressed(Key.Left)
                || Input.IsKeyPressed(Key.A) && _runtime.PanKeyAvailable(Key.A)
                || Input.IsKeyPressed(Key.Kp4);
            bool right = Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)
                || Input.IsKeyPressed(Key.Kp6);

            Vector2 size = _runtime.GetViewport().GetVisibleRect().Size;
            Vector2 mouse = _runtime.GetViewport().GetMousePosition();
            if (!_dragging && mouse.X >= 0f && mouse.Y >= 0f
                && mouse.X < size.X && mouse.Y < size.Y
                && size.X > 0f && size.Y > 0f
                && !_runtime.IsScreenPointOverHud(mouse))
            {
                const float edge = 3f;
                left |= mouse.X <= edge;
                right |= mouse.X >= size.X - edge;
                up |= mouse.Y <= edge;
                down |= mouse.Y >= size.Y - edge;
            }

            camera.SetRotationKeys(Input.IsKeyPressed(Key.Insert),
                Input.IsKeyPressed(Key.Delete));
            camera.Update(deltaTime, up, down, left, right);
        }

        internal void Scroll(float amount)
        {
            if (_runtime.Paused) return;
            _runtime._camera?.Scroll(amount);
        }

        internal bool BeginDrag(Vector2 screen)
        {
            War3Camera camera = _runtime._camera;
            if (camera == null || _runtime.Paused) return false;
            _dragPlaneY = _runtime.CameraTargetHeightAt(camera.Target.X, camera.Target.Z);
            if (!TryScreenPoint(screen, out Vector3 point)) return false;
            _dragging = true;
            _dragPending = false;
            _dragLastPoint = point;
            return true;
        }

        internal void ContinueDrag(Vector2 screen)
        {
            if (!_dragging) return;
            _dragPending = true;
            _dragPendingScreen = screen;
        }

        internal void EndDrag()
        {
            ApplyPendingDrag();
            _dragging = false;
            _dragPending = false;
        }

        void ApplyPendingDrag()
        {
            if (!_dragPending) return;
            _dragPending = false;
            if (!_dragging || _runtime.Paused || _runtime._camera == null) return;
            if (!TryScreenPoint(_dragPendingScreen, out Vector3 point)) return;
            Vector3 delta = point - _dragLastPoint;
            _runtime._camera.Pan(-delta.X, -delta.Z);
        }

        bool TryScreenPoint(Vector2 screen, out Vector3 point)
        {
            point = default;
            Camera3D camera = _runtime._camera?.Camera;
            if (camera == null) return false;
            Vector3 origin = camera.ProjectRayOrigin(screen);
            Vector3 direction = camera.ProjectRayNormal(screen);
            if (Mathf.Abs(direction.Y) < 1e-5f) return false;
            float distance = (_dragPlaneY - origin.Y) / direction.Y;
            if (distance < 0f) return false;
            point = origin + direction * distance;
            return true;
        }
    }
}