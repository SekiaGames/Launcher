using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using OpenWC3.Game.UI.Generated;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class FdfLoadingScreen : Control
{
    private const float DesignWidth = War3UiViewport.BaseWidth;
    private const float DesignHeight = War3UiViewport.BaseHeight;
    private const float LoadingBarBottomMargin = 52f;
    private const float PanelWidth = 460f;

    private static readonly float[] TextSizes = { 26f, 20f, 16f };

    private readonly CanvasLayer _layer;
    private readonly War3UiViewport _viewport = new();
    private readonly Dictionary<string, ITexture> _background = new(StringComparer.OrdinalIgnoreCase);

    private UiRoot _root;
    private LoadingScreenBinding _loading;
    private GodotUiRenderDevice _device;
    private GodotUiTextureResolver _textures;
    private readonly string _title, _subtitle, _body;
    private string _barLabel;
    private string _stage;
    private float _progress;
    private float _remaining;
    private bool _built;

    public FdfLoadingScreen(Node parent, MapSource map, int raceId = 0, float seconds = 2.2f)
    {
        _remaining = Mathf.Max(0.4f, seconds);

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        string race = RaceName(raceId, map);
        War3UiSkin skin = War3UiSkin.Load(race);
        GodotWar3FontFace font = GodotWar3FontFace.Load(skin);
        _textures = new GodotUiTextureResolver { Skin = skin };

        _title = Strip(map?.Info?.LoadingScreenTitle) ?? Strip(map?.Name) ?? "";
        _subtitle = Strip(map?.Info?.LoadingScreenSubtitle) ?? "";
        _body = Strip(map?.Info?.LoadingScreenText) ?? Strip(map?.Info?.DescriptionRef) ?? "";

        BuildFrameTree();
        CollectBackground(race, skin);

        _device = new GodotUiRenderDevice(this, font.Font)
        {
            LineSizeResolver = font.PixelSizeForLine,
        };

        _layer = new CanvasLayer { Name = "FdfLoadingScreen", Layer = 50 };
        parent.AddChild(_layer);
        _layer.AddChild(this);
    }

    private static string Strip(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : UiText.Strip(War3Strings.Resolve(value), false);

    private void BuildFrameTree()
    {
        var factory = new FdfUiFactory(_textures, FdfSource.Read);
        _loading = LoadingScreenBinding.Load(factory);
        _root = new UiRoot();
        if (_loading == null) return;

        _root.AddChild(_loading.Loading);
        _loading.Loading.SetAllPoints();
        if (_loading.LoadingMeleePanel != null) _loading.LoadingMeleePanel.Visible = false;
        if (_loading.LoadingCustomPanel != null) _loading.LoadingCustomPanel.Visible = true;

        if (_loading.MinimapImage != null)
        {
            ITexture mini = _textures.Resolve("war3mapMap.blp");
            if (mini != null)
            {
                _loading.MinimapImage.AddChild(new TextureFrame("LoadingMapThumbnail")
                {
                    Texture = mini,
                    MouseTransparent = true,
                });
            }
        }

        _barLabel = _loading.LoadingBarText?.Text ?? "";
        if (_loading.LoadingTitleText != null) _loading.LoadingTitleText.Text = _title;
        if (_loading.LoadingSubtitleText != null) _loading.LoadingSubtitleText.Text = _subtitle;
        if (_loading.LoadingText != null) _loading.LoadingText.Text = _body;
    }

    private void CollectBackground(string race, War3UiSkin skin)
    {
        string model = skin.Get("LoadingMeleeBackground",
            @"UI\Glues\Loading\Multiplayer\Load-Multiplayer-Human.mdl");
        string folder = model.Replace('/', '\\');
        int slash = folder.LastIndexOf('\\');
        if (slash > 0) folder = folder[..(slash + 1)];

        Add("TL", folder + "Loading-TopLeft.png");
        Add("TR", folder + $"Loading-{race}-TopRight.png");
        Add("BL", folder + "Loading-BotLeft.png");
        Add("BR", folder + $"Loading-{race}-BotRight.png");
        return;

        void Add(string key, string path)
        {
            ITexture texture = _textures.Resolve(path);
            if (texture != null) _background[key] = texture;
        }
    }

    private static string RaceName(int raceId, MapSource map)
    {
        int race = raceId;
        if (race <= 0 && map != null)
        {
            foreach (W3iPlayer player in map.Players)
            {
                if (player.Id != 0) continue;
                race = player.Race;
                break;
            }
        }
        return race switch { 2 => "Orc", 3 => "Undead", 4 => "NightElf", _ => "Human" };
    }

    public void SetProgress(float progress, string stage)
    {
        _progress = Mathf.Clamp(progress, 0f, 1f);
        if (!string.IsNullOrWhiteSpace(stage)) _stage = stage;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_root == null || _device == null) return;

        Vector2 size = GetViewportRect().Size;
        _viewport.Update((int)size.X, (int)size.Y);

        _device.Begin(DesignWidth, DesignHeight, _viewport.ScaleX, _viewport.ScaleY, _viewport.PixelOffsetX);
        DrawBackgroundQuads();
        if (!_built)
        {
            _built = true;
            _root.UpdateViewport(DesignWidth, DesignHeight);
            _root.Position();
        }

        UiRect bar = LoadBarRect();
        LayoutTexts(bar);
        DrawBar(bar);
        LayoutBarLabel(bar);

        _root.Render(_device);
        _device.End();
    }

    private void DrawBackgroundQuads()
    {
        var white = System.Numerics.Vector4.One;
        if (_background.Count >= 4)
        {
            float half = DesignWidth * 0.5f;
            float top = DesignHeight * 0.5f;
            _device.DrawTexturedQuad(new UiRect(0, 0, half, top), _background["TL"], white);
            _device.DrawTexturedQuad(new UiRect(half, 0, half, top), _background["TR"], white);
            _device.DrawTexturedQuad(new UiRect(0, top, half, top), _background["BL"], white);
            _device.DrawTexturedQuad(new UiRect(half, top, half, top), _background["BR"], white);
        }
        else
        {
            _device.DrawTexturedQuad(new UiRect(0, 0, DesignWidth, DesignHeight),
                _device.WhiteTexture, new System.Numerics.Vector4(0.015f, 0.025f, 0.035f, 1f));
        }

        _device.DrawTexturedQuad(new UiRect(0, DesignHeight - 92, DesignWidth, 92),
            _device.WhiteTexture, new System.Numerics.Vector4(0f, 0f, 0f, 0.38f));
    }

    private UiRect LoadBarRect()
    {
        float width = Mathf.Min(640f, DesignWidth - 96f);
        float height = width / 8f;
        float bottom = _loading?.LoadingBar != null && _loading.LoadingBar.RenderBounds.Y > 0
            ? _loading.LoadingBar.RenderBounds.Y
            : DesignHeight;
        return new UiRect((DesignWidth - width) * 0.5f,
            Mathf.Max(0f, bottom - height - LoadingBarBottomMargin), width, height);
    }

    private void DrawBar(UiRect outer)
    {
        float insetX = outer.Height * 0.5f;
        float insetY = outer.Height * 0.2f;
        var channel = new UiRect(outer.X + insetX, outer.Y + insetY,
            Math.Max(0f, outer.Width - insetX * 2), outer.Height * 0.55f);

        ITexture background = _textures.Resolve(@"UI\Glues\Loading\LoadBar\Loading-BarBackground.blp");
        ITexture fill = _textures.Resolve(@"UI\Glues\Loading\LoadBar\Loading-BarFill.blp");
        ITexture glass = _textures.Resolve(@"UI\Glues\Loading\LoadBar\Loading-BarGlass.blp");
        ITexture border = _textures.Resolve(@"UI\Glues\Loading\LoadBar\Loading-BarBorder.blp");

        if (background != null)
            _device.DrawTexturedQuadTiled(channel, background,
                System.Numerics.Vector4.One, new UiRect(0, 0, 1, 1));
        if (fill != null && _progress > 0f)
            _device.DrawTexturedQuadTiled(
                new UiRect(channel.X, channel.Y, channel.Width * _progress, channel.Height),
                fill, System.Numerics.Vector4.One, new UiRect(0, 0, 1, 1));
        if (glass != null)
            _device.DrawTexturedQuad(outer, glass, new System.Numerics.Vector4(1f, 1f, 1f, 0.72f));
        if (border != null)
            _device.DrawTexturedQuad(outer, border, System.Numerics.Vector4.One);
    }

    private void LayoutTexts(UiRect bar)
    {
        StringFrame[] frames =
        {
            _loading.LoadingTitleText, _loading.LoadingSubtitleText, _loading.LoadingText,
        };
        string[] originals = { _title, _subtitle, _body };

        float total = 0f;
        for (float scale = 1f; scale >= 0.35f; scale -= 0.05f)
        {
            total = 0f;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null) continue;
                frames[i].FontSize = TextSizes[i] * scale;
                frames[i].Text = Wrap(originals[i], PanelWidth, frames[i].FontSize);
                if (!string.IsNullOrWhiteSpace(frames[i].Text))
                    total += TextHeight(frames[i].Text, frames[i].FontSize) + 10f;
            }
            if (total <= 480f) break;
        }

        float y = Mathf.Max(80f, Mathf.Min(365f, bar.Y - 80f - total));
        float x = DesignWidth - PanelWidth - 32f;
        foreach (StringFrame frame in frames)
        {
            if (frame == null || string.IsNullOrWhiteSpace(frame.Text)) continue;
            float height = TextHeight(frame.Text, frame.FontSize);
            frame.SetPoints.Clear();
            frame.RenderBounds = new UiRect(x, y, PanelWidth, height);
            frame.LayoutValid = true;
            y += height + 10f;
        }
    }

    private void LayoutBarLabel(UiRect bar)
    {
        StringFrame label = _loading.LoadingBarText;
        if (label == null) return;

        string status = string.IsNullOrWhiteSpace(_stage) ? _barLabel : _stage;
        label.FontSize = 18f;
        label.Text = Wrap(status, bar.Width - 32f, label.FontSize);
        while (TextHeight(label.Text, label.FontSize) > bar.Height - 12f && label.FontSize > 10f)
        {
            label.FontSize -= 1f;
            label.Text = Wrap(status, bar.Width - 32f, label.FontSize);
        }
        label.SetPoints.Clear();
        label.RenderBounds = new UiRect(bar.X, bar.Y, bar.Width, bar.Height);
        label.Alignment = TextAlignment.Center;
        label.VerticalAlignment = TextVerticalAlignment.Middle;
        label.LayoutValid = true;
    }

    private string Wrap(string text, float width, float size)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (string element in SplitElements(paragraph))
            {
                if (line.Length > 0
                    && _device.MeasureTextWidth(line + element, size) > width - 8f)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }
                line.Append(element);
            }
            lines.Add(line.ToString());
        }
        return string.Join("\n", lines);
    }

    private static IEnumerable<string> SplitElements(string text)
    {
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) yield return enumerator.GetTextElement();
    }

    private float TextHeight(string text, float size) =>
        string.IsNullOrEmpty(text) ? 0f : text.Split('\n').Length * Mathf.Max(size * 1.5f, size) + 8f;

    public void FrameSeconds(float delta)
    {
        if (_remaining <= 0f) return;
        _remaining -= delta;
        if (_remaining > 0.5f)
        {
            _progress = Mathf.Clamp(1f - _remaining / 2.2f, 0f, 1f);
            QueueRedraw();
            return;
        }
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_remaining / 0.5f, 0f, 1f));
        QueueRedraw();
        if (_remaining > 0f) return;

        _layer?.QueueFree();
        QueueFree();
    }
}
