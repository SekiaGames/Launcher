using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

// 资源条 Frame: 顶部菜单按钮 (任务/菜单/盟友/聊天) + 资源图标数值 (金/木/食物)
// 对齐 War3GameUiRenderer.DrawTopBars, 经 IUiRenderDevice 绘制不直接 GL
// 坐标: 设计单位 (1024x768 = FDF 0.8x0.6)
public sealed class TopBarFrame : UiContainer
{
    const float ButtonWidth = 108.8f, ButtonHeight = 28.16f;
    const float ResourceX = 590.96f - 28f * 0.3f - 28f * 0.08f - 28f * 0.1f;
    static readonly float[] IconOffsets = { 0, 110, 221, 330 };
    static readonly float[] ResourceWidths = { 108f, 108f, 100f, 80f };
    static readonly string[] MenuLabels = { "任务", "菜单", "盟友", "聊天" };
    static readonly bool[] MenuInScope = { false, true, false, false };

    readonly StringFrame[] _menuLabels = new StringFrame[4];
    readonly ButtonFrame[] _menuButtons = new ButtonFrame[4];
    readonly TextureFrame[] _resourceIcons = new TextureFrame[4];
    readonly StringFrame[] _resourceValues = new StringFrame[4];
    float _classicOffsetX;
    readonly StringFrame _timeText;
    readonly StringFrame _fpsText;
    long _fpsLastTimestamp;
    double _fpsElapsed;
    int _fpsFrames;
    int _timeHour, _timeMinute;
    System.Globalization.CultureInfo _timeCulture;

    public ButtonFrame MenuButton => _menuButtons[1];
    public ButtonFrame QuestButton => _menuButtons[0];
    /// <summary>原版 UpperButtonBarAlliesButton 的手写回退路径。</summary>
    public ButtonFrame AlliesButton => _menuButtons[2];
    internal string TimeText=>_timeText.Text;

    public void SetChromeVisible(bool visible)
    {
        for (int i = 0; i < _menuButtons.Length; i++) _menuButtons[i].Visible = visible && MenuInScope[i];
        for (int i = 0; i < _menuLabels.Length; i++) _menuLabels[i].Visible = visible && MenuInScope[i];
        // ResourceBar.fdf already supplies the three icons and resource values.
        // Hide the fallback together when that frame is active.
        foreach(TextureFrame icon in _resourceIcons)icon.Visible=visible&&icon!=_resourceIcons[3];
        foreach(StringFrame value in _resourceValues)value.Visible=visible;
        _timeText.Visible=true;
    }

    public TopBarFrame(IUiTextureResolver textures, War3UiSkin skin) : base("TopBar")
    {
        ITexture buttonTexture = textures.Resolve(skin.Get("UpperMenuButtonTexture",
            @"UI\Widgets\Console\Human\human-console-buttonstates2.blp"));
        // 4 状态 UV: FDF TexCoord(minU,maxU,minV,maxV) -> UiRect(X,Y,W,H); V 方向 4 等分
        var normalUV = new UiRect(0, 0, 0.6640625f, 0.171875f);
        var pushedUV = new UiRect(0, 0.25f, 0.6640625f, 0.171875f);
        var disabledUV = new UiRect(0, 0.5f, 0.6640625f, 0.171875f);
        var highlightUV = new UiRect(0, 0.75f, 0.6640625f, 0.171875f);
        ITexture[] resourceIcons =
        {
            textures.Resolve(skin.Get("GoldIcon", @"UI\Feedback\Resources\ResourceGold.blp")),
            textures.Resolve(skin.Get("LumberIcon", @"UI\Feedback\Resources\ResourceLumber.blp")),
            textures.Resolve(skin.Get("SupplyIcon", @"UI\Feedback\Resources\ResourceSupply.blp")),
        };

        for (int i = 0; i < MenuLabels.Length; i++)
        {
            var btn = new ButtonFrame($"MenuBtn{i}")
            {
                Width = ButtonWidth, Height = ButtonHeight,
                NormalTexture = buttonTexture, NormalUV = normalUV,
                PushedTexture = buttonTexture, PushedUV = pushedUV,
                HighlightTexture = buttonTexture, HighlightUV = highlightUV,
                DisabledTexture = buttonTexture, DisabledUV = disabledUV,
                Disabled = i > 2,
                TooltipTitle = MenuLabels[i],
                TooltipBody = i switch
                {
                    0 => "打开任务日志。",
                    1 => "打开游戏菜单。",
                    2 => "查看盟友状态。",
                    _ => "打开聊天窗口。",
                },
            };
            var label = new StringFrame($"MenuLbl{i}")
            {
                Text = MenuLabels[i],
                FontSize = 12,
                Color = i <= 2 ? Vector4.One : new(165f / 255f, 165f / 255f, 165f / 255f, 1f),
                Alignment = TextAlignment.Center,
                VerticalAlignment = TextVerticalAlignment.Middle,
                MouseTransparent = true,
            };
            _menuLabels[i] = label;
            _menuButtons[i] = btn;
            AddChild(btn);
            AddChild(label);
        }

        for (int i = 0; i < 4; i++)
        {
            var icon = new TextureFrame($"ResIcon{i}")
            {
                Texture = i < 3 ? resourceIcons[i] : null,
                Width = 21, Height = 21,
                Visible = i < 3,
                MouseTransparent = false,
            };
            var value = new StringFrame($"ResVal{i}")
            {
                FontSize = 13,
                Alignment = TextAlignment.Right,
                VerticalAlignment = TextVerticalAlignment.Middle,
                MouseTransparent = false,
                Color = i switch
                {
                    0 => new Vector4(1f, 220f / 255f, 72f / 255f, 1f),
                    1 => new Vector4(116f / 255f, 232f / 255f, 118f / 255f, 1f),
                    _ => Vector4.One,
                },
            };
            _resourceIcons[i] = icon;
            _resourceValues[i] = value;
            AddChild(icon);
            AddChild(value);
        }
        _timeText = new StringFrame("TimeText")
        {
            FontSize = 12,
            Alignment = TextAlignment.Center,
            Color = Vector4.One,
            Visible = false,
        };
        AddChild(_timeText);
        _fpsText = new StringFrame("FpsText")
        {
            Text = "FPS: --",
            FontSize = 10,
            Alignment = TextAlignment.Center,
            VerticalAlignment = TextVerticalAlignment.Middle,
            Color = new Vector4(.8f, .8f, .8f, 1f),
            MouseTransparent = true,
        };
        AddChild(_fpsText);
    }

