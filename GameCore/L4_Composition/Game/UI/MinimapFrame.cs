using System;
using System.Collections.Generic;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class MinimapFrame : UiContainer, IClickable, IDraggable
{
    internal const float AreaX = 12f, AreaY = 582.4f;
    internal const float AreaWidth = 178.4f, AreaHeight = 176.8f;
    internal const float ToolWidth = 29.6f, ToolHeight = 26.4f;

    private const string ConsoleDir = @"UI\Widgets\Console\Human\";

    private readonly TextureFrame _mapTexture;
    private readonly IUiTextureResolver _textures;
    private readonly War3UiSkin _skin;
    private readonly War3MiscData _misc;
    private readonly ITexture _heroTexture;
    private readonly ITexture _goldTexture;
    private readonly ITexture _entangledTexture;
    private readonly ITexture _hauntedTexture;
    private readonly ITexture _neutralTexture;
    private readonly ITexture _copTexture;
    private readonly ITexture _itemTexture;
    private readonly ITexture _creepTexture;
    private readonly ITexture _toughCreepTexture;
    private readonly ITexture _startLocTexture;
    private readonly List<ButtonFrame> _tools = new();
    private readonly ButtonFrame _signalTool;
    private readonly ButtonFrame _terrainTool;
    private readonly ButtonFrame _colorTool;
    private readonly ButtonFrame _creepTool;
    private readonly ButtonFrame _formationTool;

    private Vector4 _mapBounds;
    private IReadOnlyList<MinimapUnitView> _units = Array.Empty<MinimapUnitView>();
    private int _localPlayerId;
    private int _localTeam;
    private ITexture _fogTexture;
    private Vector2 _fogUvMax = Vector2.One;
    private IReadOnlyList<MinimapPingView> _pings = Array.Empty<MinimapPingView>();
    private IReadOnlyList<MinimapSpecialIconView> _icons = Array.Empty<MinimapSpecialIconView>();
    private float _time;
    private bool _signalGesture;
    private bool _signalArmed;
    private int _allyColorState;
    private bool _showCreeps = true;
    private bool _formationEnabled;
    private bool _terrainVisible = true;
    private bool _allyButtonEnabled = true;
    private bool _creepButtonEnabled = true;

    public War3MiscData MiscData => _misc;
    public Action<Vector2> OnMinimapClick;
    public Action<Vector2> OnSignalPing;
    public Func<bool> AltHeld;
    public Vector4? CameraRect { get; set; }
    public Action<int, bool, bool, bool, bool> FilterChanged;

    public MinimapFrame(IUiTextureResolver textures, War3UiSkin skin, War3MiscData misc)
        : base("Minimap")
    {
        _textures = textures;
        _skin = skin;
        _misc = misc;

        _mapTexture = new TextureFrame("MinimapMap");
        AddChild(_mapTexture);

        _heroTexture = Resolve("MinimapHeroTexture", @"UI\MiniMap\minimap-hero.blp");
        _goldTexture = Resolve("MinimapResourceTexture", @"UI\MiniMap\minimap-gold.blp");
        _entangledTexture = Resolve("MinimapEntangledResourceTexture",
            @"UI\MiniMap\minimap-gold-entangled.blp");
        _hauntedTexture = Resolve("MinimapHauntedResourceTexture",
            @"UI\MiniMap\minimap-gold-haunted.blp");
        _neutralTexture = Resolve("MinimapNeutralTexture",
            @"UI\MiniMap\minimap-neutralbuilding.blp");
        _copTexture = Resolve("MinimapCopTexture", @"UI\MiniMap\MinimapIconCircleOfPower.blp");
        _itemTexture = Resolve("MinimapItemTexture", @"UI\MiniMap\minimap-item.blp");
        _creepTexture = Resolve("MinimapCreepCampTexture",
            @"UI\Minimap\MinimapIcon\MinimapIconCreepLoc.blp");
        _toughCreepTexture = Resolve("MinimapToughCreepCampTexture",
            @"UI\Minimap\MinimapIcon\MinimapIconCreepLoc2.blp");
        _startLocTexture = Resolve("MinimapStartLocationTexture",
            @"UI\MiniMap\MiniMapIcon\MinimapIconStartLoc.blp");

        _signalTool = MakeTool("MinimapSignalButton",
            "MiniMapSignalButtonEnabled", "human-minimap-ping-active",
            "MiniMapSignalButtonPushed", "human-minimap-ping-active-down",
            "MiniMapSignalButtonDisabled", "human-minimap-ping-disabled",
            "小地图信号 (Alt-G)");
        _terrainTool = MakeTool("MinimapTerrainButton",
            "MiniMapTerrainButtonInactiveEnabled", "human-minimap-terrain-inactive",
            "MiniMapTerrainButtonInactivePushed", "human-minimap-terrain-inactive-down",
            "MiniMapTerrainButtonInactiveDisabled", "human-minimap-terrain-inactive-disabled",
            "开关小地图地形显示 (Alt-T)");
        _colorTool = MakeTool("MinimapAllyButton",
            "MiniMapAllyButtonInactiveEnabled", "human-minimap-ally-inactive",
            "MiniMapAllyButtonInactivePushed", "human-minimap-ally-inactive-down",
            "MiniMapAllyButtonInactiveDisabled", "human-minimap-ally-inactive-disabled",
            "开关小地图玩家配色 (Alt-A)");
        _creepTool = MakeTool("MinimapCreepButton",
            "MiniMapCreepButtonInactiveEnabled", "human-minimap-creep-inactive",
            "MiniMapCreepButtonInactivePushed", "human-minimap-creep-inactive-down",
            "MiniMapCreepButtonInactiveDisabled", "human-minimap-creep-inactive-disabled",
            "开关野怪营地显示");
        _formationTool = MakeTool("MinimapFormationButton",
            "MiniMapFormationOffButtonEnabled", "human-formation-off",
            "MiniMapFormationOffButtonEnabledPushed", "human-formation-off-down",
            "MiniMapFormationOffButtonDisabled", "human-formation-off-disabled",
            "开关编队移动 (Alt-F)");

        _signalTool.Clicked += _ => SetSignalArmed(!_signalArmed);
        _terrainTool.Clicked += _ => ToggleTerrain();
        _colorTool.Clicked += _ => TogglePlayerColors();
        _creepTool.Clicked += _ =>
        {
            _showCreeps = !_showCreeps;
            ApplyFilterTools();
        };
        _formationTool.Clicked += _ =>
        {
            _formationEnabled = !_formationEnabled;
            ApplyFilterTools();
        };
        foreach (ButtonFrame tool in _tools) AddChild(tool);
        ApplyFilterTools();
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float y = viewportHeight - (War3UiViewport.BaseHeight - AreaY);
        RenderBounds = new UiRect(AreaX, y, AreaWidth, AreaHeight);
        _mapTexture.RenderBounds = RenderBounds;
        LayoutTools();
    }

    public void Update(Vector4 mapBounds, IReadOnlyList<MinimapUnitView> units,
        int localPlayerId, int localTeam, ITexture fogTexture, Vector2 fogUvMax,
        IReadOnlyList<MinimapPingView> pings, IReadOnlyList<MinimapSpecialIconView> icons,
        float timeSeconds)
    {
        _mapBounds = mapBounds;
        _units = units ?? Array.Empty<MinimapUnitView>();
        _localPlayerId = localPlayerId;
        _localTeam = localTeam;
        _fogTexture = fogTexture;
        _fogUvMax = fogUvMax;
        _pings = pings ?? Array.Empty<MinimapPingView>();
        _icons = icons ?? Array.Empty<MinimapSpecialIconView>();
        _time = timeSeconds;
    }

    public void SetMapTexture(ITexture texture) => _mapTexture.Texture = texture;

    public void SetFilterState(int allyState, bool showCreeps, bool allyEnabled,
        bool creepEnabled, bool formationEnabled)
    {
        allyState = Math.Clamp(allyState, 0, 2);
        if (_allyColorState == allyState && _showCreeps == showCreeps
            && _allyButtonEnabled == allyEnabled && _creepButtonEnabled == creepEnabled
            && _formationEnabled == formationEnabled) return;
        _allyColorState = allyState;
        _showCreeps = showCreeps;
        _allyButtonEnabled = allyEnabled;
        _creepButtonEnabled = creepEnabled;
        _formationEnabled = formationEnabled;
        ApplyFilterTools();
    }

    public void SetSignalArmed(bool armed) => _signalArmed = armed;

    public void ToggleTerrain()
    {
        _terrainVisible = !_terrainVisible;
        _mapTexture.Visible = _terrainVisible;
        ApplyFilterTools();
    }

    public void TogglePlayerColors()
    {
        if (!_allyButtonEnabled) return;
        _allyColorState = (_allyColorState + 1) % 3;
        ApplyFilterTools();
    }

    public bool HandleAltShortcut(char key)
    {
        switch (char.ToUpperInvariant(key))
        {
            case 'G': SetSignalArmed(!_signalArmed); return true;
            case 'T': ToggleTerrain(); return true;
            case 'A': TogglePlayerColors(); return true;
            default: return false;
        }
    }

    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        device.DrawTexturedQuad(RenderBounds, device.WhiteTexture, new Vector4(0, 0, 0, 1));
        base.Render(device);
        DrawFog(device);
        DrawUnitDots(device);
        DrawSpecialIcons(device);
        DrawPings(device);
        DrawCameraRect(device);
    }

    private void DrawFog(IUiRenderDevice device)
    {
        if (_fogTexture == null || !HasBounds) return;
        device.DrawTexturedQuad(ContentRect(_mapTexture.RenderBounds), _fogTexture, Vector4.One,
            new UiRect(0, 0, _fogUvMax.X, _fogUvMax.Y));
    }

    private float FogAlpha(MinimapFogLevel level) => level switch
    {
        MinimapFogLevel.Masked => _misc.BlackMaskAlpha,
        MinimapFogLevel.Dark => _misc.DarkMaskAlpha,
        MinimapFogLevel.Fogged => _misc.FogMaskAlpha,
        _ => _misc.VisibleAlpha,
    };

    private void DrawUnitDots(IUiRenderDevice device)
    {
        if (!HasBounds) return;
        UiRect frame = ContentRect(_mapTexture.RenderBounds);
        for (int index = 0; index < _units.Count; index++)
        {
            MinimapUnitView unit = _units[index];
            if (unit.Icon == MinimapIconKind.CreepCamp && !_showCreeps) continue;
            float alpha = FogAlpha(unit.Fog);
            if (alpha <= 0.01f) continue;

            float size = unit.Icon is MinimapIconKind.Resource or MinimapIconKind.EntangledResource
                or MinimapIconKind.HauntedResource or MinimapIconKind.Neutral or MinimapIconKind.Cop
                ? RenderBounds.Width * 16f / 256f
                : unit.Icon == MinimapIconKind.Hero ? DotSize(unit, _localPlayerId)
                : RenderBounds.Width * (unit.IsBuilding ? 8f : 4f) / 256f;
            Vector2 center = WorldToMinimap(new Vector2(unit.X, unit.Y), frame);
            float height = unit.Icon == MinimapIconKind.Hero ? size
                : size * RenderBounds.Height / RenderBounds.Width;
            float half = size * .5f, halfY = height * .5f;
            float marginX = Math.Min(half, frame.Width * .5f), marginY = Math.Min(halfY, frame.Height * .5f);
            center = new Vector2(
                Math.Clamp(center.X, frame.X + marginX, frame.Right - marginX),
                Math.Clamp(center.Y, frame.Y + marginY, frame.Bottom - marginY));
            var rect = new UiRect(center.X - half, center.Y - halfY, size, height);

            if (unit.Icon == MinimapIconKind.CreepCamp)
            {
                _misc.ResolveCamp(unit.CampStrength, out Vector4 campColor, out float scale);
                var campRect = new UiRect(rect.X - rect.Width * (scale - 1f) * .5f,
                    rect.Y - rect.Height * (scale - 1f) * .5f,
                    rect.Width * scale, rect.Height * scale);
                device.DrawTexturedQuad(campRect,
                    unit.CampStrength >= _misc.ToughCampThreshold
                        ? _toughCreepTexture ?? _creepTexture : _creepTexture,
                    WithAlpha(campColor, alpha));
                continue;
            }

            ITexture texture = string.IsNullOrWhiteSpace(unit.TexturePath)
                ? IconTexture(unit.Icon)
                : _textures.Resolve(unit.TexturePath) ?? IconTexture(unit.Icon);
            bool special = unit.Icon is MinimapIconKind.Resource or MinimapIconKind.EntangledResource
                or MinimapIconKind.HauntedResource or MinimapIconKind.Neutral or MinimapIconKind.Cop;
            Vector4 color = WithAlpha(special ? Vector4.One : _allyColorState == 0
                ? PlayerDotColor(unit)
                : DotColor(unit, _localPlayerId, _localTeam, unit.Icon, _misc), alpha);
            if (texture != null) device.DrawTexturedQuad(rect, texture, color);
            else device.DrawTexturedQuad(rect, device.WhiteTexture, color);
        }
    }

    private ITexture IconTexture(MinimapIconKind kind) => kind switch
    {
        MinimapIconKind.Hero => _heroTexture,
        MinimapIconKind.Resource => _goldTexture,
        MinimapIconKind.EntangledResource => _entangledTexture,
        MinimapIconKind.HauntedResource => _hauntedTexture,
        MinimapIconKind.Neutral => _neutralTexture,
        MinimapIconKind.Cop => _copTexture,
        MinimapIconKind.Item => _itemTexture,
        MinimapIconKind.StartLocation => _startLocTexture,
        _ => null,
    };

    private static Vector4 WithAlpha(Vector4 color, float alpha)
        => new(color.X, color.Y, color.Z, alpha);

    internal static float DotSize(MinimapUnitView unit, int localPlayerId) =>
        unit.IsHero
            ? unit.PlayerId == localPlayerId ? 17.92f : 8.96f
            : unit.IsBuilding ? AreaWidth * 8f / 256f : AreaWidth * 4f / 256f;

    internal static Vector4 DotColor(MinimapUnitView unit, int localPlayerId, int localTeam,
        MinimapIconKind icon = MinimapIconKind.Unit, War3MiscData misc = null)
    {
        misc ??= new War3MiscData();
        return icon switch
        {
            MinimapIconKind.Resource or MinimapIconKind.EntangledResource
                or MinimapIconKind.HauntedResource => misc.ColorResource,
            MinimapIconKind.Item => misc.ColorItem,
            MinimapIconKind.Hero => misc.ColorHero,
            MinimapIconKind.Neutral or MinimapIconKind.Cop => misc.ColorDestructable,
            _ => unit.PlayerId >= 12 ? misc.ColorCreepAllied
                : unit.PlayerId == localPlayerId ? misc.ColorPlayer
                : (unit.Allied ?? unit.Team == localTeam) ? misc.ColorAlly : misc.ColorEnemy,
        };
    }

    private Vector4 PlayerDotColor(MinimapUnitView unit)
    {
        if (unit.PlayerId == _localPlayerId) return _misc.ColorPlayer;
        if (unit.Icon == MinimapIconKind.CreepCamp) return _misc.ColorCreepNormal;
        return _skin?.TeamColor(unit.ColorIndex >= 0 ? unit.ColorIndex : unit.PlayerId)
            ?? NativePlayerColor(unit.ColorIndex >= 0 ? unit.ColorIndex : unit.PlayerId);
    }

    internal static Vector4 NativePlayerColor(int slot)
    {
        uint[] colors = DefaultPlayerColors;
        uint rgb = colors[Math.Abs(slot % colors.Length)];
        return new Vector4((rgb >> 16) / 255f, ((rgb >> 8) & 255) / 255f,
            (rgb & 255) / 255f, 1);
    }

    private static readonly uint[] DefaultPlayerColors =
    {
        0xff0303, 0x0042ff, 0x1ce6b9, 0x540081, 0xfffc00, 0xfe8a0e,
        0x20c000, 0xe55bb0, 0x959697, 0x7ebff1, 0x106246, 0x4e2a04,
        0x282828, 0x282828, 0x282828, 0x282828,
    };

    private Vector2 WorldToMinimap(Vector2 world, UiRect frame)
    {
        float tx = Math.Clamp((world.X - _mapBounds.X) / (_mapBounds.Z - _mapBounds.X), 0, 1);
        float ty = Math.Clamp((world.Y - _mapBounds.Y) / (_mapBounds.W - _mapBounds.Y), 0, 1);
        return new Vector2(frame.X + tx * frame.Width, frame.Y + (1 - ty) * frame.Height);
    }

    private void DrawSpecialIcons(IUiRenderDevice device)
    {
        if (!HasBounds) return;
        UiRect frame = ContentRect(_mapTexture.RenderBounds);
        device.PushScissor(frame);
        for (int index = 0; index < _icons.Count; index++)
        {
            MinimapSpecialIconView icon = _icons[index];
            if (icon.Kind == MinimapIconKind.CreepCamp && !_showCreeps) continue;
            Vector2 center = WorldToMinimap(new Vector2(icon.X, icon.Y), frame);
            float size = Math.Max(4, icon.Size), half = size * .5f;
            var rect = new UiRect(center.X - half, center.Y - half, size, size);
            if (icon.Kind == MinimapIconKind.CreepCamp && device.DrawMinimapCamp(rect,
                icon.CampStrength >= _misc.ToughCampThreshold,
                _time * Math.Clamp(_misc.CampPulseScale, 0, 1), icon.Color)) continue;
            ITexture texture = icon.Kind == MinimapIconKind.CreepCamp
                ? icon.CampStrength >= _misc.ToughCampThreshold ? _toughCreepTexture : _creepTexture
                : _textures.Resolve(icon.TexturePath);
            if (texture == null) continue;
            device.DrawTexturedQuad(rect, texture, icon.Color);
        }
        device.PopScissor();
    }

    private void DrawPings(IUiRenderDevice device)
    {
        if (!HasBounds) return;
        UiRect frame = ContentRect(_mapTexture.RenderBounds);
        foreach (MinimapPingView ping in _pings)
        {
            float phase = ping.Duration <= 0 ? 1 : 1 - ping.Remaining / ping.Duration;
            float pulse = phase * 3f - MathF.Floor(phase * 3f);
            float radius = 8f + pulse * 22f;
            float alpha = (1f - pulse) * Math.Clamp(ping.Remaining / .25f, 0, 1);
            Vector2 center = WorldToMinimap(new Vector2(ping.X, ping.Y), frame);
            Vector4 color = new(ping.Red / 255f, ping.Green / 255f, ping.Blue / 255f, alpha);
            DrawPingRing(device, center, radius, color, frame);
            if (ping.ExtraEffects)
                DrawPingRing(device, center, radius * .55f,
                    new Vector4(color.X, color.Y, color.Z, alpha * .75f), frame);
        }
    }

    private static void DrawPingRing(IUiRenderDevice device, Vector2 center, float radius,
        Vector4 color, UiRect clip)
    {
        const int segments = 16;
        float dotSize = Math.Clamp(radius * .18f, 2.5f, 5f);
        device.PushScissor(clip);
        for (int index = 0; index < segments; index++)
        {
            float angle = index * MathF.Tau / segments;
            float x = center.X + MathF.Cos(angle) * radius - dotSize * .5f;
            float y = center.Y + MathF.Sin(angle) * radius - dotSize * .5f;
            var dot = new UiRect(x, y, dotSize, dotSize);
            if (dot.Right <= clip.X || dot.X >= clip.Right
                || dot.Bottom <= clip.Y || dot.Y >= clip.Bottom) continue;
            device.DrawTexturedQuad(dot, device.RadialCircleTexture, color);
        }
        device.PopScissor();
    }

    private void DrawCameraRect(IUiRenderDevice device)
    {
        if (CameraRect is not Vector4 world || !HasBounds) return;
        UiRect frame = ContentRect(_mapTexture.RenderBounds);
        Vector2 min = WorldToMinimap(new Vector2(world.X, world.W), frame);
        Vector2 max = WorldToMinimap(new Vector2(world.Z, world.Y), frame);
        var rect = new UiRect(min.X, min.Y, Math.Max(1f, max.X - min.X),
            Math.Max(1f, max.Y - min.Y));
        var color = new Vector4(1f, 1f, 1f, 0.86f);
        const float t = 1f;
        device.DrawTexturedQuad(new UiRect(rect.X, rect.Y, rect.Width, t), device.WhiteTexture, color);
        device.DrawTexturedQuad(new UiRect(rect.X, rect.Bottom - t, rect.Width, t), device.WhiteTexture, color);
        device.DrawTexturedQuad(new UiRect(rect.X, rect.Y, t, rect.Height), device.WhiteTexture, color);
        device.DrawTexturedQuad(new UiRect(rect.Right - t, rect.Y, t, rect.Height), device.WhiteTexture, color);
    }

    private bool HasBounds => _mapBounds.Z > _mapBounds.X && _mapBounds.W > _mapBounds.Y;

    private UiRect ContentRect(UiRect frame) => FitContentRect(frame, _mapBounds);

    internal static UiRect FitContentRect(UiRect frame, Vector4 mapBounds)
    {
        if (mapBounds.Z <= mapBounds.X || mapBounds.W <= mapBounds.Y) return frame;
        float mapAspect = (mapBounds.Z - mapBounds.X) / (mapBounds.W - mapBounds.Y);
        if (mapAspect > 1f)
        {
            float height = frame.Height / mapAspect;
            return new UiRect(frame.X, frame.Y + (frame.Height - height) * .5f,
                frame.Width, height);
        }
        float width = frame.Width * mapAspect;
        return new UiRect(frame.X + (frame.Width - width) * .5f, frame.Y, width, frame.Height);
    }

    private void ApplyFilterTools()
    {
        _colorTool.Disabled = !_allyButtonEnabled;
        _creepTool.Disabled = !_creepButtonEnabled;
        string ally = _allyColorState == 0 ? "Off" : _allyColorState == 1 ? "Inactive" : "Active";
        SetToolState(_colorTool, "MiniMapAllyButton" + ally, "human-minimap-ally-" + ally.ToLowerInvariant());
        string creep = _showCreeps ? "Active" : "Inactive";
        SetToolState(_creepTool, "MiniMapCreepButton" + creep, "human-minimap-creep-" + creep.ToLowerInvariant());
        string formation = _formationEnabled ? "On" : "Off";
        SetToolState(_formationTool, "MiniMapFormation" + formation + "Button",
            "human-formation-" + formation.ToLowerInvariant(), true);
        SetToolState(_terrainTool, "MiniMapTerrainButton" + (_terrainVisible ? "Active" : "Inactive"),
            "human-minimap-terrain-" + (_terrainVisible ? "active" : "inactive"));
        FilterChanged?.Invoke(_allyColorState, _showCreeps, _allyButtonEnabled,
            _creepButtonEnabled, _formationEnabled);
    }

    private void SetToolState(ButtonFrame button, string key, string path, bool formation = false)
    {
        button.NormalTexture = Resolve(key + "Enabled", ConsoleDir + path + ".blp");
        button.PushedTexture = Resolve(key + (formation ? "EnabledPushed" : "Pushed"),
            ConsoleDir + path + "-down.blp") ?? button.NormalTexture;
        button.DisabledTexture = Resolve(key + "Disabled",
            ConsoleDir + path + "-disabled.blp") ?? button.NormalTexture;
    }

    private ButtonFrame MakeTool(string name, string normalKey, string normalPath,
        string pushedKey, string pushedPath, string disabledKey, string disabledPath,
        string tooltip)
    {
        var button = new ButtonFrame(name)
        {
            TooltipTitle = tooltip,
            TooltipBody = "",
        };
        button.NormalTexture = Resolve(normalKey, ConsoleDir + normalPath + ".blp");
        button.PushedTexture = Resolve(pushedKey, ConsoleDir + pushedPath + ".blp")
            ?? button.NormalTexture;
        button.DisabledTexture = Resolve(disabledKey, ConsoleDir + disabledPath + ".blp")
            ?? button.NormalTexture;
        _tools.Add(button);
        return button;
    }

    private void LayoutTools()
    {
        float bottom = RenderBounds.Bottom + 8.8f;
        float[] offsets = { 184f, 156f, 128f, 99.2f, 64f };
        for (int i = 0; i < _tools.Count; i++)
        {
            _tools[i].RenderBounds = new UiRect(i == 4 ? 198.4f : 197.6f,
                bottom - offsets[i], ToolWidth, ToolHeight);
            _tools[i].SetPoints.Clear();
        }
    }

    private ITexture Resolve(string skinKey, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(fallbackPath)) return null;
        if (_skin == null || string.IsNullOrWhiteSpace(skinKey))
            return _textures.Resolve(fallbackPath);
        return _textures.Resolve(_skin.Get(skinKey, fallbackPath));
    }

    bool IClickable.Enabled => Enabled;

    public override UiFrame HitTest(Vector2 point)
    {
        if (!Visible || !Enabled || MouseTransparent) return null;
        foreach (UiFrame child in Children)
        {
            UiFrame hit = child.HitTest(point);
            if (hit != null) return hit;
        }
        return RenderBounds.Contains(point.X, point.Y) ? this : null;
    }

    void IClickable.OnPointerDown(Vector2 pos)
    {
        _signalGesture = _signalArmed || AltHeld?.Invoke() == true;
        if (_signalGesture)
        {
            if (ToWorld(pos, out Vector2 signal))
            {
                OnSignalPing?.Invoke(signal);
                if (_signalArmed) SetSignalArmed(false);
            }
            return;
        }
        MoveCameraTo(pos);
    }

    void IClickable.OnPointerUp(Vector2 pos) { }

    void IClickable.OnClick(int button) { }

    public void OnPointerDrag(Vector2 pos)
    {
        if (!_signalGesture) MoveCameraTo(pos);
    }

    private void MoveCameraTo(Vector2 pos)
    {
        if (ToWorld(pos, out Vector2 world)) OnMinimapClick?.Invoke(world);
    }

    private bool ToWorld(Vector2 pos, out Vector2 world)
    {
        world = default;
        if (!HasBounds) return false;
        UiRect frame = ContentRect(_mapTexture.RenderBounds);
        if (frame.Width <= 0 || frame.Height <= 0) return false;
        float tx = Math.Clamp((pos.X - frame.X) / frame.Width, 0, 1);
        float ty = Math.Clamp(1 - (pos.Y - frame.Y) / frame.Height, 0, 1);
        world = new Vector2(
            _mapBounds.X + tx * (_mapBounds.Z - _mapBounds.X),
            _mapBounds.Y + ty * (_mapBounds.W - _mapBounds.Y));
        return true;
    }
}