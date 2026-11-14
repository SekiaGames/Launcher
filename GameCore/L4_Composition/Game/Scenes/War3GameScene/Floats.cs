using Godot;
using System.Collections.Generic;
using OpenWC3.Game.UI;
using OpenWC3.UI;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private readonly List<TextTagFrame.TagView> _textTagViews = new();

    // 视口外剔除余量(px)：覆盖阴影偏移与锚点/估算宽度误差，避免边缘误裁。
    private const float FloatTextCullMargin = 48f;

    /// <summary>普通伤害数字（客户端默认关；暴击始终显示）。</summary>
    public bool ShowDamageNumbers;

    private void OnDamageApplied(DamageData info)
    {
        if (info?.Target == null || info.Cancelled) return;

        // 暴击出在攻击者头上，用的是减伤前的数值（客户端 WeaponSystem 4b0460 取的是 retained pre-reduction）。
        if (info.IsCritical) FloatingText.ShowCriticalStrike(info.Source ?? info.Target, info.Raw);

        if (!ShowDamageNumbers || info.DamageType == DamageType.Magic || info.Amount <= 0f) return;
        FloatingText.ShowOrdinaryDamage(info.Target, info.Amount);
    }

    private void ShowMissFloat(Unit attacker) => FloatingText.ShowMiss(attacker);

    private void ShowManaBurnFloat(Unit target, float drained) => FloatingText.ShowManaBurn(target, drained);

    /// <summary>
    /// 世界层飘字投影（客户端 Ui.BuildTextTagViews）：每帧把模拟层的飘字投影成屏幕空间文字，
    /// 交给 HUD 在所有界面之前绘制。字号按帧缓冲高度换算，拉远镜头不跟着缩小。
    /// </summary>
    private void UpdateWorldText()
    {
        Camera3D camera = _camera?.Camera;
        if (camera == null || _world == null || _newHud == null)
        {
            _newHud?.RenderWorldText(null);
            return;
        }

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0f || viewport.Y <= 0f) return;
        float scaleX = viewport.X / GameHudScreen.DesignWidth;
        float scaleY = viewport.Y / GameHudScreen.DesignHeight;

        List<TextTagFrame.TagView> views = _textTagViews;
        views.Clear();
        foreach (FloatingText text in _world.Floats)
        {
            if (text == null || string.IsNullOrEmpty(text.Text)) continue;
            // 普通伤害数字受「伤害数字显示」和迷雾两道门禁；其它反馈位置固定，只做视口剔除。
            if (text.OrdinaryDamage && (!ShowDamageNumbers || !IsAudible(text.Pos))) continue;

            var point = new Vector3(text.Pos.X, text.WorldHeight, text.Pos.Z);
            if (camera.IsPositionBehind(point)) continue;
            Vector2 screen = camera.UnprojectPosition(point);
            float screenX = screen.X / scaleX + text.OffsetX
                + (text.Style?.Velocity.X ?? 0f) * text.Age * GameHudScreen.DesignWidth / .8f;
            float screenY = screen.Y / scaleY
                - text.Age * (text.Style != null
                    ? text.Style.Velocity.Y * GameHudScreen.DesignHeight / .6f
                    : 30f);
            float fontSize = text.Style != null
                ? TextTagFrame.FontLineHeight(text.Style.Height, (int)viewport.Y, scaleY)
                : 24f;

            // 锚点在文字左下角(向上、向右展开)；宽度只用于剔除，估大不影响观感。
            float estimatedWidth = System.Math.Max(40f, text.Text.Length * fontSize * .75f) + FloatTextCullMargin;
            if (screenX > GameHudScreen.DesignWidth + FloatTextCullMargin
                || screenX + estimatedWidth < -FloatTextCullMargin
                || screenY < -FloatTextCullMargin
                || screenY - fontSize > GameHudScreen.DesignHeight + FloatTextCullMargin) continue;

            views.Add(new TextTagFrame.TagView
            {
                ScreenX = screenX,
                ScreenY = screenY,
                Text = text.Text,
                FontSize = fontSize,
                Alignment = TextAlignment.Left,
                Color = new System.Numerics.Vector4(text.Color.R, text.Color.G, text.Color.B, text.Alpha),
            });
        }
        _newHud.RenderWorldText(views);
    }
}
