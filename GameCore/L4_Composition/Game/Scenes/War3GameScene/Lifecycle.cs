using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using OpenWC3.Game.UI;
using OpenWC3.RHI;
using NVector4 = System.Numerics.Vector4;
namespace OpenWC3.Game;

public sealed partial class War3GameScene : Node3D
{
    private MapSource _map;
    private MapWorldBuilder _terrainBuilder;
    private TerrainShadowMap _terrainShadows;
    private World _world;
    private War3Camera _camera;
    private War3CameraComponent _cameraComponent;
    private War3CursorController _cursor;
    private PlayerSelectionController _selection;
    private PlayerCommandProcessor _commandProcessor;
    private War3PickingComponent _picking;
    private War3WorldInteractionComponent _worldInteraction;
    private War3InputComponent _inputComponent;
    private MeleeHudController _hud;
    private GameHudScreen _newHud;
    private BattleOverlayRenderer _battleOverlay;
    private SelectionCircleRenderer _selectionCircles;
    private UnitShadowRenderer _unitShadows;
    private NVector4 _mapUiBounds;
    private GodotUiTexture _minimapFogTexture;
    private Texture2D _minimapBaseTexture;
        public int SelectedRace;
    private War3AudioSystem _audio;
    private War3AckSoundService _ackSounds;
    private War3UiSoundService _uiSounds;
    private War3MusicPlayer _music;
    private War3LoopingSoundService _loopingSounds;
    private Func<Unit, bool> _unitAudibleForAudio;
    private FogOfWar _fog;
    private float _fogTimer;
    private Action _back;

    private readonly List<Unit> _allUnits = new();
    private readonly List<Unit> _selected = new();
        private Node3D _placementGhost;
    private MeshInstance3D _placementFootprintNode;
    private ImmediateMesh _placementFootprintMesh;
    private bool _placementPreviewValid;
        private float _hudTimer;
    private PathGrid _pathGrid;
    private List<TreeState> _trees = new();
    private string _placingId;
    private Item _selectedGroundItem;

    /// <summary>暂停只停模拟层与玩家镜头输入，脚本/界面照常存在。</summary>
    public bool Paused;

    /// <summary>设置等对话框可见时暂停模拟（客户端 MenuPauseRequested，不改动手动暂停状态）。</summary>
    internal bool MenuPauseRequested => _newHud?.AutoPauseRequested == true;

    /// <summary>模拟是否停步：手动暂停或设置类对话框可见。</summary>
    internal bool SimulationBlocked => Paused || MenuPauseRequested;

    private GameUserSettings _userSettings;

    private void ApplyFrameRateLimit(int fps) => Engine.MaxFps = GameUserSettings.NormalizeFrameRate(fps);

    private void ApplyRenderScale(float scale)
    {
        Viewport viewport = GetViewport();
        if (viewport == null) return;
        viewport.Scaling3DScale = GameUserSettings.NormalizeRenderScale(scale);
    }

    private void TogglePause()
    {
        Paused = !Paused;
        _escMenu?.SetPaused(Paused);
        _hud?.SetHint(Paused ? "已暂停" : "");
    }

    /// <summary>-1 表示跟随客户端 melee 模板（TFT 500/150、RoC 750/200）。</summary>
    public int StartGold = -1;
    public int StartLumber = -1;
    public float TimeScaleHint = 1f;
    public bool Aggression;
    public bool FogEnabled = true;
    public bool MusicEnabled = true;

    /// <summary>一昼夜多少秒。原版是 8 分钟，可用 --day-seconds 覆盖。</summary>
    public float DaySeconds = 480f;

    private float _todHours = MeleeRules.StartingTod;
    public bool AiEnabled = true;
    /// <summary>0=简单 1=普通 2=困难：只影响波次间隔、编成厚度与后撤阈值。</summary>
    public int AiDifficulty = 1;

    /// <summary>0 表示自动：w3i 里 controller 为玩家/电脑且有出生点的座位全部参战。</summary>
    public int ActivePlayers;

