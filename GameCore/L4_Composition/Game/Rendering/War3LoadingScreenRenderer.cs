using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using OpenWC3.Game.UI;
using OpenWC3.Game.UI.Generated;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game;

public sealed partial class War3LoadingScreenRenderer : Control
{
    private const float DesignWidth = War3UiViewport.BaseWidth;
    private const float DesignHeight = War3UiViewport.BaseHeight;
    private const float LoadingBarBottomMargin = 52f;
    private const float PanelWidth = 460f;

    private static readonly float[] TextSizes = { 26f, 20f, 16f };

    private readonly CanvasLayer _layer;
    private readonly MapSource _map;
    private readonly War3UiViewport _viewport = new();
    private readonly Dictionary<string, ITexture> _background = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _backgroundPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _debugLayout =
        System.Environment.GetEnvironmentVariable("OPENWC3_LOADING_DEBUG") is "1" or "2";
    private readonly bool _debugEveryFrame =
        System.Environment.GetEnvironmentVariable("OPENWC3_LOADING_DEBUG") == "2";
    private string _lastLayout;

    private UiRoot _root;
    private LoadingScreenBinding _loading;
    private GodotUiRenderDevice _device;
    private GodotUiTextureResolver _textures;
    private readonly string _title, _subtitle, _body;
    private string _barLabel;
    private string _stage;
    private readonly string _backgroundModel;
    private readonly ITexture _thumbnail;
    private const float FadeSeconds = 0.5f;

    private float _progress;
    private float _remaining;
    private bool _finishing;
    private int _settleFrames;
    private float _fade;
    private bool _finished;

    public War3LoadingScreenRenderer(Node parent, MapSource map, int raceId = 0, float seconds = 0f)
    {
        _remaining = Mathf.Max(0f, seconds);
        _map = map;

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        string race = RaceName(raceId, map);
        War3UiSkin skin = War3UiSkin.Load(race);
        GodotWar3FontFace font = GodotWar3FontFace.Load(skin);
        _textures = new GodotUiTextureResolver { Skin = skin };

        _title = Strip(map?.Info?.LoadingScreenTitle) ?? Strip(map?.Name) ?? "";
        _subtitle = Strip(map?.Info?.LoadingScreenSubtitle) ?? "";
        _body = Strip(map?.Info?.LoadingScreenText) ?? Strip(map?.Info?.DescriptionRef) ?? "";

        _backgroundModel = ResolveBackgroundModel(map, skin);
        _thumbnail = MapThumbnailCache.Get(map);
        BuildFrameTree();
        CollectBackground(race, _backgroundModel);

        _device = new GodotUiRenderDevice(this, font.Font)
        {
            LineSizeResolver = font.PixelSizeForLine,
        };

        _layer = new CanvasLayer { Name = "War3LoadingScreen", Layer = 50 };
        parent.AddChild(_layer);
        _layer.AddChild(this);
    }

