using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class GameScreen : Node3D
{
    private MapSource _map;
    private MapWorldBuilder _world;
    private BattleSim _sim;
    private War3CameraRig _camera;
    private Hud _hud;
    private Action _back;

    private readonly List<UnitState> _allUnits = new();
    private readonly List<UnitState> _selected = new();
    private bool _middleDown;
    private bool _leftDown;
    private Vector2 _dragStart;
    private float _hudTimer;
    private PathGrid _pathGrid;
    private List<TreeState> _trees = new();
    private string _placingId;

    /// <summary>-1 表示跟随客户端 melee 模板（TFT 500/150、RoC 750/200）。</summary>
    public int StartGold = -1;
    public int StartLumber = -1;
    public float TimeScaleHint = 1f;
    public bool Aggression;
    public bool AiEnabled = true;

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

    private sealed class PlayerSlot
    {
        public int Id;
        public int Gold;
        public int Lumber;
        public bool Ai = true;
        public bool Done;
        public bool Announced;
        public float Timer;
        public float StatusTimer;
        public readonly HashSet<string> KnownBuildings = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> Research = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> BuiltCounts = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<InhouseTask> Tasks = new();

        public int ResearchLevel(string id) => Research.TryGetValue(id, out int level) ? level : 0;
        public int BuiltCount(string id) => BuiltCounts.TryGetValue(id, out int count) ? count : 0;
    }

    private sealed class InhouseTask
    {
        public string Kind;
        public string Id;
        public int Total = 1;
        public string Label = "";
    }

    private enum OrderKind
    {
        Train,
        Research,
        Upgrade,
    }

    private sealed class BuildingOrder
    {
        public int Player;
        public UnitState Building;
        public OrderKind Kind;
        public string Id;
        public int Level;
        public float Remaining;
        public float Total;
    }

    private readonly PlayerSlot[] _players = new PlayerSlot[24];

    private PlayerSlot Slot(int id)
    {
        if (id < 0 || id >= _players.Length) return null;
        return _players[id] ??= new PlayerSlot { Id = id };
    }

    private IEnumerable<PlayerSlot> Slots()
    {
        foreach (int id in _active)
        {
            PlayerSlot slot = Slot(id);
            if (slot != null) yield return slot;
        }
    }

    private readonly List<BuildingOrder> _orders = new();

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

        _world = new MapWorldBuilder();
        Node3D worldRoot = _world.Build(map.Terrain, map.Doodads);
        AddChild(worldRoot);

        SpawnUnits();

        _pathGrid = PathGrid.Build(map.Pathing, map.Terrain);
        _trees = _world.Trees;
        _sim = new BattleSim(map.Terrain, _pathGrid, _trees);
        foreach (UnitState unit in _allUnits) _sim.Units.Add(unit);
        _sim.Died += OnUnitDied;
        _sim.GoldDeposited += OnGoldDeposited;
        _sim.LumberDeposited += OnLumberDeposited;
        _sim.MissileFired += OnMissileFired;
        _sim.Allied = AreAllied;

        _camera = new War3CameraRig(this);
        SetCameraBounds();
        FocusStartPosition();

        MiscRules.Log();
        MeleeRules.Log(IsReignOfChaos);
        _hud = new Hud(this, War3Strings.Resolve(map.Name));
        _hud.DetailProvider = DescribeUnit;
        _hud.Minimap.Configure(map.Terrain, () => _allUnits, () => _camera.Target, point => _camera.Focus(point));
        _hud.Minimap.SetTexture(Minimap.BuildTexture(map.Terrain, new TerrainTileSet()));
        _hud.SetSelection(_selected);
        RefreshHud();
        UpdateCommandCard();
        LogUnitSummary();

        if (AiEnabled)
        {
            foreach (PlayerSlot slot in Slots()) BuildPlan(slot);
        }
    }

    public float MapOffsetX => _map.Terrain.OffsetX;
    public float MapOffsetY => _map.Terrain.OffsetY;

    private void LogUnitSummary()
    {
        var byPlayer = new Dictionary<int, int>();
        int neutralPassive = 0;
        int neutralHostile = 0;
        int mines = 0;
        foreach (UnitState unit in _allUnits)
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

    private UnitState FindHall(int player)
    {
        UnitState fallback = null;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || !unit.IsBuilding) continue;
            if (unit.Type.FoodMade > 0 && unit.Type.Trains.Length > 0) return unit;
            if (fallback == null && unit.Type.Trains.Length > 0) fallback = unit;
        }
        return fallback;
    }

    private bool IsReignOfChaos => _map.Info == null || _map.Info.FormatVersion < 25;

    private void ResolveActivePlayers()
    {
        _active.Clear();
        _playerNames.Clear();

        var seats = new List<W3iPlayer>();
        foreach (W3iPlayer player in _map.Players)
        {
            if (!MeleeRules.IsPlayingController(player.Controller)) continue;
            if (player.StartX == 0f && player.StartY == 0f) continue;
            seats.Add(player);
            _playerNames[player.Id] = War3Strings.Resolve(player.Name);
        }

        if (seats.Count == 0)
        {
            GD.Print("[INIT] w3i 没有可玩座位，退回 P0");
            _active.Add(0);
            ViewPlayer = 0;
            return;
        }

        if (ActiveSlots.Count > 0)
        {
            foreach (W3iPlayer player in seats)
            {
                if (ActiveSlots.Contains(player.Id)) _active.Add(player.Id);
            }
        }
        else if (ActivePlayers > 0)
        {
            for (int i = 0; i < seats.Count && i < ActivePlayers; i++) _active.Add(seats[i].Id);
        }
        else
        {
            foreach (W3iPlayer player in seats) _active.Add(player.Id);
        }

        if (_active.Count == 0) _active.Add(seats[0].Id);
        if (!_active.Contains(ViewPlayer)) ViewPlayer = seats[0].Id;

        BuildAlliances();

        var active = new List<int>(_active);
        active.Sort();
        GD.Print($"[INIT] 地图可玩座位 {seats.Count} 个（{string.Join(" ", seats.ConvertAll(p => $"P{p.Id}/{RaceName(p.Race)}"))}），"
            + $"本局参战 {active.Count} 个：{string.Join(" ", active.ConvertAll(id => $"P{id}"))}");
    }

    /// <summary>
    /// 同盟来自 w3i 的 forces（同一 force 且带 AlliedFlag），没有 forces 时退回玩家的 ally 位掩码。
    /// 客户端 GetPlayerAlliance 读的就是这份初始状态。
    /// </summary>
    private void BuildAlliances()
    {
        _allyMasks.Clear();

        if (Teams.Count > 0)
        {
            foreach (List<int> team in Teams)
            {
                int mask = 0;
                foreach (int id in team) mask |= 1 << id;
                foreach (int id in team) _allyMasks[id] = mask;
            }
            var lobby = new List<string>();
            foreach (List<int> team in Teams)
            {
                if (team.Count > 1) lobby.Add(string.Join("+", team.ConvertAll(id => $"P{id}")));
            }
            GD.Print(lobby.Count > 0 ? $"[INIT] 大厅队伍：{string.Join(" | ", lobby)}" : "[INIT] 大厅队伍：无（各自为战）");
            return;
        }

        if (_map.Info != null && _map.Info.Forces.Count > 0)
        {
            foreach (W3iForce force in _map.Info.Forces)
            {
                if ((force.Flags & W3iForce.AlliedFlag) == 0) continue;
                foreach (W3iPlayer player in _map.Players)
                {
                    if (!force.ContainsPlayer(player.Id)) continue;
                    _allyMasks.TryGetValue(player.Id, out int mask);
                    _allyMasks[player.Id] = mask | (int)force.PlayerMask;
                }
            }
        }

        // 注意：w3i 的 allyLow/allyHigh 是 AI 的盟友优先级位，不是同盟关系。
        // 实测海龟岛 FFA 每人是「自己那一位」（P0=8/P1=4/P2=2/P3=1，位序还是反的），
        // 拿来判定同盟会把 FFA 误判成盟友，所以只用 forces 与大厅 --teams。

        var groups = new List<string>();
        foreach (W3iForce force in _map.Info?.Forces ?? new List<W3iForce>())
        {
            if ((force.Flags & W3iForce.AlliedFlag) == 0) continue;
            var members = new List<string>();
            foreach (W3iPlayer player in _map.Players)
            {
                if (force.ContainsPlayer(player.Id) && IsActivePlayer(player.Id)) members.Add($"P{player.Id}");
            }
            if (members.Count > 1) groups.Add(string.Join("+", members));
        }
        GD.Print(groups.Count > 0
            ? $"[INIT] 同盟队伍：{string.Join(" | ", groups)}"
            : "[INIT] 同盟队伍：无（各自为战）");
    }

    /// <summary>对局层同盟判定：同队(true)、野怪互不攻击、中立被动不参战。</summary>
    public bool AreAllied(int a, int b)
    {
        if (a == b) return true;
        if (a == MeleeRules.PlayerNeutralHostile && b == MeleeRules.PlayerNeutralHostile) return true;
        if (a is MeleeRules.PlayerNeutralHostile or MeleeRules.PlayerNeutralPassive) return false;
        if (b is MeleeRules.PlayerNeutralHostile or MeleeRules.PlayerNeutralPassive) return false;
        if (_allyMasks.TryGetValue(a, out int mask) && (mask & (1 << b)) != 0) return true;
        return false;
    }

    private static string RaceName(int race)
    {
        return race switch
        {
            1 => "人",
            2 => "兽",
            3 => "亡",
            4 => "暗夜",
            _ => $"种族{race}",
        };
    }

    private string PlayerName(int player)
    {
        if (_playerNames.TryGetValue(player, out string name) && !string.IsNullOrWhiteSpace(name)) return name;
        return $"P{player}";
    }

    /// <summary>w3i 地图坐标 → 世界坐标（与地形 mesh 的取负 X 约定一致）。</summary>
    private Vector3 ToWorld(float mapX, float mapY)
    {
        float worldX = -(mapX - _map.Terrain.OffsetX);
        float worldZ = mapY - _map.Terrain.OffsetY;
        return new Vector3(worldX, 0f, worldZ);
    }

    private Vector2 ToMap(Vector3 world)
    {
        return new Vector2(_map.Terrain.OffsetX - world.X, world.Z + _map.Terrain.OffsetY);
    }

    private void SpawnUnits()
    {
        int placed = 0;
        int missingType = 0;
        int missingScene = 0;

        if (_map.Units != null)
        {
            foreach (UnitPlacement entry in _map.Units.Units)
            {
                if (entry.IsStartingLocation) continue;

                Vector3 world = ToWorld(entry.X, entry.Y);
                var player = (int)Mathf.Clamp(entry.Player, 0, 15);
                UnitState unit = Spawn(entry.Id, player, world.X, world.Z, Mathf.Pi + entry.Angle,
                    entry.Hitpoints > 0 ? entry.Hitpoints : 0f, entry.ScaleX, entry.Gold);

                if (unit != null) placed++;
                else if (UnitCatalog.Get(entry.Id) == null) missingType++;
                else missingScene++;
            }
        }

        int cleared = ClearExcessUnits();
        int melee = SpawnMeleeStarts();

        GD.Print($"[INIT] 地图单位 {placed} 个（缺类型 {missingType} 缺模型 {missingScene}），"
            + $"出生点清场移除 {cleared} 个，开局基地单位 {melee} 个");
    }

    /// <summary>MeleeClearExcessUnits：参战座位 1500 内清中立敌对、以及中立被动的非建筑。</summary>
    private int ClearExcessUnits()
    {
        var starts = new List<Vector3>();
        foreach (W3iPlayer player in _map.Players)
        {
            if (!IsActivePlayer(player.Id)) continue;
            if (player.StartX == 0f && player.StartY == 0f) continue;
            starts.Add(ToWorld(player.StartX, player.StartY));
        }
        if (starts.Count == 0) return 0;

        int removed = 0;
        int hostile = 0;
        int passive = 0;
        for (int index = _allUnits.Count - 1; index >= 0; index--)
        {
            UnitState unit = _allUnits[index];
            if (!MeleeRules.ShouldClearAtStart(unit.Player, unit.IsBuilding)) continue;

            bool inside = false;
            foreach (Vector3 start in starts)
            {
                float distance = new Vector2(unit.Position.X - start.X, unit.Position.Z - start.Z).Length();
                if (distance <= MeleeRules.ClearUnitsRadius) { inside = true; break; }
            }
            if (!inside) continue;

            if (unit.Player == MeleeRules.PlayerNeutralHostile) hostile++;
            else passive++;

            unit.Alive = false;
            unit.View?.Root.QueueFree();
            _allUnits.RemoveAt(index);
            removed++;
        }

        if (removed > 0) GD.Print($"[INIT] 清场：中立敌对 {hostile} 个、中立被动非建筑 {passive} 个");
        return removed;
    }

    /// <summary>MeleeFindNearestMine：只认 ngol，搜索半径 2000（地图坐标）。</summary>
    private UnitState FindNearestGoldMine(Vector2 startMap)
    {
        UnitState best = null;
        float bestDistance = MeleeRules.MineSearchRadius;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Gold <= 0) continue;
            if (!string.Equals(unit.Type.Id, "ngol", StringComparison.OrdinalIgnoreCase)) continue;

            Vector2 mineMap = ToMap(unit.Position);
            float distance = (mineMap - startMap).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    private readonly Dictionary<int, Vector3> _startFocus = new();

    private int SpawnMeleeStarts()
    {
        _startFocus.Clear();
        int spawned = 0;

        var ordered = new List<W3iPlayer>();
        foreach (W3iPlayer player in _map.Players)
        {
            if (IsActivePlayer(player.Id)) ordered.Add(player);
            else if (player.Id is >= 0 and <= 23 && player.StartX != 0f && player.StartY != 0f)
            {
                GD.Print($"[INIT] P{player.Id} 空闲出生点 地图({player.StartX:0},{player.StartY:0})，地图原始单位保持不变");
            }
        }

        foreach (W3iPlayer player in ordered)
        {
            if (player.StartX == 0f && player.StartY == 0f) continue;

            var start = new Vector2(player.StartX, player.StartY);
            UnitState mine = FindNearestGoldMine(start);
            Vector2? mineMap = mine != null ? ToMap(mine.Position) : null;

            MeleeRules.StartPlan plan = MeleeRules.Plan(player.Race, start, mineMap);
            _startFocus[player.Id] = ToWorld(plan.Camera.X, plan.Camera.Y);

            GD.Print($"[INIT] P{player.Id} {PlayerName(player.Id)} 种族{RaceName(player.Race)}"
                + $" 起点({start.X:0},{start.Y:0}) 主基地 {plan.HallId}"
                + (mine != null ? $" 最近金矿 距离{(mineMap!.Value - start).Length():0}" : " 附近没有金矿"));

            if (plan.Units.Count == 0)
            {
                GD.PushWarning($"[INIT] P{player.Id} 种族 {player.Race} 没有开局模板");
                continue;
            }

            foreach (MeleeRules.StartUnit entry in plan.Units)
            {
                Vector3 world = ToWorld(entry.Map.X, entry.Map.Y);
                UnitState unit = Spawn(entry.Id, player.Id, world.X, world.Z, 0f, 0f, 1f);
                if (unit == null) continue;
                spawned++;
                Slot(player.Id)?.KnownBuildings.Add(entry.Id);
            }

            if (mine != null)
            {
                ApplyStartMineEffect(player, mine, plan);
            }
        }

        return spawned;
    }

    private void ApplyStartMineEffect(W3iPlayer player, UnitState mine, MeleeRules.StartPlan plan)
    {
        string replacementId = plan.Mine switch
        {
            MeleeRules.MineEffect.Haunted => "ugol",
            MeleeRules.MineEffect.Entangled => "egol",
            _ => null,
        };
        if (replacementId == null) return;

        int gold = mine.Gold;
        Vector3 position = mine.Position;
        float yaw = mine.Facing;

        mine.Alive = false;
        mine.View?.Root.QueueFree();
        _allUnits.Remove(mine);

        UnitState replacement = Spawn(replacementId, player.Id, position.X, position.Z, yaw, 0f, 1f, gold);
        if (replacement == null)
        {
            GD.PushWarning($"[INIT] P{player.Id} 金矿替换 {replacementId} 失败，矿量丢失 {gold}");
            return;
        }

        replacement.Gold = gold;
        if (plan.BlightRadius > 0f)
        {
            GD.Print($"[INIT] P{player.Id} {replacement.Type.Name} 占据金矿（储量 {gold}），荒芜半径 {plan.BlightRadius:0}");
        }
    }

    private static bool IsGoldMineType(UnitType type)
    {
        if (type == null || !type.IsBuilding || type.CanAttack) return false;
        string id = type.Id ?? "";
        return id.StartsWith("ngol", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("egol", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("ugol", StringComparison.OrdinalIgnoreCase);
    }

    private UnitState Spawn(string id, int player, float worldX, float worldZ, float yaw, float hp, float entryScale, int gold = 0)
    {
        UnitType type = UnitCatalog.Get(id);
        if (type == null) return null;

        PackedScene scene = Assets.LoadScene(type.ModelPath);
        if (scene == null)
        {
            GD.PushWarning($"[GAME] 缺模型 {id} -> {type.ModelPath}");
            return null;
        }

        float worldY = MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ);
        float scale = type.ModelScale * (entryScale > 0f ? entryScale : 1f);

        UnitView view = UnitView.Create(scene, UnitView.ColorOf(player), scale, type.IsBuilding, type.FootprintRadius);
        view.Root.Position = new Vector3(worldX, worldY, worldZ);
        view.Root.Rotation = new Vector3(0f, yaw, 0f);
        view.Root.Name = $"{id}_{_allUnits.Count}";
        AddChild(view.Root);

        float maxHp = type.Hp;
        if (hp > 0f) maxHp = hp <= 100f ? type.Hp * hp / 100f : hp;

        var unit = new UnitState
        {
            Type = type,
            Player = player,
            Position = view.Root.Position,
            Facing = yaw,
            Hp = maxHp,
            MaxHp = maxHp,
            IsBuilding = type.IsBuilding,
            Gold = IsGoldMineType(type) ? gold : 0,
            Home = view.Root.Position,
            AutoAcquire = Aggression && (player <= 3 || player == 12),
            LeashRange = player is >= 1 and <= 3 ? 1e9f : 260f,
            View = view,
        };

        if (_sim != null && type.IsBuilding)
        {
            float multiplier = _sim.HpMultiplier(unit, player);
            unit.MaxHp = maxHp * multiplier;
            unit.Hp = unit.MaxHp;
        }

        view.Play("Stand");
        view.SetHp(1f, true);
        _allUnits.Add(unit);
        return unit;
    }

    public void SetCameraPreset(int preset)
    {
        _camera?.SetPreset(preset);
    }

    public void FocusMapPoint(float mapX, float mapY)
    {
        float worldX = -(mapX - _map.Terrain.OffsetX);
        float worldZ = mapY - _map.Terrain.OffsetY;
        _camera?.Focus(new Vector3(worldX, MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ), worldZ));
    }

    private void SetCameraBounds()
    {
        // 地形覆盖范围（世界坐标 X 取负）
        float terrainMinX = -(_map.Terrain.Width - 1) * MapWorldBuilder.CellSize;
        float terrainMaxX = 0f;
        float terrainMinZ = 0f;
        float terrainMaxZ = (_map.Terrain.Height - 1) * MapWorldBuilder.CellSize;

        // w3i 的 CameraBounds 是地图坐标下的矩形，转成世界坐标后与地形取交集
        float[] bounds = _map.Info?.CameraBounds;
        if (bounds != null && bounds.Length >= 4)
        {
            float mapMinX = bounds[0];
            float mapMinY = bounds[1];
            float mapMaxX = bounds[2];
            float mapMaxY = bounds[3];
            if (mapMaxX - mapMinX > 0f && mapMaxY - mapMinY > 0f)
            {
                float worldMinX = -(mapMaxX - _map.Terrain.OffsetX);
                float worldMaxX = -(mapMinX - _map.Terrain.OffsetX);
                float worldMinZ = mapMinY - _map.Terrain.OffsetY;
                float worldMaxZ = mapMaxY - _map.Terrain.OffsetY;

                terrainMinX = Mathf.Max(terrainMinX, worldMinX);
                terrainMaxX = Mathf.Min(terrainMaxX, worldMaxX);
                terrainMinZ = Mathf.Max(terrainMinZ, worldMinZ);
                terrainMaxZ = Mathf.Min(terrainMaxZ, worldMaxZ);
            }
        }

        _camera.Bounds = new Vector4(terrainMinX, terrainMinZ, terrainMaxX, terrainMaxZ);
        GD.Print($"[INIT] 相机边界 ({terrainMinX:0},{terrainMinZ:0})-({terrainMaxX:0},{terrainMaxZ:0})");
    }

    private void FocusStartPosition()
    {
        Vector3 focus = Vector3.Zero;
        if (_startFocus.TryGetValue(ViewPlayer, out Vector3 planned))
        {
            focus = planned;
        }
        else
        {
            foreach (W3iPlayer player in _map.Players)
            {
                if (player.Id != ViewPlayer) continue;
                if (player.StartX == 0f && player.StartY == 0f) continue;
                focus = ToWorld(player.StartX, player.StartY);
                break;
            }
        }

        focus.Y = MapWorldBuilder.HeightAt(_map.Terrain, focus.X, focus.Z);
        GD.Print($"[INIT] 相机焦点 P{ViewPlayer} {focus}");
        _camera.Focus(focus);
    }

    public override void _Process(double delta)
    {
        HandleKeyboardPan((float)delta);
        _world?.TickWater(delta);
        TickProjectiles((float)delta);

        TickVictoryCheck((float)delta);

        _hudTimer += (float)delta;
        if (_hudTimer >= 0.2f)
        {
            _hudTimer = 0f;
            _hud?.SetSelection(_selected);
            RefreshConstruction();
            RefreshBars();
            RefreshHud();
            UpdateCommandCard();
        }
    }

    private sealed class Projectile
    {
        public Node3D Node;
        public Vector3 From;
        public Vector3 To;
        public float Arc;
        public float Speed;
        public float Length = 1f;
        public float Travel;
        public UnitState Target;
        public float TargetHeight = 60f;
    }

    private readonly List<Projectile> _projectiles = new();

    private void OnMissileFired(UnitState attacker, UnitState target)
    {
        if (attacker?.View == null || target == null) return;

        Node3D node = null;
        PackedScene scene = Assets.LoadScene(attacker.Type.MissileScene);
        if (scene != null)
        {
            node = scene.Instantiate<Node3D>();
            MapWorldBuilder.ApplyUnitTextureFilter(node);
        }
        else
        {
            node = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 12f, Height = 24f },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color(1f, 0.85f, 0.4f),
                },
            };
        }

        Vector3 from = attacker.Position + new Vector3(0f, attacker.View.Height * 0.6f, 0f);
        Vector3 to = target.Position + new Vector3(0f, (target.View?.Height ?? 100f) * 0.5f, 0f);
        node.Position = from;
        node.Name = $"Missile_{attacker.Type.Id}";
        AddChild(node);

        var projectile = new Projectile
        {
            Node = node,
            From = from,
            To = to,
            Arc = Mathf.Max(0f, attacker.Type.MissileArc),
            Speed = Mathf.Max(200f, attacker.Type.MissileSpeed),
            Length = Mathf.Max(1f, from.DistanceTo(to)),
            Target = target,
            TargetHeight = (target.View?.Height ?? 100f) * 0.5f,
        };
        _projectiles.Add(projectile);
        if (_missileLogBudget > 0)
        {
            _missileLogBudget--;
            GD.Print($"[MISSILE] {attacker.Type.Id} → {target.Type.Id} 距离 {projectile.Length:0} 速度 {projectile.Speed:0} 弧 {projectile.Arc:0.00}");
        }
    }

    private static int _missileLogBudget = 6;

    private void TickProjectiles(float delta)
    {
        for (int index = _projectiles.Count - 1; index >= 0; index--)
        {
            Projectile projectile = _projectiles[index];
            if (projectile.Node == null || !IsInstanceValid(projectile.Node))
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            if (projectile.Target != null && projectile.Target.Alive)
            {
                projectile.To = projectile.Target.Position + new Vector3(0f, projectile.TargetHeight, 0f);
            }

            projectile.Travel += projectile.Speed * delta / projectile.Length;
            if (projectile.Travel >= 1f)
            {
                projectile.Node.QueueFree();
                _projectiles.RemoveAt(index);
                continue;
            }

            Vector3 position = projectile.From.Lerp(projectile.To, projectile.Travel);
            position.Y += projectile.Arc * projectile.Length * 4f * projectile.Travel * (1f - projectile.Travel);
            Vector3 direction = projectile.To - projectile.From;
            if (direction.LengthSquared() > 1f)
            {
                projectile.Node.Rotation = new Vector3(0f, Mathf.Atan2(direction.X, direction.Z), 0f);
            }
            projectile.Node.Position = position;
        }
    }

    private void RefreshConstruction()
    {
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || !unit.IsUnderConstruction) continue;
            float progress = unit.BuildTotal <= 0f ? 1f : 1f - unit.BuildRemaining / unit.BuildTotal;
            unit.View?.SetBuildProgress(progress);
        }
    }

    private void RefreshBars()
    {
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive)
            {
                unit.View?.SetBarVisible(false);
                continue;
            }
            bool damaged = unit.Hp < unit.MaxHp - 0.01f;
            unit.View?.SetBarVisible(damaged || _selected.Contains(unit));
        }
    }

    private string DescribeUnit(UnitState unit)
    {
        if (unit.IsUnderConstruction) return $"剩余 {Mathf.Max(0f, unit.BuildRemaining):0.0}s";

        float bonus = _sim != null ? _sim.DamageBonus(unit) : 0f;
        float armor = _sim != null ? _sim.ArmorOf(unit) : unit.Type.Armor;
        if (unit.Type.CanAttack)
        {
            return $"攻击 {unit.Type.MinDamage + bonus:0}-{unit.Type.MaxDamage + bonus:0}    护甲 {armor:0}"
                + $"    射程 {unit.Type.AttackRange:0}    速度 {unit.Type.Speed:0}";
        }
        return $"护甲 {armor:0}    速度 {unit.Type.Speed:0}    供给 {unit.Type.FoodMade}";
    }

    public void ScriptedMoveTest()
    {
        int moved = 0;
        var target = new Vector3();
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding || !unit.Type.CanAttack) continue;
            if (moved == 0)
            {
                target = unit.Position + new Vector3(500f, 0f, 500f);
                target.Y = MapWorldBuilder.HeightAt(_map.Terrain, target.X, target.Z);
            }
            _selected.Add(unit);
            unit.View?.SetSelected(true);
            unit.MoveTarget = target + new Vector3(60f * moved, 0f, 0f);
            moved++;
            if (moved >= 4) break;
        }
        _hud?.SetSelection(_selected);
        _hud?.SetHint($"脚本测试：{moved} 个单位向 {target.X:0},{target.Z:0} 移动");
    }

    public void RunClickTest()
    {
        SceneTreeTimer selectTimer = GetTree().CreateTimer(2.0);
        selectTimer.Timeout += () =>
        {
            UnitState target = null;
            foreach (UnitState unit in _allUnits)
            {
                if (unit.Alive && unit.Player == ViewPlayer && !unit.IsBuilding) { target = unit; break; }
            }
            if (target?.View == null) return;

            Vector3 point = target.Position + new Vector3(0f, target.View.Height * 0.5f, 0f);
            Vector2 screen = _camera.Camera.UnprojectPosition(point);
            PushClick(screen, MouseButton.Left);
        };

        SceneTreeTimer orderTimer = GetTree().CreateTimer(3.5);
        orderTimer.Timeout += () =>
        {
            Vector3 point = _camera.Target + new Vector3(-500f, 0f, 400f);
            point.Y = MapWorldBuilder.HeightAt(_map.Terrain, point.X, point.Z);
            Vector2 screen = _camera.Camera.UnprojectPosition(point);
            PushClick(screen, MouseButton.Right);
        };
    }

    private void PushClick(Vector2 position, MouseButton button)
    {
        var press = new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = true,
            Position = position,
            GlobalPosition = position,
        };
        var release = new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = false,
            Position = position,
            GlobalPosition = position,
        };
        GetViewport().PushInput(press);
        GetViewport().PushInput(release);
    }

    public override void _PhysicsProcess(double delta)
    {
        _sim?.Advance();
        TickOrders((float)delta);
        if (Aggression) TickAiWaves((float)delta);
        if (AiEnabled)
        {
            foreach (PlayerSlot slot in Slots()) TickAi(slot, (float)delta);
        }
    }

    private float _waveTimer;

    private static readonly Dictionary<int, string> RaceMelee = new()
    {
        [1] = "hfoo",
        [2] = "ogru",
        [3] = "ugho",
        [4] = "esen",
    };

    private void TickAiWaves(float delta)
    {
        _waveTimer += delta;
        float interval = 90f * (TimeScaleHint < 1f ? TimeScaleHint : 1f);
        if (_waveTimer < interval) return;
        _waveTimer = 0f;
        SpawnWave();
    }

    private void SpawnWave()
    {
        foreach (W3iPlayer player in _map.Players)
        {
            if (player.Id == ViewPlayer) continue;

            UnitState hall = FindHall(player.Id);
            if (hall == null) continue;

            int race = Mathf.Clamp(player.Race, 1, 4);
            string unitId = RaceMelee[race];
            float angle = Mathf.Pi;
            float worldX = hall.Position.X + Mathf.Cos(angle) * 220f;
            float worldZ = hall.Position.Z + Mathf.Sin(angle) * 220f;
            UnitState attacker = Spawn(unitId, player.Id, worldX, worldZ, Mathf.Pi + angle, 0f, 1f);
            if (attacker == null) continue;

            _sim.Units.Add(attacker);
            attacker.Target = NearestTargetOf(ViewPlayer, hall.Position);
        }
    }

    private UnitState NearestTargetOf(int player, Vector3 from)
    {
        UnitState best = null;
        float bestDistance = float.MaxValue;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player) continue;
            float distance = new Vector2(unit.Position.X - from.X, unit.Position.Z - from.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    private void HandleKeyboardPan(float delta)
    {
        if (_camera == null) return;

        float forwardInput = 0f;
        float rightInput = 0f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) forwardInput += 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) forwardInput -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) rightInput += 1f;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) rightInput -= 1f;
        if (forwardInput == 0f && rightInput == 0f) return;

        Vector3 forward = -_camera.Camera.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.Normalized();
        Vector3 right = _camera.Camera.GlobalTransform.Basis.X;
        right.Y = 0f;
        right = right.Normalized();

        Vector3 move = forward * forwardInput + right * rightInput;
        float speed = 1200f * delta;
        _camera.Pan(new Vector2(move.X, move.Z) * speed);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape)
            {
                Back();
                return;
            }

            int group = (int)(key.Keycode - Key.Key0);
            if (group is >= 0 and <= 9)
            {
                HandleGroupKey(group, key.CtrlPressed);
                return;
            }
        }

        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed) _camera.Zoom(-1);
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed) _camera.Zoom(1);
            else if (button.ButtonIndex == MouseButton.Middle)
            {
                _middleDown = button.Pressed;
                Input.MouseMode = button.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
                if (!button.Pressed) Input.WarpMouse(GetViewport().GetVisibleRect().Size / 2f);
            }
            else if (button.ButtonIndex == MouseButton.Left)
            {
                _leftDown = button.Pressed;
                if (button.Pressed)
                {
                    _dragStart = button.Position;
                    _hud?.SetSelectionBox(new Rect2(), false);
                }
                else if (_placingId != null)
                {
                    if (button.Position.DistanceTo(_dragStart) < 8f) TryPlace(button.Position);
                }
                else if (button.Position.DistanceTo(_dragStart) < 8f)
                {
                    _hud?.SetSelectionBox(new Rect2(), false);
                    HandleClick(button.Position, button.ShiftPressed);
                }
                else
                {
                    _hud?.SetSelectionBox(new Rect2(), false);
                    BoxSelect(_dragStart, button.Position, button.ShiftPressed);
                }
            }
            else if (button.ButtonIndex == MouseButton.Right && button.Pressed)
            {
                if (_placingId != null) CancelPlacement();
                else OrderAt(button.Position);
            }
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            if (_middleDown)
            {
                _camera.Pan(new Vector2(-motion.Relative.X, -motion.Relative.Y) * 1.6f);
                return;
            }

            if (_leftDown && _placingId == null && motion.Position.DistanceTo(_dragStart) >= 8f)
            {
                _hud?.SetSelectionBox(BoxRect(_dragStart, motion.Position), true);
            }
        }
    }

    private readonly Dictionary<int, List<UnitState>> _groups = new();
    private float _groupPressedAt = -1f;
    private int _groupPressed = -1;
    private float _clickTime = -1f;
    private UnitState _clickUnit;

    private static Rect2 BoxRect(Vector2 a, Vector2 b)
    {
        float x = Mathf.Min(a.X, b.X);
        float y = Mathf.Min(a.Y, b.Y);
        return new Rect2(x, y, Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }

    private void HandleClick(Vector2 position, bool additive)
    {
        float now = Time.GetTicksMsec() / 1000f;
        UnitState picked = PickUnit(position, ViewPlayer);

        if (picked != null && _clickUnit == picked && now - _clickTime < 0.4f)
        {
            SelectSameType(picked);
            _clickUnit = null;
            _clickTime = -1f;
            return;
        }

        SelectAt(position, additive);
        _clickUnit = picked;
        _clickTime = now;
    }

    /// <summary>双击：把屏幕上所有同类型单位一起选上（客户端口径）。</summary>
    private void SelectSameType(UnitState reference)
    {
        Camera3D camera = _camera.Camera;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;

        ClearSelection();
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer) continue;
            if (!string.Equals(unit.Type.Id, reference.Type.Id, StringComparison.OrdinalIgnoreCase)) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;
            Vector2 screen = camera.UnprojectPosition(point);
            if (screen.X < 0f || screen.Y < 0f || screen.X > viewport.X || screen.Y > viewport.Y) continue;

            _selected.Add(unit);
            unit.View?.SetSelected(true);
        }

        if (_selected.Count == 0) return;
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        _hud?.SetHint($"已选中 {_selected.Count} 个 {reference.Type.Name}");
    }

    private void BoxSelect(Vector2 from, Vector2 to, bool additive)
    {
        Rect2 rect = BoxRect(from, to);
        if (rect.Size.X < 4f || rect.Size.Y < 4f) return;

        Camera3D camera = _camera.Camera;
        if (!additive) ClearSelection();

        int added = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;

            Vector2 screen = camera.UnprojectPosition(point);
            if (!rect.HasPoint(screen)) continue;
            if (_selected.Contains(unit)) continue;

            _selected.Add(unit);
            unit.View?.SetSelected(true);
            added++;
        }

        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        if (added > 0) _hud?.SetHint($"框选 {_selected.Count} 个单位");
    }

    private void HandleGroupKey(int group, bool assign)
    {
        if (assign)
        {
            _groups[group] = new List<UnitState>(_selected);
            _hud?.SetHint($"编队 {group} 已保存（{_selected.Count} 个单位）");
            return;
        }

        if (!_groups.TryGetValue(group, out List<UnitState> members)) return;
        members.RemoveAll(unit => unit == null || !unit.Alive);
        if (members.Count == 0) return;

        ClearSelection();
        Vector3 sum = Vector3.Zero;
        foreach (UnitState unit in members)
        {
            _selected.Add(unit);
            unit.View?.SetSelected(true);
            sum += unit.Position;
        }
        _hud?.SetSelection(_selected);
        UpdateCommandCard();

        float now = Time.GetTicksMsec() / 1000f;
        bool center = _groupPressed == group && now - _groupPressedAt < 0.5f;
        _groupPressed = group;
        _groupPressedAt = now;

        if (center)
        {
            _camera.Focus(sum / members.Count);
        }
    }

    private void SelectAt(Vector2 screenPosition, bool additive)
    {
        UnitState picked = PickUnit(screenPosition, ViewPlayer);
        if (picked == null)
        {
            if (!additive) ClearSelection();
            return;
        }

        if (!additive) ClearSelection();
        if (_selected.Contains(picked))
        {
            picked.View?.SetSelected(false);
            _selected.Remove(picked);
        }
        else
        {
            _selected.Add(picked);
            picked.View?.SetSelected(true);
        }
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
    }

    private void ClearSelection()
    {
        foreach (UnitState unit in _selected) unit.View?.SetSelected(false);
        _selected.Clear();
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
    }

    private UnitState PickUnit(Vector2 screenPosition, int player)
    {
        Camera3D camera = _camera.Camera;
        UnitState best = null;
        float bestDistance = 55f;

        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;

            Vector2 screen = camera.UnprojectPosition(point);
            float distance = screen.DistanceTo(screenPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    private void OrderAt(Vector2 screenPosition)
    {
        if (_selected.Count == 0) return;

        UnitState enemy = null;
        Camera3D camera = _camera.Camera;
        float bestDistance = 55f;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || AreAllied(unit.Player, ViewPlayer)) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;
            float distance = camera.UnprojectPosition(point).DistanceTo(screenPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                enemy = unit;
            }
        }

        if (enemy != null)
        {
            if (enemy.Gold > 0)
            {
                int gatherers = 0;
                foreach (UnitState unit in _selected)
                {
                    if (unit.IsWorker)
                    {
                        _sim.SetGather(unit, enemy);
                        gatherers++;
                    }
                }
                _hud?.SetHint(gatherers > 0
                    ? $"采金：{gatherers} 个农民 → {enemy.Type.Name}（剩余 {enemy.Gold}）"
                    : "金矿只能由农民采集");
                return;
            }

            foreach (UnitState unit in _selected)
            {
                if (!unit.Type.CanAttack) continue;
                unit.GoldMine = null;
                unit.Tree = null;
                unit.Target = enemy;
                unit.MoveTarget = null;
                unit.Path = null;
            }
            _hud?.SetHint($"攻击 {enemy.Type.Name}");
            return;
        }

        TreeState tree = PickTree(screenPosition);
        if (tree != null)
        {
            int choppers = 0;
            foreach (UnitState unit in _selected)
            {
                if (!unit.IsWorker) continue;
                _sim.SetChop(unit, tree);
                choppers++;
            }
            if (choppers > 0)
            {
                _hud?.SetHint($"伐木：{choppers} 个农民 → 树（{tree.Hp:0} HP）");
                return;
            }
        }

        if (!TryGroundPoint(screenPosition, out Vector3 ground)) return;

        int index = 0;
        foreach (UnitState unit in _selected)
        {
            if (unit.IsBuilding) continue;

            float ring = 60f * (index / 6);
            float angle = Mathf.Tau * (index % 6) / 6f;
            var offset = new Vector3(Mathf.Cos(angle) * ring, 0f, Mathf.Sin(angle) * ring);
            unit.Target = null;
            unit.GoldMine = null;
            unit.Tree = null;
            unit.MoveTarget = ground + offset;
            unit.Path = null;
            index++;
        }
        _hud?.SetHint($"移动到 ({ground.X:0}, {ground.Z:0})");
    }

    private TreeState PickTree(Vector2 screenPosition)
    {
        Camera3D camera = _camera.Camera;
        TreeState best = null;
        float bestDistance = 45f;
        foreach (TreeState tree in _trees)
        {
            if (!tree.Alive) continue;
            Vector3 point = tree.Position + new Vector3(0f, 90f, 0f);
            if (camera.IsPositionBehind(point)) continue;
            float distance = camera.UnprojectPosition(point).DistanceTo(screenPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tree;
            }
        }
        return best;
    }

    private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.Zero;
        Camera3D camera = _camera.Camera;
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = camera.ProjectRayNormal(screenPosition);

        Vector3 previous = origin;
        for (float travel = 50f; travel < 20000f; travel += 50f)
        {
            Vector3 sample = origin + direction * travel;
            if (sample.Y <= MapWorldBuilder.HeightAt(_map.Terrain, sample.X, sample.Z))
            {
                Vector3 low = previous;
                Vector3 high = sample;
                for (int i = 0; i < 10; i++)
                {
                    Vector3 mid = (low + high) * 0.5f;
                    if (mid.Y <= MapWorldBuilder.HeightAt(_map.Terrain, mid.X, mid.Z)) high = mid;
                    else low = mid;
                }
                point = high;
                return true;
            }
            previous = sample;
        }
        return false;
    }

    public void ScriptedGatherTest()
    {
        UnitState worker = FindWorker(ViewPlayer);
        if (worker == null)
        {
            GD.Print("[TEST] 没有农民");
            return;
        }

        UnitState mine = null;
        float best = float.MaxValue;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Gold <= 0) continue;
            float distance = worker.DistanceTo(unit);
            if (distance < best)
            {
                best = distance;
                mine = unit;
            }
        }
        if (mine == null)
        {
            GD.Print("[TEST] 没有金矿");
            return;
        }

        ClearSelection();
        _selected.Add(worker);
        worker.View?.SetSelected(true);
        _sim.SetGather(worker, mine);
        _hud?.SetSelection(_selected);
        _hud?.SetHint($"采集测试：{worker.Type.Name} → 金矿（{mine.Gold} 金，距离 {best:0}）");
        GD.Print($"[TEST] 采集 {worker.Type.Name} → 金矿 距离 {best:0} 储量 {mine.Gold}");
    }

    public void ScriptedTrainTest()
    {
        UnitState hall = FindHall(ViewPlayer);
        if (hall == null || hall.Type.Trains.Length == 0)
        {
            GD.Print("[TEST] 没有可训练的主基地");
            return;
        }

        ClearSelection();
        _selected.Add(hall);
        hall.View?.SetSelected(true);
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        StartTraining(hall, hall.Type.Trains[0]);
        StartTraining(hall, hall.Type.Trains[0]);
    }

    public void ScriptedMoveTo(float mapX, float mapY)
    {
        float worldX = -(mapX - _map.Terrain.OffsetX);
        float worldZ = mapY - _map.Terrain.OffsetY;
        var goal = new Vector3(worldX, MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ), worldZ);

        int moved = 0;
        ClearSelection();
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding) continue;
            unit.GoldMine = null;
            unit.Target = null;
            unit.Path = null;
            unit.MoveTarget = goal;
            _selected.Add(unit);
            unit.View?.SetSelected(true);
            moved++;
            if (moved >= 6) break;
        }
        _hud?.SetSelection(_selected);
        GD.Print($"[TEST] 移动测试单位 {moved} 个");
    }

    public void PathProbe()
    {
        if (_pathGrid == null || !_pathGrid.Ready)
        {
            GD.Print("[PROBE] 寻路网格为空");
            return;
        }

        UnitState worker = FindWorker(ViewPlayer);
        if (worker == null) return;

        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.Tau * i / 8f;
            Vector3 goal = worker.Position + new Vector3(Mathf.Cos(angle) * 600f, 0f, Mathf.Sin(angle) * 600f);
            List<Vector3> path = _pathGrid.FindPath(worker.Position, goal, 28f);
            GD.Print($"[PROBE] 方向{i} 终点{goal.X:0},{goal.Z:0} 阻挡={_pathGrid.IsBlockedWorld(goal.X, goal.Z)} 航点={path?.Count ?? -1}");
        }
    }

    public void ScriptedLumberTest()
    {
        UnitState worker = FindWorker(ViewPlayer);
        if (worker == null || _trees.Count == 0)
        {
            GD.Print("[TEST] 没有农民或树");
            return;
        }

        TreeState tree = null;
        float best = float.MaxValue;
        foreach (TreeState candidate in _trees)
        {
            if (!candidate.Alive) continue;
            float distance = new Vector2(worker.Position.X - candidate.Position.X, worker.Position.Z - candidate.Position.Z).Length();
            if (distance < best) { best = distance; tree = candidate; }
        }
        if (tree == null) return;

        ClearSelection();
        _selected.Add(worker);
        worker.View?.SetSelected(true);
        _sim.SetChop(worker, tree);
        _hud?.SetSelection(_selected);
        _hud?.SetHint($"伐木测试：{worker.Type.Name} → 树 HP {tree.Hp:0} 距离 {best:0}");
    }

    public void ScriptedBuildTest()
    {
        UnitState worker = FindWorker(ViewPlayer);
        UnitState hall = FindHall(ViewPlayer);
        if (worker == null || hall == null)
        {
            GD.Print("[TEST] 缺少农民或基地");
            return;
        }

        ClearSelection();
        _selected.Add(worker);
        worker.View?.SetSelected(true);
        _hud?.SetSelection(_selected);

        string buildingId = null;
        foreach (string candidate in worker.Type.Builds)
        {
            UnitType type = UnitCatalog.Get(candidate);
            if (type == null) continue;
            if (candidate == hall.Type.Id) continue;
            if (type.GoldCost > 0 || type.LumberCost > 0) { buildingId = candidate; break; }
        }
        if (buildingId == null)
        {
            GD.Print("[TEST] 农民没有可建造项目");
            return;
        }

        UnitType buildType = UnitCatalog.Get(buildingId);
        if (!TryFindBuildSpot(hall.Position, Mathf.Max(32f, buildType?.FootprintRadius ?? 96f), out Vector3 ground))
        {
            GD.Print("[TEST] 找不到可建造位置");
            return;
        }

        UnitState site = PlaceBuildingAt(Slot(ViewPlayer), buildingId, ground, true);
        if (site == null) return;

        GD.Print($"[TEST] {site.Type.Name} 开工 位置 {ground} 工期 {site.BuildTotal:0}s");
        SceneTreeTimer timer = GetTree().CreateTimer(site.BuildTotal + 0.5f);
        timer.Timeout += () =>
        {
            if (!site.Alive || site.IsUnderConstruction) return;
            ClearSelection();
            _selected.Add(site);
            site.View?.SetSelected(true);
            _hud?.SetSelection(_selected);
            UpdateCommandCard();
            if (site.Type.Trains.Length > 0) StartTraining(site, site.Type.Trains[0]);
        };
    }

    public void RunCreepProbe()
    {
        UnitState creep = null;
        float bestDistance = float.MaxValue;
        UnitState hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 12) continue;
            float distance = new Vector2(unit.Position.X - origin.X, unit.Position.Z - origin.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                creep = unit;
            }
        }
        if (creep == null)
        {
            GD.Print("[PROBE] 没有野怪");
            return;
        }

        var baitPosition = new Vector3(creep.Position.X + 350f, creep.Position.Y, creep.Position.Z);
        UnitState bait = Spawn("hrif", ViewPlayer, baitPosition.X, baitPosition.Z, 0f, 0f, 1f);
        if (bait == null) bait = Spawn("hfoo", ViewPlayer, baitPosition.X, baitPosition.Z, 0f, 0f, 1f);
        if (bait == null)
        {
            GD.Print("[PROBE] 假人模型缺失");
            return;
        }
        _sim.Units.Add(bait);

        GD.Print($"[PROBE] 目标野怪 {creep.Type.Id} 距基地 {bestDistance:0}，假人放在 350 外");
        float elapsed = 0f;
        const float step = 1f;
        const float pullAt = 6f;

        void TickProbe()
        {
            elapsed += step;
            float toGuard = new Vector2(creep.Position.X - creep.Home.X, creep.Position.Z - creep.Home.Z).Length();
            string state = creep.Returning ? "回营" : creep.ReturnDelay > 0f ? $"等待{creep.ReturnDelay:0.0}s" : creep.Target != null ? "追击" : "待机";
            GD.Print($"[PROBE] t={elapsed:0}s 状态={state} 距营地={toGuard:0} 距假人={creep.DistanceTo(bait):0} HP={creep.Hp:0}"
                + $" 假人 目标={bait.Target?.Type.Id ?? "无"} 目标HP={bait.Target?.Hp:0} 射程={bait.Type.AttackRange:0} 弹道={bait.Type.MissileScene ?? "无"}");

            if (Mathf.Abs(elapsed - pullAt) < 0.01f)
            {
                bait.Position = new Vector3(creep.Home.X + 1400f, bait.Position.Y, creep.Home.Z);
                bait.View.Root.Position = bait.Position;
                bait.Path = null;
                GD.Print("[PROBE] 把假人拉出 1400，观察放弃追击与回营");
            }

            if (elapsed >= 24f)
            {
                bait.Alive = false;
                bait.View.Root.QueueFree();
                _allUnits.Remove(bait);
                _sim.Units.Remove(bait);
                GD.Print("[PROBE] 结束");
                return;
            }
            SceneTreeTimer timer = GetTree().CreateTimer(step);
            timer.Timeout += TickProbe;
        }

        SceneTreeTimer first = GetTree().CreateTimer(step);
        first.Timeout += TickProbe;
    }

    private UnitState FindWorker(int player)
    {
        foreach (UnitState unit in _allUnits)
        {
            if (unit.Alive && unit.Player == player && unit.IsWorker) return unit;
        }
        return null;
    }

    private void OnUnitDied(UnitState unit)
    {
        if (_selected.Remove(unit))
        {
            _hud?.SetSelection(_selected);
            UpdateCommandCard();
        }
        RefreshHud();
    }

    private bool HasBuilding(PlayerSlot slot, string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        if (slot.KnownBuildings.Contains(id)) return true;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || unit.IsUnderConstruction) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private bool RequirementsMet(PlayerSlot slot, string[] requires)
    {
        if (requires == null) return true;
        foreach (string id in requires)
        {
            if (!HasBuilding(slot, id)) return false;
        }
        return true;
    }

    private string MissingRequirement(PlayerSlot slot, string[] requires)
    {
        if (requires == null) return "";
        foreach (string id in requires)
        {
            if (HasBuilding(slot, id)) continue;
            UnitType type = UnitCatalog.Get(id);
            return type != null ? type.Name : id;
        }
        return "";
    }

    private int FoodUsed(int player)
    {
        int used = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || unit.IsBuilding) continue;
            used += Mathf.Max(1, unit.Type.FoodUsed);
        }
        return used;
    }

    private int FoodCap(int player)
    {
        int cap = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            cap += unit.Type.FoodMade;
        }
        return cap;
    }

    private int QueueLength(UnitState building)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (ReferenceEquals(order.Building, building)) count++;
        }
        return count;
    }

    private void RefreshHud()
    {
        PlayerSlot slot = Slot(ViewPlayer);
        _hud?.SetResources(slot.Gold, slot.Lumber, FoodUsed(slot.Id), FoodCap(slot.Id));
        _hud?.SetPlayerInfo(PlayerInfoText());
        UpdateQueueLabel();
    }

    /// <summary>
    /// MeleeInitVictoryDefeat 的最小版本：没有建筑即判负，只剩一家存活即结束。
    /// 完整客户端还有「建造/死亡/离开/同盟变化」触发器与 2 秒延迟检查，这里先对齐判定口径。
    /// </summary>
    private void TickVictoryCheck(float delta)
    {
        if (_gameOver) return;
        _victoryTimer += delta;
        if (_victoryTimer < 2f) return;
        _victoryTimer = 0f;

        var alive = new List<int>();
        foreach (int id in _active)
        {
            bool hasBuilding = false;
            foreach (UnitState unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != id || !unit.IsBuilding) continue;
                hasBuilding = true;
                break;
            }

            if (!hasBuilding)
            {
                if (_defeated.Add(id))
                {
                    GD.Print($"[MELEE] P{id} {PlayerName(id)} 没有任何建筑，判负");
                    _hud?.SetHint($"{PlayerName(id)} 已被击败");
                }
                continue;
            }

            _defeated.Remove(id);
            alive.Add(id);
        }

        if (alive.Count == 0 || _active.Count < 2) return;
        bool oneTeamLeft = true;
        for (int i = 1; i < alive.Count; i++)
        {
            if (!AreAllied(alive[0], alive[i])) { oneTeamLeft = false; break; }
        }
        if (!oneTeamLeft) return;

        _gameOver = true;
        var winners = new List<string>();
        foreach (int id in alive) winners.Add(PlayerName(id));
        GD.Print($"[MELEE] 对局结束，胜者：{string.Join("、", winners)}");
        _hud?.SetHint($"对局结束，胜者：{string.Join("、", winners)}");
    }

    private string PlayerInfoText()
    {
        int race = 0;
        foreach (W3iPlayer player in _map.Players)
        {
            if (player.Id == ViewPlayer) { race = player.Race; break; }
        }

        var allies = new List<string>();
        foreach (int id in _active)
        {
            if (id == ViewPlayer || !AreAllied(ViewPlayer, id)) continue;
            allies.Add(PlayerName(id));
        }

        string allyText = allies.Count > 0 ? $"盟友 {string.Join("、", allies)}" : "无盟友";
        return $"{PlayerName(ViewPlayer)}（{RaceName(race)}）  {allyText}";
    }

    private void OnLumberDeposited(int player, int amount)
    {
        if (Slot(player) == null) return;
        Slot(player).Lumber += amount;
        RefreshHud();
    }

    private void OnGoldDeposited(int player, int amount)
    {
        if (Slot(player) == null) return;
        Slot(player).Gold += amount;
        RefreshHud();
    }

    private void UpdateCommandCard()
    {
        if (_hud == null) return;

        PlayerSlot slot = Slot(ViewPlayer);
        var commands = new List<CommandEntry>();
        if (_selected.Count > 0 && _selected[0].Player == slot.Id)
        {
            UnitState single = _selected.Count == 1 ? _selected[0] : null;
            if (single != null && single.IsBuilding && !single.IsUnderConstruction)
            {
                AddTrainingCommands(commands, slot, single);
                AddResearchCommands(commands, slot, single);
                AddUpgradeCommands(commands, slot, single);
            }
            else if (_selected[0].IsWorker)
            {
                AddBuildCommands(commands, slot, _selected[0]);
            }
        }

        _hud.SetCommands(commands);
    }

    private void AddTrainingCommands(List<CommandEntry> commands, PlayerSlot slot, UnitState building)
    {
        foreach (string unitId in building.Type.Trains)
        {
            UnitType type = UnitCatalog.Get(unitId);
            if (type == null) continue;

            bool afford = slot.Gold >= type.GoldCost && slot.Lumber >= type.LumberCost;
            bool food = FoodUsed(slot.Id) + type.FoodUsed <= FoodCap(slot.Id);
            bool requires = RequirementsMet(slot, type.Requires);
            bool room = QueueLength(building) < 5;
            bool enabled = afford && food && requires && room;

            string cost = type.GoldCost > 0 ? $"{type.GoldCost}金" : "";
            if (type.LumberCost > 0) cost += (cost.Length > 0 ? "/" : "") + $"{type.LumberCost}木";
            if (type.FoodUsed > 0) cost += (cost.Length > 0 ? " " : "") + $"{type.FoodUsed}食";

            UnitState target = building;
            string id = unitId;
            commands.Add(new CommandEntry
            {
                Id = id,
                Name = $"训练 {type.Name}",
                Cost = cost,
                Tooltip = $"训练 {type.Name}  {type.GoldCost}金 {type.LumberCost}木  {type.TrainSeconds:0}s",
                Icon = TechTree.Icon(TechTree.UnitIconArt(id)),
                Enabled = enabled,
                Action = () => StartTraining(target, id),
            });
        }
    }

    private void AddResearchCommands(List<CommandEntry> commands, PlayerSlot slot, UnitState building)
    {
        foreach (string upgradeId in building.Type.Researches)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;

            int level = slot.ResearchLevel(upgradeId);
            if (level >= upgrade.MaxLevel) continue;

            int next = level + 1;
            int gold = upgrade.GoldFor(next);
            int lumber = upgrade.LumberFor(next);
            bool afford = slot.Gold >= gold && slot.Lumber >= lumber;
            bool busy = QueueLength(building) >= 3;
            bool requires = RequirementsMet(slot, upgrade.Requires);

            string name = upgrade.NameFor(next);
            UnitState target = building;
            string id = upgradeId;
            commands.Add(new CommandEntry
            {
                Id = id,
                Name = $"研究 {name}",
                Cost = $"{gold}金/{lumber}木",
                Tooltip = $"{name}（{next}/{upgrade.MaxLevel}）\n{upgrade.Description}",
                Icon = TechTree.Icon(upgrade.IconPath),
                Enabled = afford && requires && !busy,
                Action = () => StartResearch(target, id),
            });
        }
    }

    private void AddUpgradeCommands(List<CommandEntry> commands, PlayerSlot slot, UnitState building)
    {
        foreach (string targetId in building.Type.UpgradeTo)
        {
            UnitType target = UnitCatalog.Get(targetId);
            if (target == null) continue;

            bool afford = slot.Gold >= target.GoldCost && slot.Lumber >= target.LumberCost;
            bool requires = RequirementsMet(slot, target.Requires);
            string buildingId = targetId;
            UnitState source = building;
            commands.Add(new CommandEntry
            {
                Id = buildingId,
                Name = $"升级为 {target.Name}",
                Cost = $"{target.GoldCost}金/{target.LumberCost}木",
                Tooltip = $"升级为 {target.Name}  {target.GoldCost}金 {target.LumberCost}木  {target.BuildSeconds:0}s",
                Icon = TechTree.Icon(TechTree.UnitIconArt(buildingId)),
                Enabled = afford && requires,
                Action = () => StartBuildingUpgrade(source, buildingId),
            });
        }
    }

    private void AddBuildCommands(List<CommandEntry> commands, PlayerSlot slot, UnitState worker)
    {
        foreach (string buildingId in worker.Type.Builds)
        {
            UnitType type = UnitCatalog.Get(buildingId);
            if (type == null) continue;

            bool afford = slot.Gold >= type.GoldCost && slot.Lumber >= type.LumberCost;
            bool requires = RequirementsMet(slot, type.Requires);
            bool known = HasBuilding(slot, buildingId);
            string id = buildingId;
            commands.Add(new CommandEntry
            {
                Id = id,
                Name = $"建造 {type.Name}",
                Cost = $"{type.GoldCost}金/{type.LumberCost}木",
                Tooltip = $"建造 {type.Name}  {type.GoldCost}金 {type.LumberCost}木  {type.BuildSeconds:0}s"
                    + (type.FoodMade > 0 ? $"  供给 +{type.FoodMade}" : "")
                    + (known ? "  已有" : ""),
                Icon = TechTree.Icon(TechTree.UnitIconArt(id)),
                Enabled = afford && requires,
                Action = () => BeginPlacement(id),
            });
        }
    }

    private void BeginPlacement(string buildingId)
    {
        PlayerSlot slot = Slot(ViewPlayer);
        UnitType type = UnitCatalog.Get(buildingId);
        if (type == null) return;
        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            _hud?.SetHint($"资源不足：需要 {type.GoldCost} 金 / {type.LumberCost} 木");
            return;
        }

        _placingId = buildingId;
        _hud?.SetHint($"选择位置放置 {type.Name}（左键确认，右键取消）");
    }

    private void CancelPlacement()
    {
        if (_placingId == null) return;
        _placingId = null;
        _hud?.SetHint("已取消建造");
    }

    private void TryPlace(Vector2 screenPosition)
    {
        if (!TryGroundPoint(screenPosition, out Vector3 ground))
        {
            _hud?.SetHint("地面位置无效");
            return;
        }
        PlaceBuildingAt(Slot(ViewPlayer), _placingId, ground, true);
    }

    private UnitState PlaceBuildingAt(PlayerSlot slot, string buildingId, Vector3 ground, bool report)
    {
        UnitType type = UnitCatalog.Get(buildingId);
        if (type == null) { _placingId = null; return null; }

        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            if (report) _hud?.SetHint("资源不足");
            _placingId = null;
            return null;
        }
        float footprint = Mathf.Max(32f, type.FootprintRadius);

        UnitState consumedMine = null;
        if (IsMineConsumingType(type))
        {
            consumedMine = FindConsumableMine(ground, 1200f);
            if (consumedMine == null)
            {
                if (report) _hud?.SetHint($"{type.Name} 必须建在金矿上");
                return null;
            }
            ground = consumedMine.Position;
        }

        if (_pathGrid != null && !_pathGrid.IsAreaFree(ground, footprint))
        {
            if (report) _hud?.SetHint("这里放不下");
            return null;
        }

        foreach (UnitState other in _allUnits)
        {
            if (!other.Alive || !other.IsBuilding || other == consumedMine) continue;
            float distance = new Vector2(other.Position.X - ground.X, other.Position.Z - ground.Z).Length();
            if (distance < (footprint + other.Type.FootprintRadius) * 0.92f)
            {
                if (report) _hud?.SetHint("离已有建筑太近");
                return null;
            }
        }

        UnitState site = Spawn(buildingId, slot.Id, ground.X, ground.Z, 0f, 0f, 1f);
        if (site == null)
        {
            if (report) _hud?.SetHint("建筑模型缺失");
            _placingId = null;
            return null;
        }

        slot.Gold -= type.GoldCost;
        slot.Lumber -= type.LumberCost;

        site.IsUnderConstruction = true;
        site.BuildTotal = Mathf.Max(2f, type.BuildSeconds * TimeScaleHint);
        site.BuildRemaining = site.BuildTotal;
        site.Hp = 1f;
        site.MaxHp = type.Hp;
        site.View?.SetHp(0.01f, true);
        site.View?.Play("Birth");
        _sim.Units.Add(site);
        _pathGrid?.AddBlocker(ground, footprint);
        slot.BuiltCounts[buildingId] = slot.BuiltCount(buildingId) + 1;

        if (consumedMine != null)
        {
            site.Gold = consumedMine.Gold;
            consumedMine.Alive = false;
            consumedMine.View?.Root.QueueFree();
            _allUnits.Remove(consumedMine);
            _sim.Units.Remove(consumedMine);
            GD.Print($"[BUILD] P{slot.Id} {type.Name} 占据金矿，储量 {site.Gold}");
        }

        int wanted = 1;
        int selectedWorkers = 0;
        foreach (UnitState worker in _selected)
        {
            if (worker.IsWorker && worker.Player == slot.Id) selectedWorkers++;
        }
        if (selectedWorkers > 1) wanted = Mathf.Min(selectedWorkers, 3);
        AssignBuilders(site, slot, wanted);

        _placingId = null;
        GD.Print($"[BUILD] P{slot.Id} 开工 {type.Name} 世界({ground.X:0},{ground.Z:0}) 工期 {site.BuildTotal:0}s 资源 {slot.Gold}/{slot.Lumber}");
        RefreshHud();
        UpdateCommandCard();
        return site;
    }

    private void AssignBuilders(UnitState site, PlayerSlot slot, int wanted)
    {
        int assigned = 0;
        foreach (UnitState worker in _selected)
        {
            if (assigned >= wanted) break;
            if (!worker.IsWorker || worker.Player != slot.Id || !worker.Alive) continue;
            _sim.SetBuild(worker, site);
            assigned++;
        }

        if (assigned >= wanted) return;

        var candidates = new List<UnitState>();
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
            if (unit.BuildSite != null || _selected.Contains(unit)) continue;
            candidates.Add(unit);
        }
        candidates.Sort((a, b) => a.DistanceTo(site).CompareTo(b.DistanceTo(site)));

        foreach (UnitState worker in candidates)
        {
            if (assigned >= wanted) break;
            _sim.SetBuild(worker, site);
            assigned++;
        }

        if (assigned < wanted)
        {
            GD.Print($"[BUILD] {site.Type.Name} 只派到 {assigned}/{wanted} 个工人");
        }
    }

    private static bool IsMineConsumingType(UnitType type)
    {
        if (type == null) return false;
        return type.Id.Equals("ugol", StringComparison.OrdinalIgnoreCase)
            || type.Id.Equals("egol", StringComparison.OrdinalIgnoreCase);
    }

    private UnitState FindConsumableMine(Vector3 from, float maxDistance)
    {
        UnitState best = null;
        float bestDistance = maxDistance;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Gold <= 0) continue;
            if (unit.Type.Id.StartsWith("ugol", StringComparison.OrdinalIgnoreCase)) continue;
            float distance = new Vector2(unit.Position.X - from.X, unit.Position.Z - from.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    private bool StartTraining(UnitState building, string unitId)
    {
        if (building == null || !building.Alive) return false;
        if (Slot(building.Player) == null) return false;

        PlayerSlot slot = Slot(building.Player);
        UnitType type = UnitCatalog.Get(unitId);
        if (type == null) return false;

        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            _hud?.SetHint($"资源不足：需要 {type.GoldCost} 金 / {type.LumberCost} 木");
            return false;
        }
        if (!RequirementsMet(slot, type.Requires))
        {
            _hud?.SetHint($"缺少前置建筑：{MissingRequirement(slot, type.Requires)}");
            return false;
        }
        if (FoodUsed(slot.Id) + type.FoodUsed > FoodCap(slot.Id))
        {
            _hud?.SetHint("人口已满，先补供给建筑");
            return false;
        }

        slot.Gold -= type.GoldCost;
        slot.Lumber -= type.LumberCost;
        float seconds = Mathf.Max(1f, type.TrainSeconds * TimeScaleHint);
        _orders.Add(new BuildingOrder
        {
            Player = slot.Id,
            Building = building,
            Kind = OrderKind.Train,
            Id = unitId,
            Remaining = seconds,
            Total = seconds,
        });
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    private bool StartResearch(UnitState building, string upgradeId)
    {
        if (building == null || !building.Alive) return false;
        if (Slot(building.Player) == null) return false;

        PlayerSlot slot = Slot(building.Player);
        UpgradeType upgrade = TechTree.Upgrade(upgradeId);
        if (upgrade == null) return false;

        int next = slot.ResearchLevel(upgradeId) + CountResearchOrders(upgradeId) + 1;
        if (next > upgrade.MaxLevel) return false;

        int gold = upgrade.GoldFor(next);
        int lumber = upgrade.LumberFor(next);
        if (slot.Gold < gold || slot.Lumber < lumber) return false;
        if (!RequirementsMet(slot, upgrade.Requires)) return false;

        slot.Gold -= gold;
        slot.Lumber -= lumber;
        float seconds = Mathf.Max(1f, upgrade.SecondsFor(next) * TimeScaleHint);
        _orders.Add(new BuildingOrder
        {
            Player = slot.Id,
            Building = building,
            Kind = OrderKind.Research,
            Id = upgradeId,
            Level = next,
            Remaining = seconds,
            Total = seconds,
        });
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    private bool StartBuildingUpgrade(UnitState building, string targetId)
    {
        if (building == null || !building.Alive) return false;
        if (Slot(building.Player) == null) return false;

        PlayerSlot slot = Slot(building.Player);
        UnitType target = UnitCatalog.Get(targetId);
        if (target == null) return false;
        if (slot.Gold < target.GoldCost || slot.Lumber < target.LumberCost) return false;
        if (!RequirementsMet(slot, target.Requires)) return false;

        slot.Gold -= target.GoldCost;
        slot.Lumber -= target.LumberCost;
        float seconds = Mathf.Max(1f, target.BuildSeconds * TimeScaleHint);
        _orders.Add(new BuildingOrder
        {
            Player = slot.Id,
            Building = building,
            Kind = OrderKind.Upgrade,
            Id = targetId,
            Remaining = seconds,
            Total = seconds,
        });
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    private int CountResearchOrders(string upgradeId)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Kind == OrderKind.Research && string.Equals(order.Id, upgradeId, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private void TickOrders(float delta)
    {
        for (int index = 0; index < _orders.Count; index++)
        {
            BuildingOrder order = _orders[index];
            if (order.Building == null || !order.Building.Alive)
            {
                _orders.RemoveAt(index);
                index--;
                continue;
            }

            bool blocked = false;
            for (int earlier = 0; earlier < index; earlier++)
            {
                if (ReferenceEquals(_orders[earlier].Building, order.Building))
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked) continue;

            order.Remaining -= delta;
            if (order.Remaining > 0f) continue;

            _orders.RemoveAt(index);
            index--;
            CompleteOrder(order);
        }
        UpdateQueueLabel();
    }

    private void CompleteOrder(BuildingOrder order)
    {
        switch (order.Kind)
        {
            case OrderKind.Train:
                SpawnTrainedUnit(order);
                break;
            case OrderKind.Research:
                ApplyResearch(order);
                break;
            case OrderKind.Upgrade:
                ReplaceBuilding(order.Building, order.Id);
                break;
        }
    }

    private void ApplyResearch(BuildingOrder order)
    {
        UpgradeType upgrade = TechTree.Upgrade(order.Id);
        if (upgrade == null) return;
        if (Slot(order.Player) == null) return;

        PlayerSlot slot = Slot(order.Player);
        slot.Research[order.Id] = order.Level;
        _sim?.SetResearchLevel(slot.Id, order.Id, order.Level);
        GD.Print($"[TECH] P{slot.Id} 研究完成 {upgrade.Id} {upgrade.NameFor(order.Level)} {order.Level}/{upgrade.MaxLevel}");
        RefreshHud();
        UpdateCommandCard();
    }

    private void SpawnTrainedUnit(BuildingOrder order)
    {
        float angle = _allUnits.Count * 0.9f;
        float worldX = order.Building.Position.X + Mathf.Cos(angle) * 150f;
        float worldZ = order.Building.Position.Z + Mathf.Sin(angle) * 150f;
        UnitState unit = Spawn(order.Id, order.Player, worldX, worldZ, Mathf.Pi + angle, 0f, 1f);
        if (unit == null) return;

        _sim?.Units.Add(unit);
        RefreshHud();
    }

    private void ReplaceBuilding(UnitState building, string targetId)
    {
        Vector3 position = building.Position;
        float facing = building.Facing;
        int player = building.Player;
        bool selected = _selected.Contains(building);

        if (selected)
        {
            _selected.Remove(building);
            building.View?.SetSelected(false);
        }

        _sim?.Units.Remove(building);
        _allUnits.Remove(building);
        building.Alive = false;
        building.View?.Root.QueueFree();

        UnitState replacement = Spawn(targetId, player, position.X, position.Z, facing, 0f, 1f);
        if (replacement == null) return;

        _sim?.Units.Add(replacement);
        Slot(player)?.KnownBuildings.Add(targetId);

        if (selected)
        {
            _selected.Add(replacement);
            replacement.View?.SetSelected(true);
            _hud?.SetSelection(_selected);
        }
        GD.Print($"[BUILD] P{player} 升级完成 {replacement.Type.Name}");
        RefreshHud();
        UpdateCommandCard();
    }

    private void UpdateQueueLabel()
    {
        if (_hud == null) return;
        if (_orders.Count == 0)
        {
            _hud.SetQueue("");
            return;
        }

        BuildingOrder order = _orders[0];
        string label = order.Kind switch
        {
            OrderKind.Train => $"训练 {UnitCatalog.Get(order.Id)?.Name}",
            OrderKind.Research => $"研究 {TechTree.Upgrade(order.Id)?.NameFor(order.Level)}",
            _ => $"升级 {UnitCatalog.Get(order.Id)?.Name}",
        };
        _hud.SetQueue($"P{order.Player} {order.Building.Type.Name}：{label} {Mathf.Max(0f, order.Remaining):0.0}s（队列 {_orders.Count}）");
    }

    private void Back()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        QueueFree();
        _back?.Invoke();
    }

    private bool TryFindBuildSpot(Vector3 origin, float footprintRadius, out Vector3 spot)
    {
        spot = Vector3.Zero;
        float start = Mathf.Max(200f, footprintRadius * 1.6f);
        for (float radius = start; radius <= 3400f; radius += 120f)
        {
            int steps = radius < 700f ? 16 : 28;
            for (int step = 0; step < steps; step++)
            {
                float angle = Mathf.Tau * step / steps;
                var candidate = new Vector3(
                    origin.X + Mathf.Cos(angle) * radius,
                    0f,
                    origin.Z + Mathf.Sin(angle) * radius);
                candidate.Y = MapWorldBuilder.HeightAt(_map.Terrain, candidate.X, candidate.Z);
                if (!IsSpotUsable(candidate, footprintRadius)) continue;
                spot = candidate;
                return true;
            }
        }
        return false;
    }

    private bool IsSpotUsable(Vector3 candidate, float footprintRadius)
    {
        if (_pathGrid != null && !_pathGrid.IsAreaFree(candidate, footprintRadius)) return false;
        foreach (UnitState other in _allUnits)
        {
            if (!other.Alive || !other.IsBuilding) continue;
            float distance = new Vector2(other.Position.X - candidate.X, other.Position.Z - candidate.Z).Length();
            float spacing = (footprintRadius + other.Type.FootprintRadius) * 0.92f;
            if (distance < spacing) return false;
        }
        return true;
    }

    public void RunHousekeeping()
    {
        AiEnabled = true;
        foreach (PlayerSlot slot in Slots())
        {
            slot.Ai = true;
            BuildPlan(slot);
        }
        _hud?.SetHint("[内务] 四个 AI 同时开工");
    }

    public void RunHousekeeping(int player)
    {
        if (Slot(player) == null) return;
        BuildPlan(Slot(player));
    }

    private void BuildPlan(PlayerSlot slot)
    {
        slot.Tasks.Clear();
        slot.Done = false;
        slot.Announced = false;

        UnitState hall = FindHall(slot.Id);
        if (hall == null)
        {
            GD.Print($"[内务] P{slot.Id} 找不到主基地，跳过");
            return;
        }

        string workerId = hall.Type.Trains.Length > 0 ? hall.Type.Trains[0] : null;
        UnitType worker = UnitCatalog.Get(workerId);

        var hallFamily = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectUpgradeFamily(hall.Type.Id, hallFamily);

        var buildable = new List<string>();
        if (worker != null)
        {
            foreach (string id in worker.Builds)
            {
                if (hallFamily.Contains(id)) continue;
                buildable.Add(id);
            }
        }

        foreach (string id in buildable)
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null) continue;
            int total = Mathf.Max(1, type.UpgradeTo.Length);
            if (type.FoodMade > 0) total = Mathf.Max(total, 2);
            slot.Tasks.Add(new InhouseTask { Kind = "build", Id = id, Total = total, Label = type.Name });
        }

        foreach (string id in buildable)
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null) continue;
            foreach (string upgradeId in type.Researches)
            {
                UpgradeType upgrade = TechTree.Upgrade(upgradeId);
                if (upgrade == null) continue;
                slot.Tasks.Add(new InhouseTask { Kind = "research", Id = upgradeId, Total = upgrade.MaxLevel, Label = upgrade.Name });
            }
        }

        var trainSources = new List<string>(buildable) { hall.Type.Id };
        var trained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in trainSources)
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null) continue;
            foreach (string unitId in type.Trains)
            {
                if (!trained.Add(unitId)) continue;
                UnitType unit = UnitCatalog.Get(unitId);
                if (unit == null) continue;
                int total = unitId == workerId ? 6 : 1;
                slot.Tasks.Add(new InhouseTask { Kind = "train", Id = unitId, Total = total, Label = unit.Name });
            }
        }

        GD.Print($"[内务] P{slot.Id} 计划 {slot.Tasks.Count} 条（建筑 {buildable.Count} 种，工人 {worker?.Name}）");
    }

    private void CollectUpgradeFamily(string id, HashSet<string> family)
    {
        if (string.IsNullOrEmpty(id) || !family.Add(id)) return;
        UnitType type = UnitCatalog.Get(id);
        if (type == null) return;
        foreach (string target in type.UpgradeTo) CollectUpgradeFamily(target, family);
    }

    private void TickAi(PlayerSlot slot, float delta)
    {
        if (slot.Done || !slot.Ai) return;

        slot.Timer += delta;
        if (slot.Timer < 0.3f + slot.Id * 0.05f) return;
        slot.Timer = 0f;

        UnitState hall = FindHall(slot.Id);
        if (hall == null) return;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (PlayerOrders(slot.Id) >= 4) break;
            if (FoodCap(slot.Id) - FoodUsed(slot.Id) <= 4 && ActionFarm(slot, hall)) continue;
            if (ActionBuild(slot, hall)) continue;
            if (ActionUpgrade(slot, hall)) continue;
            if (ActionResearch(slot, hall)) continue;
            if (ActionTrain(slot, hall)) continue;
            if (ActionStaffSites(slot)) continue;
            if (ActionGather(slot)) continue;
            break;
        }

        slot.StatusTimer += 0.3f;
        if (slot.StatusTimer >= 12f)
        {
            slot.StatusTimer = 0f;
            LogStatus(slot);
        }

        if (PlayerOrders(slot.Id) > 0) return;

        foreach (InhouseTask task in slot.Tasks)
        {
            if (TaskDone(slot, task) < task.Total) return;
        }

        slot.Done = true;
        if (slot.Announced) return;
        slot.Announced = true;

        int buildings = 0;
        int units = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.IsBuilding) buildings++;
            else units++;
        }
        GD.Print($"[内务] P{slot.Id} 完成：建筑 {buildings} 座，单位 {units} 个，资源 {slot.Gold}/{slot.Lumber}，人口 {FoodUsed(slot.Id)}/{FoodCap(slot.Id)}");
    }

    private int PlayerOrders(int player)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player == player) count++;
        }
        return count;
    }

    private int CountBuildings(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountBuildingsUnderConstruction(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || !unit.IsUnderConstruction) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountUnits(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || unit.IsBuilding) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountTrainOrders(int player, string id)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player != player || order.Kind != OrderKind.Train) continue;
            if (string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountPlayerResearchOrders(int player, string id)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player != player || order.Kind != OrderKind.Research) continue;
            if (string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int TaskDone(PlayerSlot slot, InhouseTask task)
    {
        switch (task.Kind)
        {
            case "build":
                return Mathf.Max(CountBuildings(slot, task.Id), slot.BuiltCount(task.Id));
            case "research":
                return slot.ResearchLevel(task.Id) + CountPlayerResearchOrders(slot.Id, task.Id);
            case "train":
                return CountUnits(slot, task.Id) + CountTrainOrders(slot.Id, task.Id);
            default:
                return 0;
        }
    }

    private void LogStatus(PlayerSlot slot)
    {
        int buildDone = 0;
        int buildTotal = 0;
        int researchDone = 0;
        int researchTotal = 0;
        int trainDone = 0;
        int trainTotal = 0;
        foreach (InhouseTask task in slot.Tasks)
        {
            int done = TaskDone(slot, task);
            switch (task.Kind)
            {
                case "build":
                    buildDone += Mathf.Min(done, task.Total);
                    buildTotal += task.Total;
                    break;
                case "research":
                    researchDone += Mathf.Min(done, task.Total);
                    researchTotal += task.Total;
                    break;
                case "train":
                    trainDone += Mathf.Min(done, task.Total);
                    trainTotal += task.Total;
                    break;
            }
        }

        int golders = 0;
        int choppers = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.GoldMine != null) golders++;
            else if (unit.Tree != null) choppers++;
        }

        if (slot.Id == ViewPlayer)
        {
            foreach (UnitState worker in _allUnits)
            {
                if (!worker.Alive || worker.Player != slot.Id || worker.IsBuilding) continue;
                string state = worker.BuildSite != null ? "建造" : worker.GoldMine != null ? "采金" : worker.Tree != null ? "伐木"
                    : worker.Target != null ? "战斗" : worker.MoveTarget.HasValue ? "移动" : "空闲";
                GD.Print($"[内务]   工人 {worker.Type.Id} 状态={state} HP={worker.Hp:0} 位置({worker.Position.X:0},{worker.Position.Z:0})"
                    + $" 携带={worker.Carry}");
            }
        }

        GD.Print($"[内务] P{slot.Id} 进度 建筑 {buildDone}/{buildTotal}  科技 {researchDone}/{researchTotal}  兵种 {trainDone}/{trainTotal}"
            + $"  队列 {PlayerOrders(slot.Id)}  人口 {FoodUsed(slot.Id)}/{FoodCap(slot.Id)}  采金 {golders} 伐木 {choppers}  资源 {slot.Gold}/{slot.Lumber}");
    }

    private bool ActionStaffSites(PlayerSlot slot)
    {
        foreach (UnitState site in _allUnits)
        {
            if (!site.Alive || site.Player != slot.Id || !site.IsBuilding || !site.IsUnderConstruction) continue;
            if (SiteHasBuilder(site)) continue;

            UnitState best = null;
            float bestDistance = float.MaxValue;
            foreach (UnitState unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
                if (!IsIdle(unit)) continue;
                float distance = unit.DistanceTo(site);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = unit;
                }
            }
            if (best == null) continue;
            _sim.SetBuild(best, site);
            return true;
        }
        return false;
    }

    private bool ActionGather(PlayerSlot slot)
    {
        UnitState worker = null;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (!unit.Type.CanGatherGold && !unit.Type.CanGatherLumber) continue;
            if (!IsIdle(unit)) continue;
            worker = unit;
            break;
        }
        if (worker == null) return false;

        UnitState mine = null;
        float mineDistance = float.MaxValue;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Gold <= 0) continue;
            float distance = new Vector2(worker.Position.X - unit.Position.X, worker.Position.Z - unit.Position.Z).Length();
            if (distance < mineDistance)
            {
                mineDistance = distance;
                mine = unit;
            }
        }

        int goldWorkers = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.GoldMine != null) goldWorkers++;
        }

        if (worker.BuildSite != null) return false;

        if (worker.Type.CanGatherGold && mine != null && goldWorkers < 3)
        {
            _sim.SetGather(worker, mine);
            return true;
        }

        if (!worker.Type.CanGatherLumber) return false;
        TreeState tree = NearestTreeTo(worker.Position);
        if (tree == null) return false;
        _sim.SetChop(worker, tree);
        return true;
    }

    private static bool IsIdle(UnitState unit)
    {
        return unit.GoldMine == null && unit.Tree == null && unit.BuildSite == null
            && unit.Target == null && !unit.MoveTarget.HasValue;
    }

    private TreeState NearestTreeTo(Vector3 position)
    {
        TreeState best = null;
        float bestDistance = float.MaxValue;
        foreach (TreeState tree in _trees)
        {
            if (!tree.Alive) continue;
            float distance = new Vector2(position.X - tree.Position.X, position.Z - tree.Position.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tree;
            }
        }
        return best;
    }

    private bool ActionFarm(PlayerSlot slot, UnitState hall)
    {
        UnitState worker = FindWorker(slot.Id);
        if (worker == null) return false;

        foreach (string id in worker.Type.Builds)
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null || type.FoodMade <= 0) continue;
            if (type.Trains.Length > 0) continue;
            if (CountBuildings(slot, id) >= 12) continue;
            if (CountBuildingsUnderConstruction(slot, id) > 0) continue;
            if (!RequirementsMet(slot, type.Requires)) continue;
            return BuildOne(slot, id, hall);
        }
        return false;
    }

    private bool ActionBuild(PlayerSlot slot, UnitState hall)
    {
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "build") continue;
            if (TaskDone(slot, task) >= task.Total) continue;
            UnitType type = UnitCatalog.Get(task.Id);
            if (type == null || !RequirementsMet(slot, type.Requires)) continue;
            return BuildOne(slot, task.Id, hall);
        }
        return false;
    }

    private int CountActiveSites(PlayerSlot slot)
    {
        int count = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding) continue;
            if (unit.IsUnderConstruction) count++;
        }
        return count;
    }

    private bool SiteHasBuilder(UnitState site)
    {
        foreach (UnitState unit in _allUnits)
        {
            if (unit.Alive && ReferenceEquals(unit.BuildSite, site)) return true;
        }
        return false;
    }

    private bool BuildOne(PlayerSlot slot, string buildingId, UnitState hall)
    {
        UnitType type = UnitCatalog.Get(buildingId);
        if (type == null) return false;
        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost) return false;
        if (CountActiveSites(slot) >= 3) return false;

        // 不能把所有工人都派去盖房子，否则采集直接归零（亡灵 3 侍僧尤其明显）
        int freeWorkers = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
            if (unit.BuildSite == null) freeWorkers++;
        }
        if (freeWorkers == 0) return false;

        Vector3 spot;
        if (IsMineConsumingType(type))
        {
            UnitState mine = FindConsumableMine(hall.Position, 2400f);
            if (mine == null)
            {
                GD.Print($"[内务] P{slot.Id} 找不到可占据的金矿，跳过 {type.Name}");
                return false;
            }
            spot = mine.Position;
        }
        else if (!TryFindBuildSpot(hall.Position, Mathf.Max(32f, type.FootprintRadius), out spot))
        {
            GD.Print($"[内务] P{slot.Id} 找不到 {type.Name} 的位置");
            return false;
        }

        UnitState site = PlaceBuildingAt(slot, buildingId, spot, false);
        if (site == null) return false;
        return true;
    }

    private bool ActionUpgrade(PlayerSlot slot, UnitState hall)
    {
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            if (unit.Type.UpgradeTo.Length == 0) continue;

            string targetId = null;
            foreach (string candidate in unit.Type.UpgradeTo)
            {
                if (CountBuildings(slot, candidate) > 0) continue;
                UnitType target = UnitCatalog.Get(candidate);
                if (target == null || !RequirementsMet(slot, target.Requires)) continue;
                targetId = candidate;
                break;
            }
            if (targetId == null) continue;

            CancelTrainingOrders(unit);
            if (QueueLength(unit) > 0) continue;
            return StartBuildingUpgrade(unit, targetId);
        }
        return false;
    }

    private void CancelTrainingOrders(UnitState building)
    {
        for (int index = _orders.Count - 1; index >= 0; index--)
        {
            BuildingOrder order = _orders[index];
            if (!ReferenceEquals(order.Building, building) || order.Kind != OrderKind.Train) continue;

            UnitType type = UnitCatalog.Get(order.Id);
            if (type != null && Slot(order.Player) != null)
            {
                Slot(order.Player).Gold += type.GoldCost;
                Slot(order.Player).Lumber += type.LumberCost;
            }
            _orders.RemoveAt(index);
        }
    }

    private bool HasPendingUpgrade(UnitState building)
    {
        if (Slot(building.Player) == null) return false;
        PlayerSlot slot = Slot(building.Player);
        foreach (string candidate in building.Type.UpgradeTo)
        {
            if (CountBuildings(slot, candidate) > 0) continue;
            return true;
        }
        return false;
    }

    private bool ActionResearch(PlayerSlot slot, UnitState hall)
    {
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "research") continue;
            if (TaskDone(slot, task) >= task.Total) continue;

            UpgradeType upgrade = TechTree.Upgrade(task.Id);
            if (upgrade == null || !RequirementsMet(slot, upgrade.Requires)) continue;

            foreach (UnitState unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (Array.IndexOf(unit.Type.Researches, task.Id) < 0) continue;
                if (QueueLength(unit) >= 2) continue;
                return StartResearch(unit, task.Id);
            }
        }
        return false;
    }

    private bool ActionTrain(PlayerSlot slot, UnitState hall)
    {
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "train") continue;
            if (TaskDone(slot, task) >= task.Total) continue;

            UnitType type = UnitCatalog.Get(task.Id);
            if (type == null || !RequirementsMet(slot, type.Requires)) continue;
            if (FoodUsed(slot.Id) + type.FoodUsed > FoodCap(slot.Id)) continue;

            foreach (UnitState unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (Array.IndexOf(unit.Type.Trains, task.Id) < 0) continue;
                if (QueueLength(unit) >= 2) continue;
                if (HasPendingUpgrade(unit)) continue;
                return StartTraining(unit, task.Id);
            }
        }
        return false;
    }
}