    public readonly HashSet<int> ActiveSlots = new();
    /// <summary>大厅里的队伍划分（每项是一队玩家号）；为空时用地图 forces。</summary>
    public readonly List<List<int>> Teams = new();
    private readonly HashSet<int> _active = new();
    private readonly Dictionary<int, string> _playerNames = new();
    private readonly HashSet<int> _defeated = new();
    private float _victoryTimer;
    private bool _gameOver;
    private readonly Dictionary<int, int> _allyMasks = new();

    private bool IsActivePlayer(int playerId) => _active.Contains(playerId);
    public bool Housekeeping => AiEnabled;
    public int ViewPlayer;

    private string HudRaceName()
    {
        int race = 1;
        foreach (MapPlayerInfo player in _map.Players)
        {
            if (player.Id == ViewPlayer) { race = player.Race; break; }
        }
        return race switch { 2 => "Orc", 3 => "Undead", 4 => "NightElf", _ => "Human" };
    }

    public void Setup(string gmapPath, Action back)
    {
        _back = back;
        _ = RunSetupAsync(gmapPath);
    }

    // 加载完成（加载屏淡出前）触发, 之后的镜头预设与调试入口都挂在这里
    public event Action Loaded;

    private const float AbortHoldSeconds = 2.5f;

    private async Task RunSetupAsync(string gmapPath)
    {
        _map = MapSource.Mount(gmapPath);
        if (_map == null)
        {
            _back?.Invoke();
            return;
        }

        _loadingScreen = new War3LoadingScreenRenderer(this, _map, SelectedRace);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (!await SetupSteps(_map))
        {
            _loadingScreen?.Fail(_failedStage);
            await ToSignal(GetTree().CreateTimer(AbortHoldSeconds), SceneTreeTimer.SignalName.Timeout);
            _loadingScreen?.Finish();
            _back?.Invoke();
            return;
        }

        _loadingScreen?.Finish();
        _cursor = War3CursorController.TryCreate(this, HudRaceName());
        Loaded?.Invoke();
    }

    private enum LoadingStage
    {
        Begin,
        Terrain,
        Textures,
        TerrainPresentation,
        SceneResources,
        WorldConfiguration,
        PlacedUnits,
        MapAot,
        MapScript,
        PresentationPreload,
        Pathing,
        Selection,
        Behaviours,
        Complete,
    }

    private LoadingStage _loadingStage;
    private string _failedStage;

    private static string StageName(LoadingStage stage) => stage switch
    {
        LoadingStage.Begin => "初始化",
        LoadingStage.Terrain => "解析地形与地图配置",
        LoadingStage.Textures => "载入地形纹理",
        LoadingStage.TerrainPresentation => "建立地形、水面与光照",
        LoadingStage.SceneResources => "载入模型与场景资源",
        LoadingStage.WorldConfiguration => "合并地图物编与界面配置",
        LoadingStage.PlacedUnits => "创建地图预摆单位",
        LoadingStage.MapAot => "正在准备地图脚本…",
        LoadingStage.MapScript => "执行地图脚本与触发器",
        LoadingStage.PresentationPreload => "预热单位、头像、特效与弹道模型",
        LoadingStage.Pathing => "构建寻路与碰撞网格",
        LoadingStage.Selection => "初始化镜头与游戏界面",
        LoadingStage.Behaviours => "挂载行为组件",
        _ => "进入游戏",
    };

    // 阶段之间让出一帧, 该帧的 _Process/_PhysicsProcess 被 LoadingBlocked 挡住
    private async Task<bool> SetupSteps(MapSource map)
    {
        _loadingStage = LoadingStage.Begin;
        while (_loadingStage != LoadingStage.Complete)
        {
            LoadingStage stage = _loadingStage;
            try
            {
                if (!await RunStage(stage, map))
                {
                    _failedStage = StageName(stage);
                    return false;
                }
            }
            catch (Exception error) when (stage >= LoadingStage.SceneResources && stage <= LoadingStage.Selection)
            {
                GD.PushWarning($"[INIT] {StageName(stage)} 失败，已跳过: {error}");
            }
            catch (Exception error)
            {
                GD.PushError($"[INIT] {StageName(stage)} 失败，中止加载: {error}");
                _failedStage = StageName(stage);
                return false;
            }

            _loadingStage++;
        }
        return true;
    }

