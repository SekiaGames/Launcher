using System;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class QueueIconRowFrame : UiContainer
{
    const int MaxSlots = 7;
    const float PanelWidth = 240.8f, PanelHeight = 146f;
    const float ActiveSize = 37f, WaitingSize = 27f;

    readonly ButtonFrame[] _buttons = new ButtonFrame[MaxSlots];
    readonly SolidColorFrame _backdrop;
    readonly StatusBarFrame _progress;
    readonly StringFrame _action;
    readonly Func<string, ITexture> _iconResolver;

    public QueueIconRowFrame(IUiTextureResolver textures, War3UiSkin skin) : base("QueueIcons")
    {
        _iconResolver = textures.Resolve;
        _backdrop = new SolidColorFrame("QueueBackdrop", new Vector4(0f, 0f, 0f, .55f));
        _progress = new StatusBarFrame("QueueProgress")
        {
            FillColor = Vector4.One,
            FillTexture = textures.Resolve(@"UI\Feedback\BuildProgressBar\human-buildprogressbar-fill.blp"),
            BorderTexture = textures.Resolve(@"UI\Feedback\BuildProgressBar\human-buildprogressbar-border.blp"),
        };
        _action = new StringFrame("QueueAction")
        {
            FontSize = 12.8f,
            Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            Color = new Vector4(.99f, .827f, .0705f, 1f),
            FontShadowColor = new Vector4(0, 0, 0, .9f),
            FontShadowOffset = new Vector2(1.28f, 1.28f),
        };
        AddChild(_backdrop);
        AddChild(_progress);
        AddChild(_action);
        for (int i = 0; i < MaxSlots; i++)
        {
            var button = new ButtonFrame($"QueueSlot{i}");
            _buttons[i] = button;
            AddChild(button);
        }
        MouseTransparent = true;
        Visible = false;
    }

    public static UiRect PanelBounds(float viewportWidth, float viewportHeight) =>
        new((viewportWidth - War3UiViewport.BaseWidth) * .5f + 396f,
            viewportHeight - PanelHeight, PanelWidth, PanelHeight);

    public void Update(System.Collections.Generic.IReadOnlyList<QueueEntry> entries)
    {
        int count = entries?.Count ?? 0;
        Visible = count > 0;
        if (!Visible) return;
        QueueEntry first = entries[0];
        _progress.Ratio = first.Total > 0f
            ? Math.Clamp(1f - first.Remaining / first.Total, 0f, 1f) : 0f;
        _action.Text = first.Name ?? "";
        for (int i = 0; i < MaxSlots; i++)
        {
            ButtonFrame button = _buttons[i];
            if (i >= count)
            {
                button.Visible = false;
                button.Icon = null;
                continue;
            }
            QueueEntry entry = entries[i];
            button.Visible = true;
            button.Icon = entry.Icon == null ? null : new GodotUiTexture(entry.Icon);
            button.TooltipTitle = entry.Name;
            button.TooltipBody = entry.Total > 0f
                ? $"{Math.Max(0f, entry.Remaining):0.0}s / {entry.Total:0.0}s" : "";
        }
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        UiRect panel = PanelBounds(viewportWidth, viewportHeight);
        RenderBounds = panel;
        _backdrop.RenderBounds = panel;
        _progress.RenderBounds = new UiRect(panel.X + 78.4f, panel.Y + 48.8f, 134.4f, 12.8f);
        _action.RenderBounds = new UiRect(panel.X + 78.4f, panel.Y + 31.24f, 134.4f, 14f);
        for (int i = 0; i < MaxSlots; i++)
        {
            if (i == 0)
            {
                _buttons[i].RenderBounds = new UiRect(panel.X + 13.44f,
                    viewportHeight - 104.12f, ActiveSize, ActiveSize);
            }
            else
            {
                _buttons[i].RenderBounds = new UiRect(panel.X + 12.16f + (i - 1) * 37.8f,
                    viewportHeight - 51.76f, WaitingSize, WaitingSize);
            }
        }
    }
}