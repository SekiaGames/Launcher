using Godot;
using System;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class ControlGroupFrame : UiContainer
{
    private const int Groups = 9;
    private const float ButtonSize = 24f;
    private const float Gap = 3f;
    private const float Left = 8f;
    private const float TopOffset = 6f;
    private const float DoubleClickSeconds = 0.5f;

    private readonly ButtonFrame[] _buttons = new ButtonFrame[Groups];
    private readonly StringFrame[] _labels = new StringFrame[Groups];
    private readonly double[] _lastClick = new double[Groups];

    public Action<int, bool> GroupClicked;

    public ControlGroupFrame(IUiTextureResolver textures) : base("ControlGroups")
    {
        MouseTransparent = true;
        ITexture background = Resolve(textures, @"UI\Widgets\Console\Human\human-console-button-up.blp");
        ITexture pushed = Resolve(textures, @"UI\Widgets\Console\Human\human-console-button-down.blp")
            ?? background;
        ITexture highlight = Resolve(textures, @"UI\Widgets\Console\Human\human-console-button-highlight.blp");

        for (int index = 0; index < Groups; index++)
        {
            int group = index + 1;
            var button = new ButtonFrame($"ControlGroup{group}")
            {
                NormalTexture = background,
                PushedTexture = pushed,
                ActiveHighlight = highlight,
                TooltipTitle = $"编队 {group}",
                TooltipBody = "点击选中；双击镜头居中",
            };
            button.Clicked += _ => ClickGroup(group);
            var label = new StringFrame($"ControlGroup{group}Label")
            {
                Text = group.ToString(),
                FontSize = 15f,
                Color = new NVector4(1f, 0.84f, 0.12f, 1f),
                Alignment = TextAlignment.Center,
                VerticalAlignment = TextVerticalAlignment.Middle,
                FontShadowColor = new NVector4(0f, 0f, 0f, 1f),
                FontShadowOffset = new NVector2(1f, 1f),
                MouseTransparent = true,
            };
            AddChild(button);
            AddChild(label);
            _buttons[index] = button;
            _labels[index] = label;
        }
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float top = viewportHeight - 225.28f + TopOffset;
        for (int index = 0; index < Groups; index++)
        {
            var rect = new UiRect(Left + index * (ButtonSize + Gap), top, ButtonSize, ButtonSize);
            _buttons[index].RenderBounds = rect;
            _labels[index].RenderBounds = rect;
        }
    }

    private void ClickGroup(int group)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        bool center = now - _lastClick[group - 1] <= DoubleClickSeconds;
        _lastClick[group - 1] = now;
        GroupClicked?.Invoke(group, center);
    }

    private static ITexture Resolve(IUiTextureResolver textures, string path)
        => textures == null || string.IsNullOrWhiteSpace(path) ? null : textures.Resolve(path);
}