    private async Task<bool> RunStage(LoadingStage stage, MapSource map)
    {
        switch (stage)
        {
            case LoadingStage.Terrain: return await LoadTerrain(map);
            case LoadingStage.Textures: await LoadTextures(map); break;
            case LoadingStage.TerrainPresentation: await LoadTerrainPresentation(map); break;
            case LoadingStage.SceneResources: await LoadSceneResources(); break;
            case LoadingStage.WorldConfiguration: await LoadWorldConfiguration(map); break;
            case LoadingStage.PlacedUnits: await LoadPlacedUnits(); break;
            case LoadingStage.MapAot:
            case LoadingStage.MapScript:
                await Loading(stage == LoadingStage.MapAot ? .73f : .76f, StageName(stage));
                break;
            case LoadingStage.PresentationPreload: await LoadPresentationPreload(); break;
            case LoadingStage.Pathing: await LoadPathing(map); break;
            case LoadingStage.Selection: await LoadSelection(map); break;
            case LoadingStage.Behaviours: AttachBehaviours(); break;
        }
        return true;
    }

    private async Task<bool> LoadTerrain(MapSource map)
    {
        if (!map.LoadTerrainAndObjects())
        {
            return false;
        }

        if (SelectedRace > 0)
        {
            for (int i = 0; i < _map.Players.Count; i++)
            {
                if (_map.Players[i].Id != ViewPlayer) continue;
                _map.Players[i] = _map.Players[i] with { Race = SelectedRace };
                GD.Print($"[INIT] 本地玩家种族改写为 {RaceName(SelectedRace)}");
                break;
            }
        }

        ResolveActivePlayers();

        (int templateGold, int templateLumber) = MeleeRules.StartingResources(IsReignOfChaos);
        int gold = StartGold >= 0 ? StartGold : templateGold;
        int lumber = StartLumber >= 0 ? StartLumber : templateLumber;
        foreach (int id in _active)
        {
            PlayerSlot slot = Slot(id);
            slot.Gold = gold;
            slot.Lumber = lumber;
            slot.Ai = AiEnabled;
        }

        await Loading(.10f, StageName(LoadingStage.Terrain));
        return true;
    }

    private async Task LoadTextures(MapSource map)
    {
        _terrainBuilder = new MapWorldBuilder();
        War3FogConfig fog = War3FogConfig.FromW3i(map.Info);
        War3FogConfig.SetCurrent(fog);
        _fogConfig = fog;
        FogOfWarUniforms.Reset();
        GD.Print($"[FOG] 地图距离雾 {fog.Describe()}");
        Node3D worldRoot = _terrainBuilder.Build(map.Terrain, map.Doodads,
            War3WaterConfig.MapBaseColor(map.Info), fog);
        AddChild(worldRoot);
        _worldRoot = worldRoot;
        _terrainShadows = new TerrainShadowMap(map.Terrain, _terrainBuilder.TerrainMaterial,
            _terrainBuilder.TreeShadows);

        _weather = new WeatherSystem(this, map.Info, CameraTargetHeightAt, fog);

        await Loading(.22f, StageName(LoadingStage.Textures));
    }

    private async Task LoadTerrainPresentation(MapSource map)
    {
        _sceneLighting = SceneLighting.Load(SceneLighting.ResolveTileset(
            map.Info?.LightEnvTileset ?? (char)0, map.Terrain.Tileset), _todHours);
        _sceneLightingHour = _todHours;
        SceneLightRig.Apply(_worldRoot, _sceneLighting.Terrain, _sceneLighting.Terrain);
        GD.Print($"[INIT] 昼夜光照 {SceneLighting.Describe(_sceneLighting.Terrain)}");

        _skyBackground = SkyBackground.TryCreate(this, map.SkyboxType);
        if (_skyBackground == null) SkyboxRenderer.TryCreate(this, map.SkyboxType);

        await Loading(.34f, StageName(LoadingStage.TerrainPresentation));
    }

