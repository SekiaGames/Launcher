using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using OpenWC3.RHI;
using OpenWC3.UI;
using OpenWC3.War3Formats.Text;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace OpenWC3.Game.UI;

public sealed partial class MapSelectionScreen : Control
{
    public const float DesignWidth = 1024f;
    public const float DesignHeight = 768f;
    const float RowHeight = 42f;
    const float RowGap = 6f;
    const float RowPitch = RowHeight + RowGap;
    const int PageSize = 8;

    readonly List<string> _maps;
    readonly List<MapRowButton> _rows = new();
    readonly UiRoot _root = new("MapSelectionRoot");
    readonly War3UiViewport _uiViewport = new();
    readonly ListViewport _listViewport;
    readonly StringFrame _pageStatus;
    readonly ButtonFrame _previousPage, _nextPage;
    readonly TextureFrame _preview;
    readonly StringFrame _name, _suggestedPlayers, _mapSize, _tileset, _description, _players;
    readonly StringFrame _mapVersion, _compatibilityWarning;
    readonly UiFrame _panel;
    readonly GlueModelLayer _glue;
    readonly ButtonFrame _play;
    readonly RaceSelectionDialogFrame _raceSelection;
    readonly AiDifficultyDialogFrame _difficultySelection;
    readonly GodotUiTextureResolver _textures;
    readonly War3UiSkin _skin;
    readonly GodotUiRenderDevice _device;
    readonly UiEventRouter _router;

    GodotUiTexture _previewTexture;
    int _pageIndex;
    bool _pointerDown;

    public string SelectedMap { get; private set; }
    public int? SelectedRacePreference { get; private set; }
    public MeleeAiPreset SelectedAiPreset { get; private set; } = MeleeAiPreset.Normal;
    public event Action<string, int, MeleeAiPreset> StartRequested;