    private static string Strip(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : UiText.Strip(War3Strings.Resolve(value), false);

    private void BuildFrameTree()
    {
        _root = new UiRoot();
        if (System.Environment.GetEnvironmentVariable("OPENWC3_FDF_LOADING") == "0")
        {
            GD.Print("[LOADING] FDF 已按 OPENWC3_FDF_LOADING=0 停用，走直绘");
            return;
        }

        var factory = new FdfUiFactory(_textures, FdfSource.Read);
        _loading = LoadingScreenBinding.Load(factory);
        if (_loading == null)
        {
            GD.PushWarning("[LOADING] FDF 帧树不可用，改用直绘");
            return;
        }

        _root.AddChild(_loading.Loading);
        _loading.Loading.SetAllPoints();
        if (_loading.LoadingMeleePanel != null) _loading.LoadingMeleePanel.Visible = false;
        if (_loading.LoadingCustomPanel != null) _loading.LoadingCustomPanel.Visible = true;

        if (_loading.LoadingBarText != null) _loading.LoadingBarText.IgnoreUiFontScale = true;
        if (_loading.LoadingTitleText != null) _loading.LoadingTitleText.IgnoreUiFontScale = true;
        if (_loading.LoadingSubtitleText != null) _loading.LoadingSubtitleText.IgnoreUiFontScale = true;
        if (_loading.LoadingText != null) _loading.LoadingText.IgnoreUiFontScale = true;

        _barLabel = _loading.LoadingBarText?.Text ?? "";
        if (_loading.LoadingTitleText != null) _loading.LoadingTitleText.Text = _title;
        if (_loading.LoadingSubtitleText != null) _loading.LoadingSubtitleText.Text = _subtitle;
        if (_loading.LoadingText != null) _loading.LoadingText.Text = _body;
    }

    private static string ResolveBackgroundModel(MapSource map, War3UiSkin skin)
    {
        string fallback = skin.Get("LoadingMeleeBackground",
            @"UI\Glues\Loading\Multiplayer\Load-Multiplayer-Human.mdl");
        if (!string.IsNullOrWhiteSpace(map?.Info?.LoadingScreenModel))
            return map.Info.LoadingScreenModel;
        int campaign = map?.Info?.CampaignBackground ?? -1;
        if (campaign < 0) return fallback;

        string table = Assets.ReadText(Assets.Res(@"UI\WorldEditData.txt"));
        if (string.IsNullOrEmpty(table)) return fallback;
        string key = campaign.ToString("00");
        bool inSection = false;
        foreach (string raw in table.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('['))
            {
                inSection = line.Equals("[LoadingScreens]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inSection || !line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) continue;
            string[] fields = line[(key.Length + 1)..].Split(',');
            string path = fields.Length > 3 ? fields[3].Trim().Trim('"') : fallback;
            return string.IsNullOrWhiteSpace(path) ? fallback : path;
        }
        return fallback;
    }

    private void CollectBackground(string race, string model)
    {
        string folder = model.Replace('/', '\\');
        int slash = folder.LastIndexOf('\\');
        if (slash > 0) folder = folder[..(slash + 1)];

        var candidates = new List<(string Key, string Path)>();
        foreach (string tile in ModelTextures(model))
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(tile);
            string key = TileKey(stem);
            if (key == null || candidates.Exists(entry => entry.Key == key)) continue;
            candidates.Add((key, tile));
        }
        if (candidates.Count < 4)
        {
            candidates.Clear();
            candidates.Add(("TL", folder + "Loading-TopLeft.png"));
            candidates.Add(("TR", folder + $"Loading-{race}-TopRight.png"));
            candidates.Add(("BL", folder + "Loading-BotLeft.png"));
            candidates.Add(("BR", folder + $"Loading-{race}-BotRight.png"));
        }

        foreach ((string key, string path) in candidates)
        {
            ITexture texture = _textures.Resolve(path);
            if (texture == null) continue;
            _background[key] = texture;
            _backgroundPaths[key] = path;
        }
    }

    private static string TileKey(string stem)
    {
        if (stem.EndsWith("TopLeft", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("TL", StringComparison.OrdinalIgnoreCase))
            return "TL";
        if (stem.EndsWith("TopRight", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("TR", StringComparison.OrdinalIgnoreCase))
            return "TR";
        if (stem.EndsWith("BotLeft", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("BL", StringComparison.OrdinalIgnoreCase))
            return "BL";
        if (stem.EndsWith("BotRight", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("BR", StringComparison.OrdinalIgnoreCase))
            return "BR";
        return null;
    }

    private static IEnumerable<string> ModelTextures(string model)
    {
        string scene = PathResolve.ModelScene(model);
        if (scene == null) yield break;
        string text = Assets.ReadText(scene);
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (string line in text.Split('\n'))
        {
            int marker = line.IndexOf("type=\"Texture2D\"", StringComparison.Ordinal);
            if (marker < 0) continue;
            int path = line.IndexOf("path=\"res://", StringComparison.Ordinal);
            if (path < 0) continue;
            int start = path + "path=\"res://".Length;
            int end = line.IndexOf('"', start);
            if (end <= start) continue;
            yield return line[start..end];
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

    // 加载完成: 进度直接到 100%, 世界画出来后再淡出
    public bool Finished => _finished;

    public void Report(float progress, string stage)
    {
        _progress = Mathf.Max(_progress, Mathf.Clamp(progress, 0f, 1f));
        if (!string.IsNullOrWhiteSpace(stage)) _stage = stage;
        QueueRedraw();
    }

    public void Finish()
    {
        if (_finishing) return;
        _finishing = true;
        _settleFrames = 0;
        _progress = 1f;
        QueueRedraw();
    }

    public void Fail(string stage)
    {
        _stage = string.IsNullOrWhiteSpace(stage) ? "加载失败" : $"加载失败：{stage}";
        _progress = 0f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_root == null || _device == null) return;
        if (_debugLayout)
            GD.Print($"[loading-draw] progress={_progress:0.##} stage='{_stage}'");

        Vector2 size = GetViewportRect().Size;
        _viewport.Update((int)size.X, (int)size.Y);

        _device.Begin(DesignWidth, DesignHeight, _viewport.ScaleX, _viewport.ScaleY, _viewport.PixelOffsetX);
        DrawBackgroundQuads();
        _root.UpdateViewport(DesignWidth, DesignHeight);
        _root.Position();

        UiRect bar = LoadBarRect();
        DrawBar(bar);
        if (_loading == null)
        {
            DrawDirectTexts(bar);
            _device.End();
            return;
        }

        DrawThumbnail();
        LayoutTexts(bar);
        LayoutBarLabel(bar);
        DumpLayout(bar);

        _root.Render(_device);
        _device.End();
    }

    private void DrawThumbnail()
    {
        if (_thumbnail == null || _loading?.MinimapImage == null) return;
        UiRect rect = _loading.MinimapImage.RenderBounds;
        if (rect.Width <= 0f || rect.Height <= 0f) return;
        _device.DrawTexturedQuad(rect, _thumbnail, System.Numerics.Vector4.One);
    }

    private void DrawBackgroundQuads()
    {
        var white = System.Numerics.Vector4.One;
        if (_background.Count >= 4)
        {
            float half = DesignWidth * 0.5f;
            float mid = DesignHeight * 0.5f;
            _device.DrawTexturedQuad(new UiRect(0, 0, half, mid), _background["TL"], white);
            _device.DrawTexturedQuad(new UiRect(half, 0, half, mid), _background["TR"], white);
            _device.DrawTexturedQuad(new UiRect(0, mid, half, mid), _background["BL"], white);
            _device.DrawTexturedQuad(new UiRect(half, mid, half, mid), _background["BR"], white);
        }
        else
        {
            _device.DrawTexturedQuad(new UiRect(0, 0, DesignWidth, DesignHeight),
                _device.WhiteTexture, new System.Numerics.Vector4(0.015f, 0.025f, 0.035f, 1f));
        }

        _device.DrawTexturedQuad(new UiRect(0, DesignHeight - 92f, DesignWidth, 92f),
            _device.WhiteTexture, new System.Numerics.Vector4(0f, 0f, 0f, 0.38f));
    }

    private void DrawDirectTexts(UiRect bar)
    {
        float y = Mathf.Max(80f, bar.Y - 240f);
        float x = DesignWidth - PanelWidth - 32f;
        float[] sizes = { 28f, 20f, 16f };
        string[] texts = { _title, _subtitle, _body };
        for (int i = 0; i < texts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(texts[i])) continue;
            string wrapped = Wrap(texts[i], PanelWidth, sizes[i]);
            float height = TextHeight(wrapped, sizes[i]);
            _device.DrawText(new UiRect(x, y, PanelWidth, height), wrapped, sizes[i],
                System.Numerics.Vector4.One, TextAlignment.Left,
                verticalAlign: TextVerticalAlignment.Top);
            y += height + 10f;
        }

        string status = string.IsNullOrWhiteSpace(_stage) ? _barLabel : _stage;
        if (string.IsNullOrWhiteSpace(status)) return;
        _device.DrawText(bar, status, 18f, System.Numerics.Vector4.One,
            TextAlignment.Center, verticalAlign: TextVerticalAlignment.Middle);
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
        ITexture glass = _textures.Resolve(@"Textures\Loading-BarGlass.blp");
        ITexture border = _textures.Resolve(@"UI\Glues\Loading\LoadBar\Loading-BarBorder.blp");

        if (background != null)
            DrawTiledSegment(channel, background, channel.Height);
        if (fill != null && _progress > 0f)
            DrawTiledSegment(
                new UiRect(channel.X, channel.Y, channel.Width * _progress, channel.Height),
                fill, channel.Height);
        if (glass != null)
            _device.DrawTexturedQuad(outer, glass, new System.Numerics.Vector4(1f, 1f, 1f, 0.72f));
        if (border != null)
            _device.DrawTexturedQuad(outer, border, System.Numerics.Vector4.One);
    }

    // 客户端按条高的宽度切段, 每段铺满整张贴图 (u 0..1), 不是按纹理像素 1:1 平铺
    private void DrawTiledSegment(UiRect rect, ITexture texture, float tileWidth)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || tileWidth <= 0f) return;
        for (float offset = 0f; offset < rect.Width; offset += tileWidth)
        {
            float segment = Math.Min(tileWidth, rect.Width - offset);
            _device.DrawTexturedQuad(
                new UiRect(rect.X + offset, rect.Y, segment, rect.Height),
                texture, System.Numerics.Vector4.One,
                new UiRect(0f, 0f, segment / tileWidth, 1f));
        }
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
        string.IsNullOrEmpty(text)
            ? 0f
            : text.Split('\n').Sum(line => Math.Max(size * 1.5f,
                _device.MeasureTextHeight(string.IsNullOrEmpty(line) ? "Ag国" : line, size))) + 8f;

    // 逐元件位置日志, 输出格式与客户端 War3LoadingScreenRenderer.DumpLayout 完全一致.
    // 两侧设 OPENWC3_LOADING_DEBUG=1 各跑一次, 逐行 diff 即可定位偏移来自哪个元件.
    private void DumpLayout(UiRect bar)
    {
        if (!_debugLayout || _loading == null) return;
        StringFrame[] frames =
        {
            _loading.LoadingTitleText, _loading.LoadingSubtitleText, _loading.LoadingText,
        };
        string[] names = { "title", "subtitle", "body" };
        string mode = _background.Count >= 4 ? "quads" : "solid";

        var signature = new StringBuilder();
        signature.Append(_viewport.FramebufferWidth).Append('x').Append(_viewport.FramebufferHeight)
            .Append('|').Append(mode).Append('|').Append(bar.Y.ToString("0.##"));
        foreach (StringFrame frame in frames)
            signature.Append('|').Append(frame?.FontSize.ToString("0.##")).Append(':')
                .Append(frame?.Text ?? "");
        signature.Append('|').Append(_loading.LoadingBarText?.Text ?? "");
        string current = signature.ToString();
        if (!_debugEveryFrame && current == _lastLayout) return;
        _lastLayout = current;

        GD.Print($"[loading-layout] fb={_viewport.FramebufferWidth}x{_viewport.FramebufferHeight}"
            + $" active={_viewport.ActivePixelWidth} offset={_viewport.PixelOffsetX}"
            + $" scale={_viewport.ScaleX:0.####}x{_viewport.ScaleY:0.####}"
            + $" design={DesignWidth:0}x{DesignHeight:0}");
        GD.Print($"[loading-layout] bg mode={mode}");
        foreach (string key in new[] { "TL", "TR", "BL", "BR" })
        {
            if (!_background.TryGetValue(key, out ITexture texture)) continue;
            _backgroundPaths.TryGetValue(key, out string path);
            GD.Print($"[loading-layout] bg {key} tex={texture.Width}x{texture.Height} path={path}");
        }
        if (mode == "quads")
        {
            float half = DesignWidth * 0.5f, top = DesignHeight * 2f / 3f;
            float bottom = DesignHeight - top;
            GD.Print("[loading-layout] bg rects"
                + $" TL=0,0,{half:0.##},{top:0.##} TR={half:0.##},0,{half:0.##},{top:0.##}"
                + $" BL=0,{top:0.##},{half:0.##},{bottom:0.##}"
                + $" BR={half:0.##},{top:0.##},{half:0.##},{bottom:0.##}");
        }

        float insetX = bar.Height * 0.5f, insetY = bar.Height * 0.2f;
        var channel = new UiRect(bar.X + insetX, bar.Y + insetY,
            Math.Max(0f, bar.Width - insetX * 2), bar.Height * 0.55f);
        GD.Print($"[loading-layout] bar rect={Rect(bar)}"
            + $" fdf={Rect(_loading.LoadingBar?.RenderBounds ?? default)}");
        GD.Print($"[loading-layout] channel rect={Rect(channel)} progress={_progress:0.###}");

        StringFrame label = _loading.LoadingBarText;
        if (label != null)
        {
            string first = FirstLine(label.Text);
            float width = first.Length == 0 ? 0f : _device.MeasureTextWidth(first, label.FontSize);
            GD.Print($"[loading-layout] label size={label.FontSize:0.##}"
                + $" px={_device.TextPixelSize(label.Text, label.FontSize)}"
                + $" rect={Rect(label.RenderBounds)} lines={LineCount(label.Text)}"
                + $" w={width:0.##} text='{Clip(label.Text)}'");
        }

        for (int i = 0; i < frames.Length; i++)
        {
            StringFrame frame = frames[i];
            if (frame == null) continue;
            string first = FirstLine(frame.Text);
            float width = first.Length == 0 ? 0f : _device.MeasureTextWidth(first, frame.FontSize);
            GD.Print($"[loading-layout] text {names[i]}"
                + $" size={frame.FontSize:0.##}"
                + $" px={_device.TextPixelSize(frame.Text, frame.FontSize)}"
                + $" rect={Rect(frame.RenderBounds)}"
                + $" lines={LineCount(frame.Text)} h={TextHeight(frame.Text, frame.FontSize):0.##}"
                + $" w={width:0.##} text='{Clip(frame.Text)}'");
        }
    }

    private static string Rect(UiRect rect) =>
        $"{rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##}";

    private static string FirstLine(string text) =>
        string.IsNullOrEmpty(text) ? "" : text.Split('\n')[0];

    private static int LineCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;

    private static string Clip(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string flat = text.Replace("\n", "\\n");
        return flat.Length <= 60 ? flat : flat[..60] + "…";
    }

    public void FrameSeconds(float delta)
    {
        if (_finished) return;
        if (_remaining > 0f) _remaining = Mathf.Max(0f, _remaining - delta);
        if (!_finishing || _remaining > 0f) return;
        // 世界刚建好时头几帧还是黑的, 等它画出来再淡出, 否则会闪一下
        if (_settleFrames < 3) { _settleFrames++; return; }

        _fade += delta;
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - _fade / FadeSeconds, 0f, 1f));
        QueueRedraw();
        if (_fade < FadeSeconds) return;

        _finished = true;
        _layer?.QueueFree();
        QueueFree();
    }
}