    private async Task LoadSceneResources()
    {
        War3SoundTables.Load();
        GD.Print($"[INIT] 技能表 {AbilityCatalog.Count} 条");

        await Loading(.45f, StageName(LoadingStage.SceneResources));
    }

    private async Task LoadWorldConfiguration(MapSource map)
    {
        _pathGrid = PathGrid.Build(map.Pathing, map.Terrain);
        _trees = _terrainBuilder.Trees;
        _world = new World(map.Terrain, _pathGrid, _trees);
        _world.ShowDamageNumbers = ShowDamageNumbers;
        _world.ObserverPlayer = ViewPlayer;
        _world.FogVisible = (x, z) => _fog == null || !_fog.Enabled
            || _fog.IsVisible(new Vector3(x, 0f, z));
        _selection = new PlayerSelectionController(_world, () => ViewPlayer,
            OnSelectionChanged, _selected);
        _selection.SelectedAck = (unit, wasSelected) => PlaySelectionVoice(unit, wasSelected);
        _commandProcessor = new PlayerCommandProcessor(_world, _selection,
            () => FormationMovementEnabled, _ => { });
        _commandProcessor.OrderAck = attack =>
        {
            if (PrimarySelection == null) return;
            float duration = _ackSounds?.PlayOrderAck(PrimarySelection, attack) ?? 0f;
            _newHud?.PlayPortraitTalk(PrimarySelection, duration);
        };
        _world.Died += OnUnitDied;
        _world.GoldDeposited += OnGoldDeposited;
        _world.LumberDeposited += OnLumberDeposited;
        _world.MissileFired += OnMissileFired;
        _world.Allied = AreAllied;
        _world.PlayerControlQuery = ShareUnitControl;
        _world.AttackLanded += OnAttackLanded;
        _world.UnitAttacked += OnUnitAttacked;
        _world.DamageApplied += OnDamageApplied;
        _world.ManaBurned += ShowManaBurnFloat;
        _world.AttackMissed += ShowMissFloat;
        _world.TreeChopped += OnTreeChopped;
        _world.ConstructionFinished += OnConstructionFinished;
        _world.BountyAwarded += OnBountyAwarded;
        _world.ItemSpawned += OnItemSpawned;
        _world.ItemPickedUp += OnItemPickedUp;
        _world.ItemDropped += OnItemDropped;
        _world.AbilityCast += OnAbilityCast;
        _world.SummonHandler = SummonUnitFor;
        _world.AbilityMissileFired += OnAbilityMissileFired;
        _world.ChannelStarted += OnChannelStarted;
        _world.ChannelWave += OnChannelWave;
        _world.ChannelEnded += OnChannelEnded;
        _world.HeroRevived += OnHeroRevived;
        _world.UnitMorphed += OnUnitMorphed;
        _world.UnitRemoved += OnUnitRemoved;
        _world.TrySpendGold = (player, amount) =>
        {
            PlayerSlot slot = Slot(player);
            if (slot == null || slot.Gold < amount) return false;
            slot.Gold -= amount;
            return true;
        };

        await Loading(.56f, StageName(LoadingStage.WorldConfiguration));
    }

    private async Task LoadPlacedUnits()
    {
        SpawnUnits();
        foreach (Unit unit in _allUnits) _world.Units.Add(unit);
        foreach (Unit unit in _allUnits) _world.RecordHeroAcquisition(unit);

        await Loading(.66f, StageName(LoadingStage.PlacedUnits));
    }

    private async Task LoadPresentationPreload()
    {
        if (System.Environment.GetEnvironmentVariable("OPENWC3_PRELOAD_MODELS") != "0")
            PreloadPresentationResources();

        await Loading(.86f, StageName(LoadingStage.PresentationPreload));
    }

    private async Task LoadPathing(MapSource map)
    {
        _fog = new FogOfWar { Name = "Fog" };
        AddChild(_fog);
        _fog.Enabled = FogEnabled;
        _fog.Build(map.Terrain);
        GD.Print($"[INIT] 寻路 {(map.Pathing == null ? "缺 wpm" : $"{map.Pathing.Width}x{map.Pathing.Height}")}"
            + $"，网格就绪 {_pathGrid?.Ready == true}，树 {_trees.Count} 棵");

        await Loading(.93f, StageName(LoadingStage.Pathing));
    }

