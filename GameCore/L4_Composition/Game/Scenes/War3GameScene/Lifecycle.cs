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
    private NVector4 _mapUiBounds;
    private GodotUiTexture _minimapFogTexture;
    private Texture2D _minimapBaseTexture;
    public bool UseNewHud;
    public int SelectedRace;
    private War3AudioSystem _audio;
    private War3MusicPlayer _music;
    private FogOfWar _fog;
    private float _fogTimer;
    private Action _back;

    private readonly List<Unit> _allUnits = new();
    private readonly List<Unit> _selected = new();
        private Node3D _placementGhost;
    private MeshInstance3D _placementFootprintNode;
    private ImmediateMesh _placementFootprintMesh;
    private bool _placementPreviewValid;
    private int _idleWorkerCursor;
    private float _hudTimer;
    private PathGrid _pathGrid;
    private List<TreeState> _trees = new();
    private string _placingId;

    /// <summary>暂停只停模拟层与玩家镜头输入，脚本/界面照常存在。</summary>
    public bool Paused;

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
        foreach (W3iPlayer player in _map.Players)
        {
            if (player.Id == ViewPlayer) { race = player.Race; break; }
        }
        return race switch { 2 => "Orc", 3 => "Undead", 4 => "NightElf", _ => "Human" };
    }

    public void Setup(MapSource map, Action back)
    {
        _map = map;
        _back = back;
        _loadingScreen = new FdfLoadingScreen(this, map, SelectedRace, seconds: LoadHoldSeconds);
        _ = RunSetupAsync(map);
    }

    // 加载完成（加载屏淡出前）触发, 之后的镜头预设与调试入口都挂在这里
    public event Action Loaded;

    private const float LoadHoldSeconds = 1.2f;

    private async Task RunSetupAsync(MapSource map)
    {
        try
        {
            await SetupSteps(map);
        }
        catch (Exception error)
        {
            GD.PushError($"[INIT] 场景初始化失败: {error}");
        }
        _loadingScreen?.Finish();
        _cursor = War3CursorController.TryCreate(this, HudRaceName());
        Loaded?.Invoke();
    }

    // 逐帧推进: 每个阶段单独占一帧, 加载屏才有机会把进度画出来
    private async Task SetupSteps(MapSource map)
    {


        await Loading(.10f, "解析地形与地图配置");

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

        await Loading(.22f, "载入地形纹理");
        _terrainBuilder = new MapWorldBuilder();
        Node3D worldRoot = _terrainBuilder.Build(map.Terrain, map.Doodads);
        AddChild(worldRoot);
        _worldRoot = worldRoot;

        _weather = WeatherRenderer.TryCreate(this, map.Info?.Environment?.GlobalWeather);
        if (_weather != null) GD.Print($"[WEATHER] {map.Info?.Environment?.GlobalWeather} 已加载");

        _skybox = SkyboxRenderer.TryCreate(this, map.Info);
        _skyDome = SkyDomeRenderer.TryCreate(this, map.Info);

        await Loading(.34f, "建立地形、水面与光照");
        _fog = new FogOfWar { Name = "Fog" };
        AddChild(_fog);
        _fog.Enabled = FogEnabled;
        _fog.Build(map.Terrain);

        await Loading(.45f, "载入模型与场景资源");
        SpawnUnits();
        await Loading(.66f, "创建地图预摆单位");

        await Loading(.80f, "构建寻路与碰撞网格");
        _pathGrid = PathGrid.Build(map.Pathing, map.Terrain);
        _trees = _terrainBuilder.Trees;
        _world = new World(map.Terrain, _pathGrid, _trees);
        _world.ShowDamageNumbers = ShowDamageNumbers;
        foreach (Unit unit in _allUnits) _world.Units.Add(unit);
        _world.FogVisible = (x, z) => _fog == null || !_fog.Enabled
            || _fog.IsVisible(new Vector3(x, 0f, z));
        _selection = new PlayerSelectionController(_world, () => ViewPlayer,
            OnSelectionChanged, _selected);
        _selection.SelectedAck = (_, _) => PlaySelectionVoice();
        _commandProcessor = new PlayerCommandProcessor(_world, _selection, () => true, _ => { });
        _commandProcessor.OrderAck = attack =>
        {
            if (_selected.Count > 0) PlayVoice(_selected[0], attack ? "YesAttack" : "Yes");
        };
        _world.Died += OnUnitDied;
        _world.GoldDeposited += OnGoldDeposited;
        _world.LumberDeposited += OnLumberDeposited;
        _world.MissileFired += OnMissileFired;
        _world.Allied = AreAllied;
        _world.AttackLanded += OnAttackLanded;
        _world.DamageApplied += OnDamageApplied;
        _world.ManaBurned += ShowManaBurnFloat;
        _world.AttackMissed += ShowMissFloat;
        _world.TreeChopped += OnTreeChopped;
        _world.ConstructionFinished += OnConstructionFinished;
        _world.BountyAwarded += OnBountyAwarded;
        _world.ItemSpawned += OnItemSpawned;
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

        await Loading(.90f, "预热单位与界面资源");
        _camera = new War3Camera(this, CameraTargetHeightAt);
        _cameraComponent = new War3CameraComponent(this);
        _picking = new War3PickingComponent(this);
        _worldInteraction = new War3WorldInteractionComponent(this);
        _inputComponent = new War3InputComponent(this);
        SetCameraBounds();
        FocusStartPosition();

        War3SoundTables.Load();
        _audio = new War3AudioSystem(this);
        _music = new War3MusicPlayer(this);

        await Loading(.98f, "初始化镜头与游戏界面");
        MiscRules.Log();
        MeleeRules.Log(IsReignOfChaos);
        HeroRules.Log();
        _minimapBaseTexture = LoadMinimapBaseTexture(map);
        _hud = new MeleeHudController(this, War3Strings.Resolve(map.Name));
        if (UseNewHud)
        {
            GetNodeOrNull<CanvasLayer>("MeleeHudController")?.SetVisible(false);
            var hudLayer = new CanvasLayer { Name = "NewHud", Layer = 45 };
            AddChild(hudLayer);
            _newHud = new GameHudScreen { Name = "NewHudScreen" };
            hudLayer.AddChild(_newHud);
            _newHud.Load(HudRaceName(), sample: false);
            _newHud.HeroBar.HeroClicked = SelectHeroFromBar;
            ConfigureNewMinimap(map);
            GD.Print($"[HUD] 新界面已接管，种族 {HudRaceName()}");
        }
        _escMenu = new EscMenuFrame(this, Back);
        _escMenu.PauseToggled = TogglePause;
        _gameOverPanel = new GameOverPanel(this, Back);
        _hud.DetailProvider = DescribeUnit;
        _hud.SelectionSlotClicked = unit =>
        {
            if (unit == null || !unit.Alive) return;
            ClearSelection();
            _selected.Add(unit);
            unit.View?.SetSelected(true);
            _hud.SetSelection(_selected);
            UpdateCommandCard();
            PlaySelectionVoice();
        };
        _hud.MinimapFrame.Configure(map.Terrain, () => _allUnits, () => _camera.Target, point => _camera.Focus(point),
            unit => unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer) || _fog.IsVisible(unit.Position));
        _hud.MinimapFrame.RightNavigate = _worldInteraction.MinimapOrderAt;
        _hud.MinimapFrame.SetFogTexture(_fog.Texture);
        _hud.MinimapFrame.SetTexture(_minimapBaseTexture);
        if (!UseNewHud)
        {
            _heroBar = new LegacyHeroBarFrame(this);
            _heroBar.HeroClicked = SelectHeroFromBar;
        }
        _log = new ChatFrame(this);
        _log.Add("Enter 输入消息");
        _hud.SetSelection(_selected);
        RefreshHud();
        UpdateCommandCard();
        LogUnitSummary();

        _musicPending = MusicEnabled;

        if (AiEnabled)
        {
            foreach (PlayerSlot slot in Slots()) BuildPlan(slot);
        }

        await Loading(1f, "进入游戏");
    }

    // 阶段之间让出一帧, 该帧的 _Process/_PhysicsProcess 被 LoadingBlocked 挡住
    private async Task Loading(float progress, string stage)
    {
        _loadingScreen?.SetProgress(progress, stage);
        if (_loadingScreen == null) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private bool LoadingBlocked => _loadingScreen != null && !_loadingScreen.Finished;

    private bool _musicPending;
    private WeatherRenderer _weather;
    private LegacyHeroBarFrame _heroBar;
    private readonly List<Unit> _heroScratch = new();
    private ChatFrame _log;
    private EscMenuFrame _escMenu;
    private FdfLoadingScreen _loadingScreen;
    private GameOverPanel _gameOverPanel;
    private SkyboxRenderer _skybox;
    private SkyDomeRenderer _skyDome;
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
        foreach (Unit unit in _allUnits)
            unit.View?.SetSelected(_selected.Contains(unit));
        _hud?.SetSelection(_selected);
        _subgroupPool.Clear();
        _subgroupType = "";
        UpdateCommandCard();
    }

    private void RefreshHeroBar()
    {
        _heroScratch.Clear();
        if (_world != null)
        {
            foreach (Unit unit in _world.HeroUiCandidates())
            {
                if (unit == null || unit.Player != ViewPlayer
                    || !HeroPresentation.ShowInHeroBar(unit)) continue;
                _heroScratch.Add(unit);
            }
        }
        Unit selected = _selected.Count == 1 ? _selected[0] : null;
        _heroBar?.Update(_heroScratch, selected);
        _newHud?.SetHeroes(_heroScratch, selected);
    }

    private bool TrySelectHeroByIndex(int index)
    {
        int current = 0;
        IReadOnlyList<Unit> heroes = _world?.HeroUiCandidates() ?? Array.Empty<Unit>();
        foreach (Unit unit in heroes)
        {
            if (unit == null || unit.Player != ViewPlayer || !unit.Alive
                || !HeroPresentation.ShowInHeroBar(unit)) continue;
            if (current++ != index) continue;
            SelectHeroFromBar(unit);
            return true;
        }
        return false;
    }

    private void SelectIdleWorker()
    {
        var workers = new List<Unit>();
        IReadOnlyList<Unit> candidates = _world?.WorkerUiCandidates() ?? Array.Empty<Unit>();
        foreach (Unit unit in candidates)
        {
            if (unit == null || !unit.Alive || unit.Player != ViewPlayer || !unit.IsWorker) continue;
            if (unit.GoldMine != null || unit.Tree != null || unit.BuildSite != null || unit.RepairTarget != null) continue;
            if (unit.MoveTarget.HasValue || unit.Target != null || unit.BuildingNow) continue;
            workers.Add(unit);
        }
        if (workers.Count == 0) return;

        _idleWorkerCursor %= workers.Count;
        Unit worker = workers[_idleWorkerCursor];
        _idleWorkerCursor = (_idleWorkerCursor + 1) % workers.Count;

        _selection?.SelectSingle(worker);
        _camera.Focus(worker.Position);
    }

    private void SelectHeroFromBar(Unit hero)
    {
        if (hero == null || !hero.Alive || hero.Player != ViewPlayer) return;

        if (_selected.Count == 1 && ReferenceEquals(_selected[0], hero))
        {
            _camera.Focus(hero.Position);
            return;
        }

        _selection?.SelectSingle(hero);
    }

    public override void _Process(double delta)
    {
        float frame = (float)delta;
        _loadingScreen?.FrameSeconds(frame);
        if (LoadingBlocked) return;
        StartPendingMusic();

        _cameraComponent?.Update(frame);
        UpdatePlacementPreview();
        _terrainBuilder?.TickWater(delta);
        TickProjectiles((float)delta);
        TickSceneEffects((float)delta);
        TickFloatingLabels((float)delta);
        TickLightningBeams((float)delta);
        _weather?.FrameSeconds(_camera.Target);
        _skyDome?.FrameSeconds(_camera.Target);

        _rallyTimer -= (float)delta;
        if (_rallyTimer <= 0f && _worldRoot != null)
        {
            _rallyTimer = 0.3f;
            _rallyFlags.FrameSeconds(_worldRoot, _allUnits, unit => AreAllied(unit.Player, ViewPlayer), UnitSceneComponent.ColorOf);
        }

        TickFog((float)delta);
        UpdateMinimap((float)delta);
        _terrainBuilder?.Blight?.FrameSeconds((float)delta, _allUnits);
        TickVictoryCheck((float)delta);
        RefreshHeroBar();
        RefreshConstruction();
        _cursor?.Tick(frame);

        _hudTimer += (float)delta;
        if (_hudTimer >= 0.2f)
        {
            _hudTimer = 0f;
            PruneCargoedSelection();
            _hud?.SetSelection(_selected);
            RefreshBars();
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
        if (Paused) return;
        _world?.Advance();
        TickClock();
        TickOrders((float)delta);
        if (Aggression) TickAiWaves((float)delta);
        if (AiEnabled)
        {
            foreach (PlayerSlot slot in Slots()) TickAi(slot, (float)delta);
        }
    }

    private void Back()
    {
        _music?.Stop();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        QueueFree();
        _back?.Invoke();
    }
}
