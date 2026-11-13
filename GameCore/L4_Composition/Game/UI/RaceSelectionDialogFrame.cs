using System;
using System.Collections.Generic;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class RaceSelectionDialogFrame : UiContainer
{
    const float PanelWidth = 560f;
    const float PanelHeight = 430f;
    const float ButtonWidth = 360f;
    const float ButtonHeight = 46f;

    readonly SolidColorFrame _overlay;
    readonly BackdropFrame _panel;
    readonly StringFrame _title;
    readonly StringFrame _description;
    readonly List<BackdropFrame> _buttonPlates = new();
    readonly List<ButtonFrame> _buttons = new();
    readonly List<StringFrame> _labels = new();
    readonly ITexture _buttonBackground;
    readonly ITexture _buttonBorder;
    readonly ITexture _buttonPushed;
    readonly ITexture _buttonHighlight;

    public event Action<int> RaceSelected;
    public event Action Cancelled;

    public RaceSelectionDialogFrame(GodotUiTextureResolver textures, War3UiSkin skin)
        : base("RaceSelectionDialog")
    {
        ITexture panelBackground = textures.Resolve(skin.Get("EscMenuBackground",
            @"UI\Widgets\EscMenu\Human\human-options-menu-background.blp"));
        ITexture panelBorder = textures.Resolve(skin.Get("EscMenuBorder",
            @"UI\Widgets\EscMenu\Human\human-options-menu-border.blp"));
        _buttonBackground = textures.Resolve(skin.Get("EscMenuButtonBackground",
            @"UI\Widgets\EscMenu\Human\human-options-menu-background.blp"));
        _buttonBorder = textures.Resolve(skin.Get("EscMenuButtonBorder",
            @"UI\Widgets\EscMenu\Human\human-options-button-border-up.blp"));
        _buttonPushed = textures.Resolve(skin.Get("EscMenuButtonPushedBackground",
            @"UI\Widgets\EscMenu\Human\human-options-menu-background.blp"));
        _buttonHighlight = textures.Resolve(skin.Get("EscMenuButtonMouseOverHighlight",
            @"UI\Widgets\EscMenu\Human\human-options-button-highlight.blp"));

        _overlay = new SolidColorFrame("RaceSelectionOverlay", new Vector4(0, 0, 0, .65f));
        _panel = new BackdropFrame("RaceSelectionPanel")
        {
            BackgroundTexture = panelBackground,
            EdgeTexture = panelBorder,
            CornerSize = 48f,
            BackgroundSize = 164f,
            Insets = new UiRect(10f, 10f, 10f, 10f),
            TileBackground = true,
            MouseTransparent = true,
        };
        _title = Text("RaceSelectionTitle", "选择玩家种族", 27f,
            new Vector4(1f, .84f, .12f, 1f));
        _description = Text("RaceSelectionDescription", "下一步选择电脑难度", 15f,
            new Vector4(.96f, .94f, .86f, 1f));
        AddChild(_overlay);
        AddChild(_panel);
        AddChild(_title);
        AddChild(_description);
        AddButton("HumanRaceButton", "人族", 1, null);
        AddButton("OrcRaceButton", "兽族", 2, null);
        AddButton("UndeadRaceButton", "不死族", 8, null);
        AddButton("NightElfRaceButton", "暗夜精灵", 4, null);
        AddButton("CancelRaceButton", "返回", 0, () => Cancelled?.Invoke());
        Visible = false;
    }

    void AddButton(string name, string label, int racePreference, Action action)
    {
        var plate = new BackdropFrame(name + "Plate")
        {
            BackgroundTexture = _buttonBackground,
            EdgeTexture = _buttonBorder,
            CornerSize = 14f,
            BackgroundSize = 328f,
            Insets = new UiRect(5f, 5f, 5f, 5f),
            TileBackground = true,
            MouseTransparent = true,
        };
        var button = new ButtonFrame(name)
        {
            PushedTexture = _buttonPushed,
            ActiveHighlight = _buttonHighlight,
        };
        button.Clicked += mouseButton =>
        {
            if (mouseButton != 0) return;
            if (racePreference != 0) RaceSelected?.Invoke(racePreference);
            else action?.Invoke();
        };
        var text = Text(name + "Label", label, 18f, new Vector4(1f, .84f, .12f, 1f));
        _buttonPlates.Add(plate);
        _buttons.Add(button);
        _labels.Add(text);
        AddChild(plate);
        AddChild(button);
        AddChild(text);
    }

    static StringFrame Text(string name, string text, float size, Vector4 color) => new(name)
    {
        Text = text,
        FontSize = size,
        Color = color,
        Alignment = TextAlignment.Center,
        VerticalAlignment = TextVerticalAlignment.Middle,
        FontShadowColor = new Vector4(0, 0, 0, 1),
        FontShadowOffset = new Vector2(2, 2),
        MouseTransparent = true,
    };

    public void Show() => Visible = true;
    public void Hide() => Visible = false;

    public void Layout(float viewportWidth, float viewportHeight)
    {
        RenderBounds = new UiRect(0, 0, viewportWidth, viewportHeight);
        _overlay.RenderBounds = RenderBounds;
        float width = MathF.Min(PanelWidth, viewportWidth - 40f);
        float height = MathF.Min(PanelHeight, viewportHeight - 40f);
        float x = (viewportWidth - width) * .5f;
        float y = (viewportHeight - height) * .5f;
        _panel.RenderBounds = new UiRect(x, y, width, height);
        _title.RenderBounds = new UiRect(x + 40f, y + 36f, width - 80f, 42f);
        _description.RenderBounds = new UiRect(x + 40f, y + 78f, width - 80f, 28f);
        for (int i = 0; i < _buttons.Count; i++)
        {
            float buttonX = x + (width - ButtonWidth) * .5f;
            float buttonY = y + 116f + i * 55f;
            var bounds = new UiRect(buttonX, buttonY, ButtonWidth, ButtonHeight);
            _buttonPlates[i].RenderBounds = bounds;
            _buttons[i].RenderBounds = bounds;
            _labels[i].RenderBounds = bounds;
        }
    }
}