    // 视口变化 -> 重算子帧位置
    public void Layout(float viewportWidth, float viewportHeight)
    {
        _classicOffsetX = 0f;
        for (int i = 0; i < MenuLabels.Length; i++)
        {
            float x = _classicOffsetX + 2.56f + i * ButtonWidth;
            Children[i * 2].RenderBounds = new UiRect(x, 0, ButtonWidth, ButtonHeight);
            Children[i * 2 + 1].RenderBounds = new UiRect(x, 0, ButtonWidth, ButtonHeight);
        }
        float resourceX = _classicOffsetX + ResourceX;
        for (int i = 0; i < 4; i++)
        {
            float x = resourceX + IconOffsets[i];
            _resourceIcons[i].RenderBounds = new UiRect(x + 2, 4, 21, 21);
            _resourceValues[i].RenderBounds = new UiRect(x + 24, 4, ResourceWidths[i] - 26, 21);
        }
        // 原版种族 TimeOfDayIndicator MDX 由场景 Sprite 层绘制；这里仅补
        // 精确数字时间，位置对齐模型中央读数区域。
        _timeText.RenderBounds = new UiRect(_classicOffsetX + 462, 4, 100, 21);
        LayoutFps(_resourceValues[3].RenderBounds);
    }

    public void LayoutFps(UiRect upkeepBounds) =>
        _fpsText.RenderBounds = new UiRect(upkeepBounds.X, upkeepBounds.Bottom + 3,
            upkeepBounds.Width, 14);

    public void UpdateFps()
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double elapsed = _fpsLastTimestamp == 0 ? 0 :
            (now - _fpsLastTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency;
        _fpsLastTimestamp = now;
        // Discard initial/foreground-resume gaps instead of displaying a stale average.
        if (elapsed <= 0 || elapsed > 2)
        {
            _fpsElapsed = 0;
            _fpsFrames = 0;
            _fpsText.Text = "FPS: --";
            return;
        }
        _fpsElapsed += elapsed;
        _fpsFrames++;
        if (_fpsElapsed < .5) return;
        _fpsText.Text = $"FPS: {Math.Round(_fpsFrames / _fpsElapsed):0}";
        _fpsElapsed = 0;
        _fpsFrames = 0;
    }

    internal static string FormatTime(double timeOfDay)
    {
        double wrapped=(timeOfDay%24+24)%24;
        return $"{(int)wrapped:00}:{(int)((wrapped%1)*60):00}";
    }

    public void UpdateTime(double timeOfDay)
    {
        double wrapped=(timeOfDay%24+24)%24;
        int hour=(int)wrapped,minute=(int)((wrapped%1)*60);
        var culture=System.Globalization.CultureInfo.CurrentCulture;
        // Mutable cultures may change number formatting in place. Keep those
        // on the original formatting path; normal read-only cultures are cached.
        if(culture.IsReadOnly&&ReferenceEquals(culture,_timeCulture)
            &&hour==_timeHour&&minute==_timeMinute)return;
        _timeText.Text=FormatTime(timeOfDay);
        _timeHour=hour;_timeMinute=minute;_timeCulture=culture;
    }

    public void UpdateResources(int gold, int lumber, int foodUsed, int foodCap, int repairCost = 0)
    {
        _resourceValues[0].Text = gold.ToString();
        _resourceValues[1].Text = lumber.ToString();
        _resourceValues[2].Text = $"{foodUsed}/{foodCap}";
        var (upkeepText, upkeepColor) = foodUsed > 80 ? ("高维修费用", new Vector4(1f, .25f, .25f, 1f))
            : foodUsed > 50 ? ("低维修费用", new Vector4(1f, .78f, .25f, 1f))
            : ("无维修费用", new Vector4(.38f, 1f, .38f, 1f));
        _resourceValues[3].Text = upkeepText;
        _resourceValues[3].Color = upkeepColor;
    }

}