    public MapSelectionScreen()
    {
        SetAnchorsPreset(LayoutPreset.TopLeft);
        MouseFilter = MouseFilterEnum.Stop;
        _skin = War3UiSkin.Load("Human");
        GodotWar3FontFace font = GodotWar3FontFace.Load(_skin);
        _textures = new GodotUiTextureResolver { Skin = _skin };
        _device = new GodotUiRenderDevice(this, font.Font)
        {
            LineSizeResolver = font.PixelSizeForLine,
        };
        _panel = BuildFdfPanel();
        _maps = new List<string>(ListGmapsFullPaths());
        _maps.Sort(CompareBySize);

        _glue = new GlueModelLayer(this, GlueModelLayer.SkirmishBackground);

        var title = Text("MapSelectionTitle", 28, new NVector4(1f, .86f, .42f, 1f));
        title.Text = "选择地图";
        _panel.AddChild(title);
        Place(title, 400, 42, _panel, 84, 62);
        var subtitle = Text("MapSelectionSubtitle", 12, new NVector4(.72f, .76f, .78f, 1f));
        subtitle.Text = "从地图文件中选择一个场景开始游戏";
        _panel.AddChild(subtitle);
        Place(subtitle, 500, 24, _panel, 86, 101);

        var listPanel = new SolidFrame("MapFilePanel", new NVector4(.025f, .035f, .045f, .88f));
        _panel.AddChild(listPanel);
        Place(listPanel, 396, 520, _panel, 84, 132);
        StringFrame listTitle = AddLabel(listPanel, "MapFileTitle", "地图文件", 17,
            new NVector4(1f, .86f, .42f, 1f), 20, 14, 210, 30);
        _pageStatus = AddLabel(listPanel, "MapPageStatus", "", 11,
            new NVector4(.62f, .69f, .72f, 1f), 170, 18, 180, 24, TextAlignment.Right);
        _listViewport = new ListViewport("MapListViewport");
        listPanel.AddChild(_listViewport);
        Place(_listViewport, 356, 390, listPanel, 20, 58);
        BuildRows();
        _previousPage = Button("PreviousMapPageButton", "上一页",
            button => ChangePage(-1, button));
        listPanel.AddChild(_previousPage);
        Place(_previousPage, 108, 40, listPanel, 20, 462);
        _nextPage = Button("NextMapPageButton", "下一页",
            button => ChangePage(1, button));
        listPanel.AddChild(_nextPage);
        Place(_nextPage, 108, 40, listPanel, 248, 462);

        var info = new SolidFrame("MapInfoPanel", new NVector4(.025f, .035f, .045f, .88f));
        _panel.AddChild(info);
        Place(info, 460, 440, _panel, 500, 132);
        AddLabel(info, "MapInfoTitle", "地图信息", 17,
            new NVector4(1f, .86f, .42f, 1f), 20, 14, 420, 30);
        _preview = new TextureFrame("MapPreview");
        info.AddChild(_preview);
        Place(_preview, 190, 190, info, 20, 58);
        _name = AddLabel(info, "MapNameValue", "", 16,
            new NVector4(.95f, .95f, .9f, 1f), 230, 58, 210, 48);
        AddLabel(info, "PlayersLabel", "玩家人数", 11,
            new NVector4(.58f, .65f, .69f, 1f), 230, 118, 90, 20);
        _players = AddLabel(info, "MaxPlayersValue", "-", 12, NVector4.One, 330, 118, 110, 20);
        AddLabel(info, "SuggestedLabel", "建议人数", 11,
            new NVector4(.58f, .65f, .69f, 1f), 230, 150, 90, 20);
        _suggestedPlayers = AddLabel(info, "SuggestedPlayersValue", "-", 12, NVector4.One,
            330, 150, 110, 20);
        AddLabel(info, "SizeLabel", "地图尺寸", 11,
            new NVector4(.58f, .65f, .69f, 1f), 230, 182, 90, 20);
        _mapSize = AddLabel(info, "MapSizeValue", "-", 12, NVector4.One, 330, 182, 110, 20);
        AddLabel(info, "TilesetLabel", "地形", 11,
            new NVector4(.58f, .65f, .69f, 1f), 230, 214, 90, 20);
        _tileset = AddLabel(info, "MapTilesetValue", "-", 12, NVector4.One, 330, 214, 110, 20);
        AddLabel(info, "MapVersionLabel", "保存版本", 11,
            new NVector4(.58f, .65f, .69f, 1f), 230, 246, 90, 20);
        _mapVersion = AddLabel(info, "MapVersionValue", "-", 11, NVector4.One, 330, 246, 110, 20);
        AddLabel(info, "DescriptionLabel", "地图描述", 11,
            new NVector4(.58f, .65f, .69f, 1f), 20, 272, 420, 20);
        _description = AddLabel(info, "MapDescValue", "", 12,
            new NVector4(.88f, .89f, .86f, 1f), 20, 300, 420, 112);
        _description.VerticalAlignment = TextVerticalAlignment.Top;
        _description.TruncateWidth = 420f;
        _compatibilityWarning = AddLabel(info, "MapCompatibilityWarning", "", 12,
            new NVector4(1f, .78f, .34f, 1f), 20, 370, 420, 50);
        _compatibilityWarning.VerticalAlignment = TextVerticalAlignment.Top;
        _compatibilityWarning.Visible = false;

        var playBorder = new SolidFrame("PlayGameButtonBorder", new NVector4(.66f, .48f, .12f, .98f));
        _panel.AddChild(playBorder);
        Place(playBorder, 460, 58, _panel, 500, 594);
        var playBackground = new SolidFrame("PlayGameButtonBackground", new NVector4(.12f, .09f, .035f, .98f));
        _panel.AddChild(playBackground);
        Place(playBackground, 454, 52, _panel, 503, 597);
        _play = Button("PlayGameButton", "确定", OnPlayRequest);
        _panel.AddChild(_play);
        Place(_play, 454, 52, _panel, 503, 597);

        _raceSelection = new RaceSelectionDialogFrame(_textures, _skin);
        _raceSelection.RaceSelected += racePreference =>
        {
            SelectedRacePreference = racePreference;
            _raceSelection.Hide();
            _difficultySelection.Show();
        };
        _raceSelection.Cancelled += _raceSelection.Hide;
        _root.AddChild(_raceSelection);
        _difficultySelection = new AiDifficultyDialogFrame(_textures, _skin);
        _difficultySelection.DifficultySelected += preset =>
        {
            SelectedAiPreset = preset;
            _difficultySelection.Hide();
            StartRequested?.Invoke(SelectedMap, SelectedRacePreference ?? 0, SelectedAiPreset);
        };
        _difficultySelection.Cancelled += () =>
        {
            _difficultySelection.Hide();
            _raceSelection.Show();
        };
        _root.AddChild(_difficultySelection);

        _router = new UiEventRouter(_root);
        if (_maps.Count > 0) Select(_maps[0]);
        else
        {
            _play.Disabled = true;
            _description.Text = "没有找到 gmap 地图。";
        }
        UpdatePageLayout();
    }