    private async Task LoadSelection(MapSource map)
    {
        _userSettings ??= GameUserSettings.Load();
        CameraDistanceScale = War3Camera.ClampScale(_userSettings.CameraDistanceScale);
        CameraPanSensitivity = War3Camera.ClampScale(_userSettings.PanSensitivity);
        ApplyFrameRateLimit(_userSettings.FrameRateLimit);
        ApplyRenderScale(_userSettings.RenderScale);
        OpenWC3.UI.UiFontScale.Set(_userSettings.UiFontScale);

        _camera = new War3Camera(this, CameraTargetHeightAt, map);
        _camera.SetPlayerDistanceScale(CameraDistanceScale);
        _cameraComponent = new War3CameraComponent(this);
        _picking = new War3PickingComponent(this);
        _worldInteraction = new War3WorldInteractionComponent(this);
        _inputComponent = new War3InputComponent(this);
        SetCameraBounds();
        FocusStartPosition();

        _audio = new War3AudioSystem(this);
        _ackSounds = new War3AckSoundService(_audio);
        _uiSounds = new War3UiSoundService(_audio);
        _music = new War3MusicPlayer(this);
_loopingSounds = new War3LoopingSoundService(_audio,
            (x, z) => MapWorldBuilder.HeightAt(_map.Terrain, x, z));
        _unitAudibleForAudio = UnitAudibleForAudio;
        _audio.SetVolume(_userSettings.SoundVolume);
        _music.SetUserVolume(_userSettings.MusicVolume);
        _weather?.SetVolume(_userSettings.SoundVolume);

        MiscRules.Log();
        MeleeRules.Log(IsReignOfChaos);
        HeroRules.Log();
        _minimapBaseTexture = LoadMinimapBaseTexture(_map);
        var overlayLayer = new CanvasLayer { Name = "BattleOverlay", Layer = 20 };
        AddChild(overlayLayer);
        _battleOverlay = new BattleOverlayRenderer
        {
            Name = "StatusBars",
            Camera = _camera?.Camera,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overlayLayer.AddChild(_battleOverlay);
        _battleOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _selectionCircles = new SelectionCircleRenderer { Name = "SelectionCircles" };
        AddChild(_selectionCircles);
        _unitShadows = new UnitShadowRenderer { Name = "UnitShadows" };
        AddChild(_unitShadows);
        var hudLayer = new CanvasLayer { Name = "Hud", Layer = 45 };
        AddChild(hudLayer);
        _newHud = new GameHudScreen { Name = "HudScreen" };
        hudLayer.AddChild(_newHud);
        _newHud.Load(HudRaceName(), sample: false, _userSettings);
        _hud = new MeleeHudController(_newHud, War3Strings.Resolve(_map.Name))
        {
            CommandSource = BuildCommandEntries,
            UiSounds = _uiSounds,
        };
        _uiSounds?.SetPlayerRace(HudRaceName());
        _newHud.HeroBar.HeroClicked = SelectHeroFromBar;
        _newHud.SelectionClicked = OnSelectionGroupClicked;
        _newHud.IdlePeonClicked = SelectIdleWorkerUnit;
        _newHud.PortraitFollowStarted = BeginPortraitFollow;
        _newHud.PortraitFollowStopped = EndPortraitFollow;
        _newHud.ItemDragReleased = HandleInventoryDragReleased;
_newHud.AboutDialogClosed = () => _userSettings.Save();
        _newHud.UiFontScaleChanged = value =>
        {
            _userSettings.SetUiFontScale(value);
            OpenWC3.UI.UiFontScale.Set(value);
        };
        _newHud.AboutDialogExitRequested = Back;
        _newHud.ControlGroupClicked = RecallControlGroup;
        ConfigureNewMinimap(_map);
        GD.Print($"[HUD] 界面已接管，种族 {HudRaceName()}");
        _escMenu = new EscMenuFrame(this, Back);
        _escMenu.PauseToggled = TogglePause;
        _escMenu.OptionsRequested = () => _newHud.ShowAboutDialog();
        _gameOverPanel = new GameOverPanel(this, Back);
        _log = _newHud.Chat;
        _log.Add("Enter 输入消息");
        _hud.SetSelection(_selected, PrimarySelection);
        RefreshHud();
        UpdateCommandCard();
        LogUnitSummary();

        await Loading(.98f, StageName(LoadingStage.Selection));
    }

    private void AttachBehaviours()
    {
        _musicPending = MusicEnabled;
        if (!AiEnabled) return;
        foreach (PlayerSlot slot in Slots()) BuildPlan(slot);
    }

    // 阶段之间让出一帧, 该帧的 _Process/_PhysicsProcess 被 LoadingBlocked 挡住
    private async Task Loading(float progress, string stage)
    {
        _loadingScreen?.Report(progress, stage);
        if (_loadingScreen == null) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private bool LoadingBlocked => _loadingScreen != null && !_loadingScreen.Finished;

    private bool _musicPending;
    private WeatherSystem _weather;
        private readonly List<Unit> _heroScratch = new();
    private ChatFrame _log;
    private EscMenuFrame _escMenu;
    private War3LoadingScreenRenderer _loadingScreen;
    private GameOverPanel _gameOverPanel;
    private SkyBackground _skyBackground;
    private Node3D _worldRoot;
    private readonly RallyPointDisplay _rallyFlags = new();
    private float _rallyTimer;

    public override void _Ready()
    {
        // 场景树外的 AudioStreamPlayer 不能 Play；真正开播等加载屏走完（见 StartPendingMusic）
    }

    // 客户端在加载屏 dispose 之后才进第一帧，音乐与模拟都从那时开始
    private void StartPendingMusic()
    {
        if (!_musicPending || !MusicEnabled) return;
        _musicPending = false;
        int musicVersion = IsReignOfChaos ? 0 : 1;
        _music?.Start(RaceMusicName(ViewPlayer), musicVersion);
    }

    public float MapOffsetX => _map.Terrain.OffsetX;
    public float MapOffsetY => _map.Terrain.OffsetY;

    private void ConfigureNewMinimap(MapSource map)
    {
        int cellsX = Mathf.Max(1, map.Terrain.Width - 1);
        int cellsY = Mathf.Max(1, map.Terrain.Height - 1);
        _mapUiBounds = new NVector4(0f, 0f, cellsX * MeleeRules.CellWidth,
            cellsY * MeleeRules.CellWidth);

        MinimapFrame minimap = _newHud.Minimap;
        minimap.SetMapTexture(_minimapBaseTexture == null ? null : new GodotUiTexture(_minimapBaseTexture));
        minimap.OnMinimapClick = world =>
            _camera.Focus(new Vector3(-world.X, _camera.Target.Y, world.Y));
        minimap.OnSignalPing = PingMinimap;
        minimap.AltHeld = () => Input.IsKeyPressed(Key.Alt);
        minimap.FilterChanged = (allyState, showCreeps, allyEnabled, creepEnabled, formationEnabled) =>
        {
            MinimapFilter = (allyState, showCreeps, allyEnabled, creepEnabled, formationEnabled);
            FormationMovementEnabled = formationEnabled;
        };
        minimap.SetFilterState(MinimapFilter.AllyState, MinimapFilter.ShowCreeps,
            MinimapFilter.AllyEnabled, MinimapFilter.CreepEnabled, FormationMovementEnabled);
        minimap.Update(_mapUiBounds, Array.Empty<MinimapUnitView>(), ViewPlayer, ViewPlayer,
            FogTexture(), System.Numerics.Vector2.One, _minimapPings, _minimapIcons, 0f);
    }

    private GodotUiTexture FogTexture()
    {
        if (_fog?.MinimapTexture == null || !_fog.Enabled) return null;
        return _minimapFogTexture ??= new GodotUiTexture(_fog.MinimapTexture);
    }

    private void LogUnitSummary()
    {
        var byPlayer = new Dictionary<int, int>();
        int neutralPassive = 0;
        int neutralHostile = 0;
        int mines = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit.Gold > 0) mines++;
            if (unit.Player is >= 0 and <= 11 && IsActivePlayer(unit.Player))
            {
                byPlayer.TryGetValue(unit.Player, out int count);
                byPlayer[unit.Player] = count + 1;
            }
            else if (unit.Player == MeleeRules.PlayerNeutralPassive) neutralPassive++;
            else if (unit.Player == MeleeRules.PlayerNeutralHostile) neutralHostile++;
        }

        var parts = new List<string>();
        var ids = new List<int>(byPlayer.Keys);
        ids.Sort();
        foreach (int id in ids) parts.Add($"P{id}={byPlayer[id]}");

        GD.Print($"[INIT] 单位汇总 {(parts.Count > 0 ? string.Join(" ", parts) : "无玩家单位")}"
            + $" 中立被动={neutralPassive} 中立敌对={neutralHostile} 金矿/矿点={mines} 总计={_allUnits.Count}");
    }

