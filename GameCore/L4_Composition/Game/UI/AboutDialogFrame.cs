using System;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class AboutDialogFrame : UiContainer
{
    internal static class Style
    {
        public const float PanelWidth = 780f;
        public const float PanelHeight = 560f;
        public const float ButtonWidth = 230f;
        public const float ButtonHeight = 44f;
        public const float TitleFontSize = 27f;
        public const float BodyFontSize = 16f;
        public const float ButtonFontSize = 17f;

        public static readonly Vector4 OverlayColor = new(0, 0, 0, .62f);
        public static readonly Vector4 TitleColor = new(1f, .84f, .12f, 1f);
        public static readonly Vector4 BodyColor = new(.96f, .94f, .86f, 1f);
        public static readonly Vector4 NoticeColor = new(1f, .86f, .42f, 1f);
        public static readonly Vector4 ButtonColor = new(1f, .84f, .12f, 1f);
    }

    static readonly int[] FrameRateValues = { 30, 60, 90, 120, 144, 0 };
    static readonly string[] FrameRateLabels = { "30", "60", "90", "120", "144", "不限" };
    static readonly float[] RenderScaleValues = { 0.5f, 0.67f, 0.75f, 0.85f, 1f };
    static readonly string[] RenderScaleLabels = { "0.5x", "0.67x", "0.75x", "0.85x", "1.0x" };

    readonly SolidColorFrame _overlay;
    readonly BackdropFrame _outerPanel;
    readonly StringFrame _title;
    readonly StringFrame _cameraDistanceLabel;
    readonly StringFrame _cameraDistanceValue;
    readonly SliderFrame _cameraDistanceSlider;
    readonly StringFrame _panSensitivityLabel;
    readonly StringFrame _panSensitivityValue;
    readonly SliderFrame _panSensitivitySlider;
    readonly StringFrame _musicVolumeLabel;
    readonly StringFrame _musicVolumeValue;
    readonly SliderFrame _musicVolumeSlider;
    readonly StringFrame _soundVolumeLabel;
    readonly StringFrame _soundVolumeValue;
    readonly SliderFrame _soundVolumeSlider;
    readonly StringFrame _frameRateLabel;
    readonly CheckBoxFrame[] _frameRateToggles;
    readonly StringFrame[] _frameRateOptionLabels;
    readonly StringFrame _renderScaleLabel;
    readonly CheckBoxFrame[] _renderScaleToggles;
    readonly StringFrame[] _renderScaleOptionLabels;
    readonly BackdropFrame _closePlate;
    readonly ButtonFrame _closeButton;
    readonly StringFrame _closeLabel;
    readonly BackdropFrame _exitPlate;
    readonly ButtonFrame _exitButton;
    readonly StringFrame _exitLabel;
    readonly Action<float> _cameraDistanceChanged;
    readonly Action<float> _panSensitivityChanged;
    readonly Action<float> _musicVolumeChanged;
    readonly Action<float> _soundVolumeChanged;
    readonly Action<int> _frameRateChanged;
    readonly Action<float> _renderScaleChanged;
    int _frameRateLimit;
    float _renderScale;

    public event Action Closed;
    public event Action ExitToMenuRequested;

    public AboutDialogFrame(IUiTextureResolver textures, War3UiSkin skin,
        float cameraDistance, float panSensitivity, float musicVolume, float soundVolume,
        int frameRateLimit, float renderScale,
        Action<float> cameraDistanceChanged, Action<float> panSensitivityChanged,
        Action<float> musicVolumeChanged, Action<float> soundVolumeChanged,
        Action<int> frameRateLimitChanged, Action<float> renderScaleChanged)
        : base("AboutDialog")
    {
        _cameraDistanceChanged = cameraDistanceChanged;
        _panSensitivityChanged = panSensitivityChanged;
        _musicVolumeChanged = musicVolumeChanged;
        _soundVolumeChanged = soundVolumeChanged;
        _frameRateChanged = frameRateLimitChanged;
        _renderScaleChanged = renderScaleChanged;
        _frameRateLimit = GameUserSettings.NormalizeFrameRate(frameRateLimit);
        _renderScale = GameUserSettings.NormalizeRenderScale(renderScale);

        ITexture menuBackground = textures.Resolve(skin.Get("EscMenuBackground",
            @"UI\Widgets\EscMenu\Human\human-options-menu-background.blp"));
        ITexture menuBorder = textures.Resolve(skin.Get("EscMenuBorder",
            @"UI\Widgets\EscMenu\Human\human-options-menu-border.blp"));
        ITexture buttonBackground = textures.Resolve(skin.Get("EscMenuButtonBackground",
            @"UI\Widgets\EscMenu\Human\human-options-menu-background.blp"));
        ITexture buttonBorder = textures.Resolve(skin.Get("EscMenuButtonBorder",
            @"UI\Widgets\EscMenu\Human\human-options-button-border-up.blp"));
        ITexture buttonPushed = textures.Resolve(skin.Get("EscMenuButtonPushedBackground",
            @"UI\Widgets\EscMenu\Human\human-options-menu-background.blp"));
        ITexture buttonHighlight = textures.Resolve(skin.Get("EscMenuButtonMouseOverHighlight",
            @"UI\Widgets\EscMenu\Human\human-options-button-highlight.blp"));
        ITexture sliderBackground = textures.Resolve(skin.Get("EscMenuSliderBackground",
            @"UI\Widgets\EscMenu\Human\slider-background.blp"));
        ITexture sliderBorder = textures.Resolve(skin.Get("EscMenuSliderBorder",
            @"UI\Widgets\EscMenu\Human\slider-border.blp"));
        ITexture sliderDisabledBorder = textures.Resolve(skin.Get("EscMenuSliderDisabledBorder",
            @"UI\Widgets\EscMenu\Human\slider-borderdisabled.blp"));
        ITexture sliderThumb = textures.Resolve(skin.Get("EscMenuSliderThumbButton",
            @"UI\Widgets\EscMenu\Human\slider-knob.blp"));
        ITexture sliderDisabledThumb = textures.Resolve(skin.Get("EscMenuSliderDisabledThumbButton",
            @"UI\Widgets\EscMenu\Human\slider-knobdisabled.blp"));
        ITexture radioBackground = textures.Resolve(skin.Get("EscMenuRadioButtonBackground",
            @"UI\Widgets\EscMenu\Human\radiobutton-background.blp"));
        ITexture radioPushed = textures.Resolve(skin.Get("EscMenuRadioButtonPushedBackground",
            @"UI\Widgets\EscMenu\Human\radiobutton-background.blp"));
        ITexture radioDisabled = textures.Resolve(skin.Get("EscMenuRadioButtonDisabledBackground",
            @"UI\Widgets\EscMenu\Human\radiobutton-background-disabled.blp"));
        ITexture radioSelected = textures.Resolve(skin.Get("EscMenuRadioButtonSelectedHighlight",
            @"UI\Widgets\EscMenu\Human\radiobutton-button.blp"));

        _overlay = new SolidColorFrame("AboutOverlay", Style.OverlayColor);
        _outerPanel = new BackdropFrame("AboutOuterPanel")
        {
            BackgroundTexture = menuBackground,
            EdgeTexture = menuBorder,
            CornerSize = 61.44f,
            BackgroundSize = 163.84f,
            Insets = new UiRect(12.8f, 12.8f, 12.8f, 12.8f),
            TileBackground = true,
            MouseTransparent = true,
        };
        _title = Text("AboutTitle", "菜单", Style.TitleFontSize, Style.TitleColor);

        _cameraDistanceLabel = Text("CameraDistanceLabel", "游戏镜头距离",
            Style.BodyFontSize, Style.BodyColor);
        _cameraDistanceValue = Text("CameraDistanceValue", "",
            Style.BodyFontSize, Style.NoticeColor);
        _cameraDistanceSlider = CreateSlider("CameraDistanceSlider",
            GameUserSettings.MinScale, GameUserSettings.MaxScale, cameraDistance,
            sliderBackground, sliderBorder, sliderDisabledBorder, sliderThumb, sliderDisabledThumb);
        _panSensitivityLabel = Text("PanSensitivityLabel", "滑动镜头灵敏度",
            Style.BodyFontSize, Style.BodyColor);
        _panSensitivityValue = Text("PanSensitivityValue", "",
            Style.BodyFontSize, Style.NoticeColor);
        _panSensitivitySlider = CreateSlider("PanSensitivitySlider",
            GameUserSettings.MinScale, GameUserSettings.MaxScale, panSensitivity,
            sliderBackground, sliderBorder, sliderDisabledBorder, sliderThumb, sliderDisabledThumb);

        _musicVolumeLabel = Text("MusicVolumeLabel", "音乐音量",
            Style.BodyFontSize, Style.BodyColor);
        _musicVolumeValue = Text("MusicVolumeValue", "",
            Style.BodyFontSize, Style.NoticeColor);
        _musicVolumeSlider = CreateSlider("MusicVolumeSlider", 0f, 1f, musicVolume,
            sliderBackground, sliderBorder, sliderDisabledBorder, sliderThumb, sliderDisabledThumb);
        _soundVolumeLabel = Text("SoundVolumeLabel", "音效音量",
            Style.BodyFontSize, Style.BodyColor);
        _soundVolumeValue = Text("SoundVolumeValue", "",
            Style.BodyFontSize, Style.NoticeColor);
        _soundVolumeSlider = CreateSlider("SoundVolumeSlider", 0f, 1f, soundVolume,
            sliderBackground, sliderBorder, sliderDisabledBorder, sliderThumb, sliderDisabledThumb);

        _frameRateLabel = Text("FrameRateLabel", "帧率上限",
            Style.BodyFontSize, Style.BodyColor);
        _frameRateToggles = new CheckBoxFrame[FrameRateValues.Length];
        _frameRateOptionLabels = new StringFrame[FrameRateValues.Length];
        for (int index = 0; index < FrameRateValues.Length; index++)
        {
            int fps = FrameRateValues[index];
            var toggle = new CheckBoxFrame($"FrameRate{fps}")
            {
                KeepSquare = true,
                NormalTexture = radioBackground,
                PushedTexture = radioPushed,
                DisabledTexture = radioDisabled,
                CheckHighlightTexture = radioSelected,
                Checked = fps == _frameRateLimit,
            };
            toggle.Clicked += button =>
            {
                if (button == 0) SelectFrameRate(fps);
            };
            _frameRateToggles[index] = toggle;
            _frameRateOptionLabels[index] = Text($"FrameRateLabel{fps}", FrameRateLabels[index],
                Style.BodyFontSize, Style.BodyColor);
        }

        _renderScaleLabel = Text("RenderScaleLabel", "渲染倍率",
            Style.BodyFontSize, Style.BodyColor);
        _renderScaleToggles = new CheckBoxFrame[RenderScaleValues.Length];
        _renderScaleOptionLabels = new StringFrame[RenderScaleValues.Length];
        for (int index = 0; index < RenderScaleValues.Length; index++)
        {
            float scale = RenderScaleValues[index];
            var toggle = new CheckBoxFrame($"RenderScale{index}")
            {
                KeepSquare = true,
                NormalTexture = radioBackground,
                PushedTexture = radioPushed,
                DisabledTexture = radioDisabled,
                CheckHighlightTexture = radioSelected,
                Checked = MathF.Abs(scale - _renderScale) < 0.0001f,
            };
            toggle.Clicked += button =>
            {
                if (button == 0) SelectRenderScale(scale);
            };
            _renderScaleToggles[index] = toggle;
            _renderScaleOptionLabels[index] = Text($"RenderScaleLabel{index}",
                RenderScaleLabels[index], Style.BodyFontSize, Style.BodyColor);
        }

        _closePlate = CreateButtonPlate("AboutClosePlate", buttonBackground, buttonBorder);
        _closeButton = new ButtonFrame("AboutCloseButton")
        {
            PushedTexture = buttonPushed,
            ActiveHighlight = buttonHighlight,
        };
        _closeButton.Clicked += button =>
        {
            if (button == 0) Close();
        };
        _closeLabel = Text("AboutCloseLabel", "返回游戏", Style.ButtonFontSize, Style.ButtonColor);
        _exitPlate = CreateButtonPlate("AboutExitPlate", buttonBackground, buttonBorder);
        _exitButton = new ButtonFrame("AboutExitButton")
        {
            PushedTexture = buttonPushed,
            ActiveHighlight = buttonHighlight,
        };
        _exitButton.Clicked += button =>
        {
            if (button != 0) return;
            Close();
            ExitToMenuRequested?.Invoke();
        };
        _exitLabel = Text("AboutExitLabel", "退出地图", Style.ButtonFontSize, Style.ButtonColor);

        _cameraDistanceSlider.ValueChanged += value =>
        {
            UpdateValues();
            _cameraDistanceChanged?.Invoke(value);
        };
        _panSensitivitySlider.ValueChanged += value =>
        {
            UpdateValues();
            _panSensitivityChanged?.Invoke(value);
        };
        _musicVolumeSlider.ValueChanged += value =>
        {
            UpdateValues();
            _musicVolumeChanged?.Invoke(value);
        };
        _soundVolumeSlider.ValueChanged += value =>
        {
            UpdateValues();
            _soundVolumeChanged?.Invoke(value);
        };
        UpdateValues();

        foreach (StringFrame label in new[]
                 { _cameraDistanceLabel, _panSensitivityLabel, _musicVolumeLabel, _soundVolumeLabel })
            label.Alignment = TextAlignment.Left;
        foreach (StringFrame value in new[]
                 { _cameraDistanceValue, _panSensitivityValue, _musicVolumeValue, _soundVolumeValue })
            value.Alignment = TextAlignment.Right;
        _frameRateLabel.Alignment = TextAlignment.Left;
        _renderScaleLabel.Alignment = TextAlignment.Left;
        foreach (StringFrame label in _frameRateOptionLabels) label.Alignment = TextAlignment.Left;
        foreach (StringFrame label in _renderScaleOptionLabels) label.Alignment = TextAlignment.Left;

        AddChild(_overlay);
        AddChild(_outerPanel);
        AddChild(_title);
        AddChild(_cameraDistanceLabel);
        AddChild(_cameraDistanceValue);
        AddChild(_cameraDistanceSlider);
        AddChild(_panSensitivityLabel);
        AddChild(_panSensitivityValue);
        AddChild(_panSensitivitySlider);
        AddChild(_musicVolumeLabel);
        AddChild(_musicVolumeValue);
        AddChild(_musicVolumeSlider);
        AddChild(_soundVolumeLabel);
        AddChild(_soundVolumeValue);
        AddChild(_soundVolumeSlider);
        AddChild(_frameRateLabel);
        foreach (CheckBoxFrame toggle in _frameRateToggles) AddChild(toggle);
        foreach (StringFrame label in _frameRateOptionLabels) AddChild(label);
        AddChild(_renderScaleLabel);
        foreach (CheckBoxFrame toggle in _renderScaleToggles) AddChild(toggle);
        foreach (StringFrame label in _renderScaleOptionLabels) AddChild(label);
        AddChild(_closePlate);
        AddChild(_closeButton);
        AddChild(_closeLabel);
        AddChild(_exitPlate);
        AddChild(_exitButton);
        AddChild(_exitLabel);
        Visible = false;
    }

    static SliderFrame CreateSlider(string name, float minimum, float maximum, float value,
        ITexture background, ITexture border, ITexture disabledBorder,
        ITexture thumb, ITexture disabledThumb) => new(name)
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = value,
        BackgroundTexture = background,
        BorderTexture = border,
        DisabledBorderTexture = disabledBorder,
        ThumbTexture = thumb,
        DisabledThumbTexture = disabledThumb,
    };

    void SelectFrameRate(int fps)
    {
        _frameRateLimit = GameUserSettings.NormalizeFrameRate(fps);
        for (int index = 0; index < _frameRateToggles.Length; index++)
            _frameRateToggles[index].Checked = FrameRateValues[index] == _frameRateLimit;
        _frameRateChanged?.Invoke(_frameRateLimit);
    }

    void SelectRenderScale(float scale)
    {
        _renderScale = GameUserSettings.NormalizeRenderScale(scale);
        for (int index = 0; index < _renderScaleToggles.Length; index++)
            _renderScaleToggles[index].Checked =
                MathF.Abs(RenderScaleValues[index] - _renderScale) < 0.0001f;
        _renderScaleChanged?.Invoke(_renderScale);
    }

    static BackdropFrame CreateButtonPlate(string name, ITexture background, ITexture border) => new(name)
    {
        BackgroundTexture = background,
        EdgeTexture = border,
        CornerSize = 16f,
        BackgroundSize = 327.68f,
        Insets = new UiRect(6.4f, 6.4f, 6.4f, 6.4f),
        TileBackground = true,
        MouseTransparent = true,
    };

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

    void UpdateValues()
    {
        _cameraDistanceValue.Text = $"{_cameraDistanceSlider.Value:0.00}x";
        _panSensitivityValue.Text = $"{_panSensitivitySlider.Value:0.00}x";
        _musicVolumeValue.Text = $"{_musicVolumeSlider.Value * 100f:0}%";
        _soundVolumeValue.Text = $"{_soundVolumeSlider.Value * 100f:0}%";
    }

    public void Toggle()
    {
        if (Visible) Close();
        else Show();
    }

    public void Show() => Visible = true;

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        Closed?.Invoke();
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        RenderBounds = new UiRect(0, 0, viewportWidth, viewportHeight);
        _overlay.RenderBounds = RenderBounds;

        float width = MathF.Min(Style.PanelWidth, viewportWidth - 40f);
        float height = MathF.Min(Style.PanelHeight, viewportHeight - 40f);
        float x = (viewportWidth - width) * .5f;
        float y = MathF.Max(20f, (viewportHeight - height) * .5f);
        _outerPanel.RenderBounds = new UiRect(x, y, width, height);

        float colW = (width - 204f) * .5f;
        float leftX = x + 90f;
        float rightX = leftX + colW + 24f;
        const float valueW = 92f;

        _title.RenderBounds = new UiRect(x + 145f, y + 52f, width - 290f, 44f);

        _cameraDistanceLabel.RenderBounds = new UiRect(leftX, y + 110f, colW - valueW, 32f);
        _cameraDistanceValue.RenderBounds = new UiRect(leftX + colW - valueW, y + 110f, valueW, 32f);
        _cameraDistanceSlider.RenderBounds = new UiRect(leftX, y + 146f, colW, 34f);
        _panSensitivityLabel.RenderBounds = new UiRect(rightX, y + 110f, colW - valueW, 32f);
        _panSensitivityValue.RenderBounds = new UiRect(rightX + colW - valueW, y + 110f, valueW, 32f);
        _panSensitivitySlider.RenderBounds = new UiRect(rightX, y + 146f, colW, 34f);

        _musicVolumeLabel.RenderBounds = new UiRect(leftX, y + 192f, colW - valueW, 32f);
        _musicVolumeValue.RenderBounds = new UiRect(leftX + colW - valueW, y + 192f, valueW, 32f);
        _musicVolumeSlider.RenderBounds = new UiRect(leftX, y + 228f, colW, 34f);
        _soundVolumeLabel.RenderBounds = new UiRect(rightX, y + 192f, colW - valueW, 32f);
        _soundVolumeValue.RenderBounds = new UiRect(rightX + colW - valueW, y + 192f, valueW, 32f);
        _soundVolumeSlider.RenderBounds = new UiRect(rightX, y + 228f, colW, 34f);

        _frameRateLabel.RenderBounds = new UiRect(leftX, y + 276f, colW, 32f);
        float radioSize = FdfScale.S(0.016f);
        float frameRateCell = (colW * 2f + 24f) / FrameRateValues.Length;
        for (int index = 0; index < FrameRateValues.Length; index++)
        {
            float optionX = leftX + frameRateCell * index;
            _frameRateToggles[index].RenderBounds = new UiRect(optionX, y + 320f, radioSize, radioSize);
            _frameRateOptionLabels[index].RenderBounds = new UiRect(optionX + 28f, y + 314f,
                MathF.Max(40f, frameRateCell - 28f), 32f);
        }

        _renderScaleLabel.RenderBounds = new UiRect(leftX, y + 362f, colW, 32f);
        float renderScaleCell = (colW * 2f + 24f) / RenderScaleValues.Length;
        for (int index = 0; index < RenderScaleValues.Length; index++)
        {
            float optionX = leftX + renderScaleCell * index;
            _renderScaleToggles[index].RenderBounds = new UiRect(optionX, y + 406f, radioSize, radioSize);
            _renderScaleOptionLabels[index].RenderBounds = new UiRect(optionX + 28f, y + 400f,
                MathF.Max(40f, renderScaleCell - 28f), 32f);
        }

        float buttonWidth = MathF.Min(Style.ButtonWidth, (width - 100f) * .5f);
        var close = new UiRect(x + width * .5f + 10f, y + 470f, buttonWidth, Style.ButtonHeight);
        var exit = new UiRect(x + width * .5f - 10f - buttonWidth, close.Y,
            buttonWidth, Style.ButtonHeight);
        _closePlate.RenderBounds = _closeButton.RenderBounds = _closeLabel.RenderBounds = close;
        _exitPlate.RenderBounds = _exitButton.RenderBounds = _exitLabel.RenderBounds = exit;
    }
}