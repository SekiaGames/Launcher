using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene : Node3D
{
    private MapSource _map;
    private MapWorldBuilder _terrainBuilder;
    private World _world;
    private War3Camera _camera;
    private MeleeHudController _hud;
    private War3AudioSystem _audio;
    private War3MusicPlayer _music;
    private FogOfWar _fog;
    private float _fogTimer;
    private Action _back;

    private readonly List<Unit> _allUnits = new();
    private readonly List<Unit> _selected = new();
    private bool _middleDown;
    private bool _leftDown;
    private Vector2 _dragStart;
    private Node3D _placementGhost;
    private MeshInstance3D _placementFootprintNode;
    private ImmediateMesh _placementFootprintMesh;
    private bool _placementPreviewValid;
    private int _idleWorkerCursor;
    private float _hudTimer;
    private PathGrid _pathGrid;
    private List<TreeState> _trees = new();
    private string _placingId;

    /// <summary>暂停只停模拟层（世界推进与 AI），相机/UI 照常响应。</summary>
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

    public void Setup(MapSource map, Action back)
    {
        _map = map;
        _back = back;

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

        _terrainBuilder = new MapWorldBuilder();
        Node3D worldRoot = _terrainBuilder.Build(map.Terrain, map.Doodads);
        AddChild(worldRoot);
        _worldRoot = worldRoot;

        _weather = WeatherRenderer.TryCreate(this, map.Info?.Environment?.GlobalWeather);
        if (_weather != null) GD.Print($"[WEATHER] {map.Info?.Environment?.GlobalWeather} 已加载");

        _skybox = SkyboxRenderer.TryCreate(this, map.Info);
        _skyDome = SkyDomeRenderer.TryCreate(this, map.Info);

        _fog = new FogOfWar { Name = "Fog" };
        AddChild(_fog);
        _fog.Enabled = FogEnabled;
        _fog.Build(map.Terrain);

        SpawnUnits();

        _pathGrid = PathGrid.Build(map.Pathing, map.Terrain);
        _trees = _terrainBuilder.Trees;
        _world = new World(map.Terrain, _pathGrid, _trees);
        _world.ShowDamageNumbers = ShowDamageNumbers;
        foreach (Unit unit in _allUnits) _world.Units.Add(unit);
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

        _camera = new War3Camera(this);
        SetCameraBounds();
        FocusStartPosition();

        War3SoundTables.Load();
        _audio = new War3AudioSystem(this);
        _music = new War3MusicPlayer(this);

        MiscRules.Log();
        MeleeRules.Log(IsReignOfChaos);
        HeroRules.Log();
        _hud = new MeleeHudController(this, War3Strings.Resolve(map.Name));
        _escMenu = new EscMenuFrame(this, Back);
        _escMenu.PauseToggled = TogglePause;
        _loadingScreen = new LoadingScreen(this, map);
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
        _hud.MinimapFrame.RightNavigate = MinimapOrderAt;
        _hud.MinimapFrame.SetFogTexture(_fog.Texture);
        _hud.MinimapFrame.SetTexture(MinimapFrame.BuildTexture(map.Terrain, new TerrainTextureLoader()));
        _heroBar = new HeroBarFrame(this);
        _heroBar.HeroClicked = SelectHeroFromBar;
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
    }

    private bool _musicPending;
    private WeatherRenderer _weather;
    private HeroBarFrame _heroBar;
    private readonly List<Unit> _heroScratch = new();
    private ChatFrame _log;
    private EscMenuFrame _escMenu;
    private LoadingScreen _loadingScreen;
    private GameOverPanel _gameOverPanel;
    private SkyboxRenderer _skybox;
    private SkyDomeRenderer _skyDome;
    private Node3D _worldRoot;
    private readonly RallyPointDisplay _rallyFlags = new();
    private float _rallyTimer;

    public override void _Ready()
    {
        // 场景树外的 AudioStreamPlayer 不能 Play，音乐等进树再起
        if (!_musicPending || !MusicEnabled) return;
        _musicPending = false;
        int musicVersion = IsReignOfChaos ? 0 : 1;
        _music?.Start(RaceMusicName(ViewPlayer), musicVersion);
    }

    public float MapOffsetX => _map.Terrain.OffsetX;
    public float MapOffsetY => _map.Terrain.OffsetY;

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


    private void RefreshHeroBar()
    {
        if (_heroBar == null) return;
        _heroScratch.Clear();
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.IsHero || unit.Player != ViewPlayer) continue;
            _heroScratch.Add(unit);
        }
        Unit selected = _selected.Count == 1 ? _selected[0] : null;
        _heroBar.Update(_heroScratch, selected);
    }

    private bool TrySelectHeroByIndex(int index)
    {
        int current = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.IsHero || unit.Player != ViewPlayer || !unit.Alive) continue;
            if (current++ != index) continue;
            SelectHeroFromBar(unit);
            return true;
        }
        return false;
    }

    private void SelectIdleWorker()
    {
        var workers = new List<Unit>();
        foreach (Unit unit in _allUnits)
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

        ClearSelection();
        _selected.Add(worker);
        worker.View?.SetSelected(true);
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        _camera.Focus(worker.Position);
        PlaySelectionVoice();
    }

    private void SelectHeroFromBar(Unit hero)
    {
        if (hero == null || !hero.Alive || hero.Player != ViewPlayer) return;

        if (_selected.Count == 1 && ReferenceEquals(_selected[0], hero))
        {
            _camera.Focus(hero.Position);
            return;
        }

        ShowHeroSubgroup(hero);
        PlaySelectionVoice();
    }

    public override void _Process(double delta)
    {
        HandleKeyboardPan((float)delta);
        UpdatePlacementPreview();
        _terrainBuilder?.TickWater(delta);
        TickProjectiles((float)delta);
        TickSceneEffects((float)delta);
        TickFloatingLabels((float)delta);
        TickLightningBeams((float)delta);
        _weather?.Tick(_camera.Target);
        _skyDome?.Tick(_camera.Target);
        _loadingScreen?.Tick((float)delta);

        _rallyTimer -= (float)delta;
        if (_rallyTimer <= 0f && _worldRoot != null)
        {
            _rallyTimer = 0.3f;
            _rallyFlags.Tick(_worldRoot, _allUnits, unit => AreAllied(unit.Player, ViewPlayer), UnitSceneComponent.ColorOf);
        }

        TickFog((float)delta);
        _terrainBuilder?.Blight?.Tick((float)delta, _allUnits);
        TickVictoryCheck((float)delta);
        RefreshHeroBar();
        RefreshConstruction();

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

    public override void _PhysicsProcess(double delta)
    {
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