    // 客户端 Blizzard.j:7903 用 VersionGet()（运行版本）决定 750/200 还是 500/150。
    // Launcher 是 TFT 客户端（读 TFT 表、TFT 平衡），没有 RoC 运行模式，故恒为 false。
    // 不能按地图文件头 FormatVersion 判定——RoC 格式地图在 TFT 里跑，资源仍是 500/150。
    private bool IsReignOfChaos => false;


    private void OnSelectionChanged()
    {
        _uiSounds?.PlaySelectionChanged();
        foreach (Unit unit in _allUnits)
            unit.View?.SetSelected(_selected.Contains(unit));
        RefreshSelection();
        UpdateCommandCard();
    }

    private bool HeroBarIncludesPlayer(int player)
    {
        if (player == ViewPlayer) return true;
        if (_map?.Info?.Forces == null) return false;
        foreach (MapForceInfo force in _map.Info.Forces)
        {
            if (force.ContainsPlayer(ViewPlayer)
                && (force.Flags & MapForceInfo.SharedControlFlag) != 0
                && force.ContainsPlayer(player)) return true;
        }
        return false;
    }

    private void RefreshHeroBar()
    {
        _heroScratch.Clear();
        if (_world != null)
        {
            foreach (Unit unit in _world.HeroUiCandidates())
            {
                if (unit == null || !HeroBarIncludesPlayer(unit.Player)
                    || !HeroPresentation.ShowInHeroBar(unit)) continue;
                _heroScratch.Add(unit);
            }
        }
        Unit selected = PrimarySelection;
        _newHud?.SetHeroes(_heroScratch, selected);
    }