    public static string[] ListGmapsFullPaths()
    {
        string[] names = Assets.ListGmaps();
        var result = new string[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = Assets.GmapDir + "/" + names[i];
        return result;
    }

    UiFrame BuildFdfPanel()
    {
        var factory = new FdfUiFactory(_textures, FdfSource.Read);
        factory.LoadTemplate(@"UI\FrameDef\Glue\Skirmish.fdf");
        UiFrame panel = factory.Create("Skirmish", _root);
        panel.MouseTransparent = true;
        foreach (UiFrame child in panel.Children) child.Visible = false;
        return panel;
    }

    static int CompareBySize(string left, string right)
    {
        int bySize = FileLengthOrZero(left).CompareTo(FileLengthOrZero(right));
        if (bySize != 0) return bySize;
        return string.Compare(Path.GetFileName(left), Path.GetFileName(right),
            StringComparison.OrdinalIgnoreCase);
    }

    static long FileLengthOrZero(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }

    void ApplyMapVersion(MapInfo info)
    {
        _mapVersion.Text = MapCompatibilityNotice.VersionText(info);
        string warning = MapCompatibilityNotice.Warning(info);
        _compatibilityWarning.Text = warning;
        _compatibilityWarning.Visible = warning.Length > 0;
        _description.Height = _compatibilityWarning.Visible ? 60f : 112f;
    }

    void BuildRows()
    {
        for (int i = 0; i < _maps.Count; i++)
        {
            string path = _maps[i];
            var row = new MapRowButton($"MapRow{i}", path);
            row.Clicked += _ => Select(path);
            StringFrame label = Text(row.Name + "Text", 15, new NVector4(.94f, .94f, .9f, 1f));
            label.Text = Path.GetFileNameWithoutExtension(path);
            row.AddChild(label);
            Place(label, 330, RowHeight, row, 12, 0);
            _listViewport.AddChild(row);
            _rows.Add(row);
        }
    }

    void Select(string path)
    {
        SelectedMap = path;
        foreach (MapRowButton row in _rows)
            row.Selected = string.Equals(row.MapPath, path, StringComparison.OrdinalIgnoreCase);
        _previewTexture = null;
        _preview.Texture = null;
        _name.Text = Path.GetFileNameWithoutExtension(path);
        _players.Text = _suggestedPlayers.Text = _mapSize.Text = _tileset.Text = "-";
        _mapVersion.Text = "-";
        _compatibilityWarning.Text = "";
        _compatibilityWarning.Visible = false;
        _description.Height = 112f;
        _description.Text = "";
        try
        {
            GmapPackage package = GmapPackage.Mount(path);
            if (package == null)
            {
                _description.Text = "gmap 挂载失败。";
                return;
            }
            byte[] w3iBytes = package.ReadMapFile("war3map.w3i");
            MapInfo info = w3iBytes == null ? null : MapInfo.Parse(w3iBytes);
            WtsDocument strings = null;
            byte[] wts = package.ReadMapFile("war3map.wts");
            if (wts != null) strings = WtsDocument.Parse(wts);
            string name = info?.ResolveName(strings);
            if (!string.IsNullOrWhiteSpace(name)) _name.Text = name;
            _players.Text = info == null ? "-" : info.Players.Count.ToString();
            _suggestedPlayers.Text = info == null ? "-"
                : Fallback(MapInfo.Resolve(info.PlayersRef, strings), "未知");
            _mapSize.Text = info == null ? "-" : $"{info.PlayableWidth} × {info.PlayableHeight}";
            _tileset.Text = info == null ? "-" : info.Tileset.ToString();
            ApplyMapVersion(info);
            _description.Text = info == null ? "该地图没有提供描述。"
                : Fallback(MapInfo.Resolve(info.DescriptionRef, strings), "该地图没有提供描述。");
            foreach (string candidateName in new[] { "war3mapPreview.png", "war3mapMap.png" })
            {
                byte[] candidate = package.ReadMapFile(candidateName);
                if (candidate == null) continue;
                var image = new Image();
                if (image.LoadPngFromBuffer(candidate) != Error.Ok) continue;
                _previewTexture = new GodotUiTexture(ImageTexture.CreateFromImage(image));
                _preview.Texture = _previewTexture;
                break;
            }
        }
        catch (Exception error)
        {
            _description.Text = error.Message;
        }
    }

    static string Fallback(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Replace("\r", " ").Replace("\n", " ");

    void OnPlayRequest(int button)
    {
        if (button != 0 || string.IsNullOrEmpty(SelectedMap)
            || !File.Exists(SelectedMap)) return;
        SelectedRacePreference = null;
        SelectedAiPreset = MeleeAiPreset.Normal;
        _raceSelection.Show();
    }

    StringFrame AddLabel(UiFrame parent, string name, string value, float size, NVector4 color,
        float x, float y, float width, float height, TextAlignment alignment = TextAlignment.Left)
    {
        StringFrame label = Text(name, size, color);
        label.Text = value;
        label.Alignment = alignment;
        parent.AddChild(label);
        Place(label, width, height, parent, x, y);
        return label;
    }

    static void Place(UiFrame frame, float width, float height, UiFrame target, float x, float y)
    {
        if (frame == null || target == null) return;
        frame.SetPoints.Clear();
        frame.Width = width;
        frame.Height = height;
        frame.SetPoint(FramePoint.TopLeft, target, FramePoint.TopLeft, new NVector2(x, y));
    }

    ButtonFrame Button(string name, string label, Action<int> clicked)
    {
        var button = new ButtonFrame(name)
        {
            NormalTexture = _textures.Resolve(
                @"UI\Widgets\EscMenu\Human\blank-background.blp"),
            PushedTexture = _textures.Resolve(
                @"UI\Widgets\EscMenu\Human\blank-background.blp"),
            ActiveHighlight = _textures.Resolve(
                @"UI\Widgets\EscMenu\Human\human-options-button-highlight.blp"),
        };
        button.Clicked += clicked;
        StringFrame text = Text(name + "Text", 18, new NVector4(1f, .86f, .42f, 1f));
        text.Text = label;
        text.Alignment = TextAlignment.Center;
        button.AddChild(text);
        text.SetAllPoints();
        return button;
    }

    static StringFrame Text(string name, float size, NVector4 color) => new(name)
    {
        FontSize = size,
        Color = color,
        Alignment = TextAlignment.Left,
        VerticalAlignment = TextVerticalAlignment.Middle,
        MouseTransparent = true,
        FontShadowColor = new NVector4(0, 0, 0, 1),
        FontShadowOffset = new NVector2(1.5f, 1.5f),
    };

    int PageCount => Math.Max(1, (_rows.Count + PageSize - 1) / PageSize);

    void ChangePage(int delta, int button)
    {
        if (button != 0 || _rows.Count == 0) return;
        int next = Math.Clamp(_pageIndex + delta, 0, PageCount - 1);
        if (next == _pageIndex) return;
        _pageIndex = next;
        Select(_maps[_pageIndex * PageSize]);
        UpdatePageLayout();
    }

    void UpdatePageLayout()
    {
        int first = _pageIndex * PageSize;
        int last = Math.Min(_rows.Count, first + PageSize);
        for (int i = 0; i < _rows.Count; i++)
        {
            MapRowButton row = _rows[i];
            row.Visible = i >= first && i < last;
            if (!row.Visible) continue;
            row.SetPoints.Clear();
            row.Width = 356;
            row.Height = RowHeight;
            row.SetPoint(FramePoint.TopLeft, _listViewport, FramePoint.TopLeft,
                new NVector2(0, (i - first) * RowPitch));
        }
        _previousPage.Disabled = _pageIndex <= 0 || _rows.Count == 0;
        _nextPage.Disabled = _pageIndex >= PageCount - 1 || _rows.Count == 0;
        _pageStatus.Text = _rows.Count == 0 ? "共 0 张"
            : $"第 {_pageIndex + 1} / {PageCount} 页  ·  共 {_rows.Count} 张";
        _root.InvalidateLayout();
    }

    public override void _Process(double delta)
    {
        _glue.FollowCameraTarget();
        UpdateLayoutRect();
        QueueRedraw();
    }

    void UpdateLayoutRect()
    {
        Vector2 framebuffer = GetViewportRect().Size;
        _uiViewport.Update((int)framebuffer.X, (int)framebuffer.Y);
        var offset = new Vector2(_uiViewport.PixelOffsetX, 0f);
        var size = new Vector2(_uiViewport.ActivePixelWidth, _uiViewport.FramebufferHeight);
        if (Position != offset) Position = offset;
        if (Size != size) Size = size;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Keycode != Key.Escape) return;
        if (_raceSelection.Visible)
        {
            _raceSelection.Hide();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_difficultySelection.Visible)
        {
            _difficultySelection.Hide();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
            NVector2 point = ScreenToUi(button.Position);
            int index = button.ButtonIndex == MouseButton.Right ? 1 : 0;
            if (button.Pressed)
            {
                _router.PointerDown(point, index);
                _pointerDown = true;
                AcceptEvent();
            }
            else
            {
                _router.PointerUp(point, index);
                _pointerDown = false;
                AcceptEvent();
            }
            return;
        }
        if (@event is InputEventMouseMotion motion)
        {
            _router.PointerMove(ScreenToUi(motion.Position));
            if (_pointerDown) AcceptEvent();
        }
    }

    NVector2 ScreenToUi(Vector2 point)
    {
        Vector2 size = Size;
        return new NVector2(point.X * DesignWidth / Mathf.Max(1f, size.X),
            point.Y * DesignHeight / Mathf.Max(1f, size.Y));
    }

    public override void _Draw()
    {
        UpdateLayoutRect();
        _glue.Resize(new Vector2I((int)Size.X, (int)Size.Y));
        DrawTextureRect(_glue.Texture, new Rect2(Vector2.Zero, Size), false);
        float scaleX = Size.X / DesignWidth;
        float scaleY = Size.Y / DesignHeight;
        _root.UpdateViewport(DesignWidth, DesignHeight);
        _root.Position();
        _raceSelection.Layout(DesignWidth, DesignHeight);
        _difficultySelection.Layout(DesignWidth, DesignHeight);
        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        _root.Render(_device);
        _device.End();
    }

    sealed class SolidFrame : UiFrame
    {
        readonly NVector4 _color;

        public SolidFrame(string name, NVector4 color) : base(name)
        {
            _color = color;
            MouseTransparent = true;
        }

        public override void Render(IUiRenderDevice device)
        {
            if (!Visible) return;
            device.DrawTexturedQuad(RenderBounds, device.WhiteTexture, _color);
            base.Render(device);
        }
    }

    sealed class ListViewport : UiFrame
    {
        public ListViewport(string name) : base(name) => MouseTransparent = true;

        public override void Render(IUiRenderDevice device)
        {
            if (!Visible) return;
            device.PushScissor(RenderBounds);
            base.Render(device);
            device.PopScissor();
        }

        public override UiFrame HitTest(NVector2 point) =>
            RenderBounds.Contains(point.X, point.Y) ? base.HitTest(point) : null;
    }

    sealed class MapRowButton : ButtonFrame
    {
        public readonly string MapPath;
        public bool Selected;

        public MapRowButton(string name, string mapPath) : base(name) => MapPath = mapPath;

        public override void Render(IUiRenderDevice device)
        {
            if (!Visible) return;
            NVector4 color = Selected ? new(.27f, .20f, .07f, .96f)
                : IsHovered ? new(.16f, .18f, .18f, .92f)
                : new(.07f, .09f, .10f, .76f);
            device.DrawTexturedQuad(RenderBounds, device.WhiteTexture, color);
            base.Render(device);
        }
    }
}