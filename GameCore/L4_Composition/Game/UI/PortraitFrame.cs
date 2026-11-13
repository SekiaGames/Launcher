using System;
using System.Numerics;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class PortraitFrame : UiContainer, IClickable
{
    const float ModelX = 270.08f, ModelWidth = 106.88f, ModelHeight = 108.8f;
    const float ModelBottomOffset = 36.48f;
    const float LineHeight = 14f;

    readonly StringFrame _hpText;
    readonly StringFrame _mpText;
    UiRect _modelBounds;
    Unit _unit;

    public Action OnStartFollow;
    public Action OnStopFollow;

    public PortraitFrame() : base("Portrait")
    {
        _hpText = new StringFrame("PortraitHp")
        {
            FontSize = 12.65f,
            Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            FontShadowColor = new Vector4(0, 0, 0, 1),
            FontShadowOffset = new Vector2(1, 1),
        };
        _mpText = new StringFrame("PortraitMp")
        {
            FontSize = 11.5f,
            Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            FontShadowColor = new Vector4(0, 0, 0, 1),
            FontShadowOffset = new Vector2(1, 1),
        };
        AddChild(_hpText);
        AddChild(_mpText);
    }

    public static UiRect ModelArea(float viewportHeight) =>
        new(ModelX, viewportHeight - ModelBottomOffset - ModelHeight, ModelWidth, ModelHeight);

    public void Layout(float viewportWidth, float viewportHeight)
    {
        _modelBounds = ModelArea(viewportHeight);
        float x = ModelX, width = ModelWidth;
        _hpText.RenderBounds = new UiRect(x, viewportHeight - 27.36f - LineHeight * .5f,
            width, LineHeight);
        _mpText.RenderBounds = new UiRect(x, viewportHeight - 9.12f - LineHeight * .5f,
            width, LineHeight);
    }

    public void Update(Unit selected)
    {
        _unit = selected;
        Visible = selected != null;
        if (selected == null)
        {
            _hpText.Text = "";
            _mpText.Text = "";
            return;
        }
        float ratio = selected.MaxHp <= 0 ? 0f : Math.Clamp(selected.Hp / selected.MaxHp, 0f, 1f);
        _hpText.Text = selected.MaxHp > 0
            ? $"{MathF.Ceiling(selected.Hp):0} / {MathF.Ceiling(selected.MaxHp):0}" : "";
        _hpText.Color = new Vector4(Math.Clamp(2f - ratio * 2f, 0f, 1f),
            Math.Clamp(ratio * 2f, 0f, 1f), 0f, 1f);
        _mpText.Text = selected.MaxMana > 0
            ? $"{MathF.Ceiling(selected.Mana):0} / {MathF.Ceiling(selected.MaxMana):0}" : "";
        _mpText.Color = Vector4.One;
    }

    bool IClickable.Enabled => Enabled;

    public override UiFrame HitTest(Vector2 point)
    {
        if (!Visible || !Enabled || MouseTransparent) return null;
        return _modelBounds.Contains(point.X, point.Y) ? this : null;
    }

    void IClickable.OnPointerDown(Vector2 pos) => OnStartFollow?.Invoke();
    void IClickable.OnPointerUp(Vector2 pos) => OnStopFollow?.Invoke();
    void IClickable.OnClick(int button) => OnStopFollow?.Invoke();
}