    private bool TrySelectHeroByIndex(int index)
    {
        int current = 0;
        IReadOnlyList<Unit> heroes = _world?.HeroUiCandidates() ?? Array.Empty<Unit>();
        foreach (Unit unit in heroes)
        {
            if (unit == null || !HeroBarIncludesPlayer(unit.Player) || !unit.Alive
                || !HeroPresentation.ShowInHeroBar(unit)) continue;
            if (current++ != index) continue;
            SelectHeroFromBar(unit);
            return true;
        }
        return false;
    }

    private void SelectIdleWorker() => _newHud?.TriggerIdlePeon();

    private void SelectIdleWorkerUnit(Unit worker)
    {
        if (worker == null || !worker.Alive) return;
        _selection?.SelectSingle(worker);
        _camera.Focus(worker.Position);
    }

    private void SelectHeroFromBar(Unit hero)
    {
        if (hero == null || !hero.Alive || hero.Player != ViewPlayer) return;

        if (ReferenceEquals(PrimarySelection, hero))
        {
            _camera.Focus(hero.Position);
            return;
        }

        _selection?.SelectSingle(hero);
    }

    private static float SunShadowStrength(double timeOfDay)
    {
        double hour = ((timeOfDay % 24) + 24) % 24;
        if (hour < 6 || hour >= 19) return 0f;
        if (hour < 9) return (float)((hour - 6) / 3);
        if (hour > 16) return (float)((19 - hour) / 3);
        return 1f;
    }

