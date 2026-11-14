using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class BattleOverlayRenderer : Control
{
    private const string HealthFillPath = @"UI\Feedback\HPBarConsole\human-healthbar-fill.blp";

    public Camera3D Camera { get; set; }
    public bool ShowHealthBars { get; set; } = true;
    public bool ShowManaBars { get; set; } = true;

    private IReadOnlyList<Unit> _units = Array.Empty<Unit>();
    private Func<Unit, bool> _visible;
    private Func<Unit, bool> _enemy;
    private Texture2D _healthFill;

    public void Frame(IReadOnlyList<Unit> units, Func<Unit, bool> visible, Func<Unit, bool> enemy)
    {
        _units = units ?? Array.Empty<Unit>();
        _visible = visible;
        _enemy = enemy;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!ShowHealthBars || Camera == null || !Camera.IsInsideTree()) return;

        _healthFill ??= Assets.LoadTexture(War3Assets.ConvertedPath(HealthFillPath));
        Vector2 viewport = GetViewportRect().Size;
        int width = Mathf.Max(1, (int)viewport.X);
        int height = Mathf.Max(1, (int)viewport.Y);

        for (int index = 0; index < _units.Count; index++)
        {
            Unit unit = _units[index];
            if (unit == null || !unit.Alive || unit.Type == null) continue;
            if (_visible != null && !_visible(unit)) continue;

            UnitSceneComponent view = unit.View;
            if (view?.Root == null || !view.Root.IsVisibleInTree()) continue;

            Vector3 modelTop = view.Root.GlobalPosition + new Vector3(0f, view.Height, 0f);
            if (Camera.IsPositionBehind(modelTop) || !Camera.IsPositionInFrustum(modelTop)) continue;
            Vector2 screen = Camera.UnprojectPosition(modelTop);
            if (screen.X < -64f || screen.Y < -64f
                || screen.X > width + 64f || screen.Y > height + 64f) continue;

            (float barWidth, float barHeight, float inset) =
                UnitStatusBarGeometry.Pixels(unit.Type.SelectionDiameter, width, height);
            if (!float.IsFinite(barWidth) || barWidth <= 0f) continue;
            barHeight = Mathf.Max(1f, barHeight);
            inset = Mathf.Min(inset, (barHeight - 1f) * 0.5f);

            float left = screen.X - barWidth * 0.5f;
            float bottom = screen.Y - barHeight;
            DrawRect(new Rect2(left, bottom, barWidth, barHeight), Colors.Black);

            float life = unit.MaxHp > 0f ? Mathf.Clamp(unit.Hp / unit.MaxHp, 0f, 1f) : 0f;
            float fillWidth = Mathf.Max(0f, (barWidth - inset * 2f) * life);
            if (fillWidth > 0f)
            {
                Color color = _enemy != null && _enemy(unit)
                    ? new Color(1f, 0f, 0f)
                    : new Color(Mathf.Min(1f, 2f - life * 2f), Mathf.Min(1f, life * 2f), 0f);
                DrawBarFill(left + inset, bottom + inset,
                    fillWidth, barHeight - inset * 2f, life, color);
            }

            if (!ShowManaBars || unit.MaxMana <= 0f) continue;

            float manaHeight = UnitStatusBarGeometry.ManaHeight(height);
            float manaBottom = bottom - manaHeight;
            DrawRect(new Rect2(left, manaBottom, barWidth, manaHeight), Colors.Black);
            float mana = Mathf.Clamp(unit.Mana / unit.MaxMana, 0f, 1f);
            float manaInset = Mathf.Min(inset * 0.6f, (manaHeight - 1f) * 0.5f);
            float manaWidth = Mathf.Max(0f, (barWidth - manaInset * 2f) * mana);
            if (manaWidth <= 0f) continue;
            DrawBarFill(left + manaInset, manaBottom + manaInset,
                manaWidth, manaHeight - manaInset * 2f, mana, new Color(.1f, .3f, 1f));
        }
    }

    private void DrawBarFill(float x, float y, float width, float height, float ratio, Color color)
    {
        Rect2 rect = new(x, y, width, height);
        if (_healthFill == null)
        {
            DrawRect(rect, color);
            return;
        }
        Rect2 source = new(0f, 0f, _healthFill.GetWidth() * Mathf.Clamp(ratio, 0f, 1f), _healthFill.GetHeight());
        DrawTextureRectRegion(_healthFill, rect, source, color);
    }
}