    private bool StatusBarVisible(Unit unit)
        => _fog == null || !_fog.Enabled || _fog.IsVisible(unit.Position);

    private float StatusBarHeightAt(float worldX, float worldZ)
        => _map == null ? 0f : MapWorldBuilder.SurfaceHeightAt(_map.Terrain, worldX, worldZ);

    private bool StatusBarEnemy(Unit unit)
        => unit.Player != ViewPlayer
            && unit.Player != MeleeRules.PlayerNeutralPassive
            && !AreAllied(unit.Player, ViewPlayer);

    public override void _Process(double delta)
    {
        float frame = (float)delta;
        _loadingScreen?.FrameSeconds(frame);
        if (LoadingBlocked) return;
        StartPendingMusic();

        _cameraComponent?.Update(frame);
        UpdatePlacementPreview();
        _terrainBuilder?.TickWater(delta);
        _fog?.TickFade((float)delta);
        _terrainShadows?.Update(SunShadowStrength(_todHours));
        TickProjectiles((float)delta);
        RenderVisualEffects((float)delta);
        UpdateWorldText();
        TickLightningBeams((float)delta);
        _weather?.FrameSeconds(_camera.Target);
        _skyBackground?.FrameSeconds(_camera?.Camera, _todHours);

        _rallyTimer -= (float)delta;
        if (_rallyTimer <= 0f && _worldRoot != null)
        {
            _rallyTimer = 0.3f;
            _rallyFlags.FrameSeconds(_worldRoot, _allUnits, unit => AreAllied(unit.Player, ViewPlayer), UnitSceneComponent.ColorOf);
        }

        TickFog((float)delta);
        _audio?.Update((float)delta);
        _music?.Update((float)delta);
        TickGroundDecals((float)delta);
        TickDepartingItems((float)delta);
        UpdateMinimap((float)delta);
        _terrainBuilder?.TickBlight((float)delta, _allUnits, _fog);
        TickVictoryCheck((float)delta);
        RefreshHeroBar();
        RefreshConstruction();
        UpdateUnitAnimation();
        _battleOverlay?.Frame(_allUnits, StatusBarVisible, StatusBarEnemy);
        _selectionCircles?.Frame(_allUnits, StatusBarHeightAt, StatusBarEnemy, StatusBarVisible);
        _unitShadows?.Frame(_allUnits, StatusBarHeightAt, StatusBarVisible);
        for (int index = 0; index < _allUnits.Count; index++)
        {
            Unit shaped = _allUnits[index];
            if (shaped?.View == null || shaped.View.FogMemorial) continue;
            shaped.View.SyncPlacement(shaped, (float)delta, StatusBarHeightAt);
            shaped.View.SyncAppearance(shaped, ViewPlayer, AreAllied);
            shaped.View.SyncLookAt(shaped);
            shaped.View.ResolveRibbons();
            shaped.View.TickRibbons((float)delta);
        }
        TickTerrainLights((float)delta);
        _cursor?.Tick(frame);

        _hudTimer += (float)delta;
        if (_hudTimer >= 0.2f)
        {
            _hudTimer = 0f;
            PruneCargoedSelection();
            RefreshSelection();
            RefreshHud();
            UpdateCommandCard();
        }
    }

    public override void _ExitTree()
    {
        _cursor?.Dispose();
        _cursor = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (LoadingBlocked) return;
        if (SimulationBlocked) return;
        _world?.Advance();
        TickClock();
        // 位置只在物理步更新，循环音也必须按同一节拍判断移动，否则渲染帧会误判为已停下。
        _loopingSounds?.Update(_world?.Units, (float)delta, _unitAudibleForAudio);
        TickOrders((float)delta);
        if (Aggression) TickAiWaves((float)delta);
        if (AiEnabled)
        {
            foreach (PlayerSlot slot in Slots()) TickAi(slot, (float)delta);
        }
    }

    private void Back()
    {
        _userSettings?.Save();
        _music?.Stop();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        QueueFree();
        _back?.Invoke();
    }
}
