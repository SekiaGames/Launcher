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

    private readonly List<UnitState> _selected = new();
    private readonly List<UnitState> _own = new();
    private readonly List<UnitState> _allUnits = new();
    private bool _middleDown;
    private bool _leftDown;
    private Vector2 _dragStart;
    private float _hudTimer;
    private PathGrid _pathGrid;
    private List<TreeState> _trees = new();
    private string _placingId;

    public int StartGold = 1000000;
    public int StartLumber = 1000000;
    private int _gold;
    private int _lumber;

    private enum OrderKind
    {
        Train,
        Research,
        Upgrade,
    }

    private sealed class BuildingOrder
    {
        public UnitState Building;
        public OrderKind Kind;
        public string Id;
        public int Level;
        public float Remaining;
        public float Total;
    }

    private readonly List<BuildingOrder> _orders = new();
    private readonly Dictionary<string, int> _researchLevels = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownBuildings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _builtCounts = new(StringComparer.OrdinalIgnoreCase);

    public void Setup(MapSource map, Action back)
    {
        _map = map;
        _back = back;
        _gold = StartGold;
        _lumber = StartLumber;

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

        _camera = new War3CameraRig(this);
        SetCameraBounds();
        FocusStartPosition();

        _hud = new Hud(this, map.Name);
        _hud.DetailProvider = DescribeUnit;
        _hud.SetSelection(_selected);
        RefreshHud();
        UpdateCommandCard();
        LogNearestCreep();
    }

    private void LogNearestCreep()
    {
        UnitState hall = FindHall(0);
        if (hall == null) return;

        var nearest = new List<(float Distance, string Id)>();
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 12 || unit.Gold > 0) continue;
            float distance = new Vector2(unit.Position.X - hall.Position.X, unit.Position.Z - hall.Position.Z).Length();
            nearest.Add((distance, unit.Type.Id));
        }
        nearest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        for (int i = 0; i < Mathf.Min(3, nearest.Count); i++)
        {
            GD.Print($"[AI] 野怪 {nearest[i].Id} 距基地 {nearest[i].Distance:0}");
        }
    }

    private void SpawnUnits()
    {
        float offsetX = _map.Terrain.OffsetX;
        float offsetY = _map.Terrain.OffsetY;
        int placed = 0;
        int missingType = 0;
        int missingScene = 0;

        if (_map.Units != null)
        {
            foreach (UnitPlacement entry in _map.Units.Units)
            {
                if (entry.IsStartingLocation) continue;

                float worldX = -(entry.X - offsetX);
                float worldZ = entry.Y - offsetY;
                var player = (int)Mathf.Clamp(entry.Player, 0, 15);
                UnitState unit = Spawn(entry.Id, player, worldX, worldZ, Mathf.Pi + entry.Angle,
                    entry.Hitpoints > 0 ? entry.Hitpoints : 0f, entry.ScaleX, entry.Gold);

                if (unit != null) placed++;
                else if (UnitCatalog.Get(entry.Id) == null) missingType++;
                else missingScene++;

                if (player <= 3 && unit != null)
                {
                    GD.Print($"[MAP] 预置 P{player} {entry.Id} 位置 {unit.Position.X:0},{unit.Position.Z:0}");
                }
            }
        }

        int melee = SpawnMeleeStarts(offsetX, offsetY);

        GD.Print($"[GAME] 地图单位 {placed} 个（缺类型 {missingType} 缺模型 {missingScene}），开局单位 {melee} 个");
        if (placed + melee == 0)
        {
            _hud?.SetHint("没有生成任何单位：检查 extract.pck 是否挂载、Tables 是否齐全");
        }
    }

    private static readonly (string TownHall, string Worker, int Workers)[] MeleeArchetypes =
    {
        ("htow", "hpea", 5),
        ("ogre", "opeo", 5),
        ("unpl", "uaco", 3),
        ("etol", "ewsp", 5),
    };

    private int SpawnMeleeStarts(float offsetX, float offsetY)
    {
        int spawned = 0;
        foreach (W3iPlayer player in _map.Players)
        {
            if (player.Id is < 0 or > 3) continue;
            if (player.StartX == 0f && player.StartY == 0f) continue;

            float worldX = -(player.StartX - offsetX);
            float worldZ = player.StartY - offsetY;
            int race = Mathf.Clamp(player.Race, 1, 4);
            (string townHall, string worker, int workers) = MeleeArchetypes[race - 1];
            GD.Print($"[GAME] 开局 P{player.Id} 种族{race} 地图({player.StartX:0},{player.StartY:0}) 世界({worldX:0},{worldZ:0})");

            UnitState hall = Spawn(townHall, player.Id, worldX, worldZ, 0f, 0f, 1f);
            if (hall != null)
            {
                spawned++;
                if (player.Id == 0) MarkBuildingKnown(townHall);
            }

            for (int i = 0; i < workers; i++)
            {
                float angle = Mathf.Tau * i / workers;
                float workerX = worldX + Mathf.Cos(angle) * 110f;
                float workerZ = worldZ + Mathf.Sin(angle) * 110f;
                if (Spawn(worker, player.Id, workerX, workerZ, Mathf.Pi + angle, 0f, 1f) != null) spawned++;
            }
        }
        return spawned;
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

        UnitView view = UnitView.Create(scene, UnitView.ColorOf(player), scale, type.IsBuilding);
        view.Root.Position = new Vector3(worldX, worldY, worldZ);
        view.Root.Rotation = new Vector3(0f, yaw, 0f);
        view.Root.Name = $"{id}_{_allUnits.Count}";
        AddChild(view.Root);

        float maxHp = type.Hp;
        if (hp > 0f) maxHp = hp <= 100f ? type.Hp * hp / 100f : hp;
        bool hostile = player == 12 || (player >= 1 && player <= 3);
        var unit = new UnitState
        {
            Type = type,
            Player = player,
            Position = view.Root.Position,
            Facing = yaw,
            Hp = maxHp,
            MaxHp = maxHp,
            IsBuilding = type.IsBuilding,
            Gold = gold,
            Home = view.Root.Position,
            AutoAcquire = hostile,
            LeashRange = player >= 1 && player <= 3 ? 1e9f : 260f,
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
        if (player == 0) _own.Add(unit);
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
        float minWorldX = -(_map.Terrain.Width - 1) * MapWorldBuilder.CellSize;
        float maxWorldX = 0f;
        float minWorldZ = 0f;
        float maxWorldZ = (_map.Terrain.Height - 1) * MapWorldBuilder.CellSize;
        _camera.Bounds = new Vector4(minWorldX, minWorldZ, maxWorldX, maxWorldZ);
    }

    private void FocusStartPosition()
    {
        Vector3 focus = Vector3.Zero;
        W3iPlayer start = null;
        foreach (W3iPlayer player in _map.Players)
        {
            if (player.Id == 0 && (player.StartX != 0f || player.StartY != 0f))
            {
                start = player;
                break;
            }
        }

        if (start != null)
        {
            float worldX = -(start.StartX - _map.Terrain.OffsetX);
            float worldZ = start.StartY - _map.Terrain.OffsetY;
            focus = new Vector3(worldX, MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ), worldZ);
        }
        else if (_own.Count > 0)
        {
            focus = _own[0].Position;
        }

        GD.Print($"[GAME] 相机焦点 {focus}，起始单位 {_own.Count} 个");
        _camera.Focus(focus);
    }

    public override void _Process(double delta)
    {
        HandleKeyboardPan((float)delta);
        _world?.TickWater(delta);

        _hudTimer += (float)delta;
        if (_hudTimer >= 0.2f)
        {
            _hudTimer = 0f;
            _hud?.SetSelection(_selected);
            RefreshBars();
            RefreshHud();
            UpdateCommandCard();
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

    public void ScriptedMoveTest()
    {
        int moved = 0;
        var target = new Vector3();
        foreach (UnitState unit in _own)
        {
            if (unit.IsBuilding) continue;
            if (!unit.Type.CanAttack) continue;
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
        GD.Print($"[GAME] 脚本测试单位 {moved} 个，目标 {target}");
    }

    public void RunClickTest()
    {
        SceneTreeTimer selectTimer = GetTree().CreateTimer(2.0);
        selectTimer.Timeout += () =>
        {
            UnitState target = null;
            foreach (UnitState unit in _own)
            {
                if (!unit.IsBuilding) { target = unit; break; }
            }
            if (target?.View == null) return;

            Vector3 point = target.Position + new Vector3(0f, target.View.Height * 0.5f, 0f);
            Vector2 screen = _camera.Camera.UnprojectPosition(point);
            GD.Print($"[TEST] 点击 {target.Type.Name} 屏幕坐标 {screen}");
            PushClick(screen, MouseButton.Left);
        };

        SceneTreeTimer orderTimer = GetTree().CreateTimer(3.5);
        orderTimer.Timeout += () =>
        {
            Vector3 point = _camera.Target + new Vector3(-500f, 0f, 400f);
            point.Y = MapWorldBuilder.HeightAt(_map.Terrain, point.X, point.Z);
            Vector2 screen = _camera.Camera.UnprojectPosition(point);
            GD.Print($"[TEST] 右键移动 屏幕坐标 {screen} 选中 {_selected.Count} 个");
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
        TickAiWaves((float)delta);
        TickHousekeeping((float)delta);
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
            if (player.Id is < 1 or > 3) continue;

            UnitState hall = null;
            int alive = 0;
            foreach (UnitState unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != player.Id) continue;
                if (unit.IsBuilding && unit.Type.Trains.Length > 0 && hall == null) hall = unit;
                if (!unit.IsBuilding) alive++;
            }
            if (hall == null || alive >= 8) continue;

            int race = Mathf.Clamp(player.Race, 1, 4);
            string unitId = RaceMelee[race];
            for (int i = 0; i < 1; i++)
            {
                float angle = Mathf.Pi + i * 0.9f;
                float worldX = hall.Position.X + Mathf.Cos(angle) * 220f;
                float worldZ = hall.Position.Z + Mathf.Sin(angle) * 220f;
                UnitState attacker = Spawn(unitId, player.Id, worldX, worldZ, Mathf.Pi + angle, 0f, 1f);
                if (attacker == null) continue;

                _sim.Units.Add(attacker);
                attacker.Target = NearestPlayerTarget(hall.Position);
            }
            GD.Print($"[AI] P{player.Id} 出兵 1 个 {unitId} → {NearestPlayerTarget(hall.Position)?.Type.Id}");
        }
    }

    private UnitState NearestPlayerTarget(Vector3 from)
    {
        UnitState best = null;
        float bestDistance = float.MaxValue;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 0) continue;
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
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            Back();
            return;
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
                if (button.Pressed) _dragStart = button.Position;
                else if (button.Position.DistanceTo(_dragStart) < 8f)
                {
                    if (_placingId != null) TryPlace(button.Position);
                    else SelectAt(button.Position, button.ShiftPressed);
                }
            }
            else if (button.ButtonIndex == MouseButton.Right && button.Pressed)
            {
                if (_placingId != null) CancelPlacement();
                else OrderAt(button.Position);
            }
            return;
        }

        if (@event is InputEventMouseMotion motion && _middleDown)
        {
            _camera.Pan(new Vector2(-motion.Relative.X, -motion.Relative.Y) * 1.6f);
        }
    }

    private void SelectAt(Vector2 screenPosition, bool additive)
    {
        UnitState picked = PickUnit(screenPosition, ownOnly: true);
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

    private UnitState PickUnit(Vector2 screenPosition, bool ownOnly)
    {
        Camera3D camera = _camera.Camera;
        UnitState best = null;
        float bestDistance = 55f;

        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive) continue;
            if (ownOnly && unit.Player != 0) continue;

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
            if (!unit.Alive || unit.Player == 0) continue;

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
        UnitState worker = FindWorker(0);
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
        UnitState hall = FindHall(0);
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
        GD.Print($"[TEST] 训练队列 {_orders.Count} 个，金币 {_gold}");
    }

    public void ScriptedMoveTo(float mapX, float mapY)
    {
        float worldX = -(mapX - _map.Terrain.OffsetX);
        float worldZ = mapY - _map.Terrain.OffsetY;
        var goal = new Vector3(worldX, MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ), worldZ);
        GD.Print($"[TEST] 目标点 ({mapX},{mapY}) → 世界 {goal} 阻挡={_pathGrid?.IsBlockedWorld(worldX, worldZ)}");

        int moved = 0;
        ClearSelection();
        foreach (UnitState unit in _own)
        {
            if (unit.IsBuilding) continue;
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

        UnitState worker = FindWorker(0);
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
        UnitState worker = FindWorker(0);
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
        GD.Print($"[TEST] 伐木 {worker.Type.Name} → 树 距离 {best:0} HP {tree.Hp:0}，树总数 {_trees.Count}");
    }

    public void ScriptedBuildTest()
    {
        UnitState worker = FindWorker(0);
        UnitState hall = FindHall(0);
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
            if (type == null || type.IsBuilding == false) continue;
            if (candidate == hall.Type.Id) continue;
            buildingId = candidate;
            break;
        }
        if (buildingId == null)
        {
            GD.Print("[TEST] 农民没有可建造项目");
            return;
        }

        if (!TryFindBuildSpot(hall.Position, buildingId, out Vector3 ground))
        {
            GD.Print("[TEST] 找不到可建造位置");
            return;
        }

        UnitState site = PlaceBuildingAt(buildingId, ground, true);
        if (site == null) return;

        GD.Print($"[TEST] {site.Type.Name} 开工 位置 {ground} 工期 {site.BuildTotal:0}s");
        SceneTreeTimer timer = GetTree().CreateTimer(site.BuildTotal + 0.5f);
        timer.Timeout += () =>
        {
            if (!site.Alive || site.IsUnderConstruction)
            {
                GD.Print($"[TEST] 未完工 进行中={site.IsUnderConstruction} 剩余 {site.BuildRemaining:0.0}s");
                return;
            }
            GD.Print($"[TEST] {site.Type.Name} 完工，开始训练 {site.Type.Trains[0]}");
            ClearSelection();
            _selected.Add(site);
            site.View?.SetSelected(true);
            _hud?.SetSelection(_selected);
            UpdateCommandCard();
            StartTraining(site, site.Type.Trains[0]);
        };
    }

    private UnitState FindWorker(int player)
    {
        foreach (UnitState unit in _own)
        {
            if (unit.Alive && unit.Player == player && unit.IsWorker) return unit;
        }
        return null;
    }

    private UnitState FindHall(int player)
    {
        UnitState fallback = null;
        foreach (UnitState unit in _own)
        {
            if (!unit.Alive || unit.Player != player || !unit.IsBuilding) continue;
            if (unit.Type.FoodMade > 0 && unit.Type.Trains.Length > 0) return unit;
            if (fallback == null && unit.Type.Trains.Length > 0) fallback = unit;
        }
        return fallback;
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

    private void MarkBuildingKnown(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        _knownBuildings.Add(id);
    }

    private bool HasBuilding(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        if (_knownBuildings.Contains(id)) return true;
        foreach (UnitState unit in _own)
        {
            if (!unit.Alive || unit.IsUnderConstruction) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private bool RequirementsMet(string[] requires)
    {
        if (requires == null) return true;
        foreach (string id in requires)
        {
            if (!HasBuilding(id)) return false;
        }
        return true;
    }

    private int FoodUsed()
    {
        int used = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 0 || unit.IsBuilding) continue;
            used += Mathf.Max(1, unit.Type.FoodUsed);
        }
        return used;
    }

    private int FoodCap()
    {
        int cap = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 0 || !unit.IsBuilding || unit.IsUnderConstruction) continue;
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
        _hud?.SetResources(_gold, _lumber, FoodUsed(), FoodCap());
        UpdateQueueLabel();
    }

    private void OnLumberDeposited(int player, int amount)
    {
        if (player != 0) return;
        _lumber += amount;
        GD.Print($"[TEST] 入袋 {amount} 木 → 总计 {_lumber}");
        RefreshHud();
    }

    private void OnGoldDeposited(int player, int amount)
    {
        if (player != 0) return;
        _gold += amount;
        GD.Print($"[TEST] 入袋 {amount} 金 → 总计 {_gold}");
        RefreshHud();
    }

    private void UpdateCommandCard()
    {
        if (_hud == null) return;

        var commands = new List<CommandEntry>();
        if (_selected.Count > 0 && _selected[0].Player == 0)
        {
            UnitState single = _selected.Count == 1 ? _selected[0] : null;
            if (single != null && single.IsBuilding && !single.IsUnderConstruction)
            {
                AddTrainingCommands(commands, single);
                AddResearchCommands(commands, single);
                AddUpgradeCommands(commands, single);
            }
            else if (_selected[0].IsWorker)
            {
                AddBuildCommands(commands, _selected[0]);
            }
        }

        _hud.SetCommands(commands);
    }

    private void AddTrainingCommands(List<CommandEntry> commands, UnitState building)
    {
        foreach (string unitId in building.Type.Trains)
        {
            UnitType type = UnitCatalog.Get(unitId);
            if (type == null) continue;

            bool afford = _gold >= type.GoldCost && _lumber >= type.LumberCost;
            bool food = FoodUsed() + type.FoodUsed <= FoodCap();
            bool requires = RequirementsMet(type.Requires);
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

    private void AddResearchCommands(List<CommandEntry> commands, UnitState building)
    {
        foreach (string upgradeId in building.Type.Researches)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;

            int level = ResearchLevel(upgradeId);
            if (level >= upgrade.MaxLevel) continue;

            int next = level + 1;
            int gold = upgrade.GoldFor(next);
            int lumber = upgrade.LumberFor(next);
            bool afford = _gold >= gold && _lumber >= lumber;
            bool busy = QueueLength(building) >= 3;
            bool requires = RequirementsMet(upgrade.Requires);

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

    private void AddUpgradeCommands(List<CommandEntry> commands, UnitState building)
    {
        foreach (string targetId in building.Type.UpgradeTo)
        {
            UnitType target = UnitCatalog.Get(targetId);
            if (target == null) continue;

            bool afford = _gold >= target.GoldCost && _lumber >= target.LumberCost;
            bool requires = RequirementsMet(target.Requires);
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

    private void AddBuildCommands(List<CommandEntry> commands, UnitState worker)
    {
        foreach (string buildingId in worker.Type.Builds)
        {
            UnitType type = UnitCatalog.Get(buildingId);
            if (type == null) continue;

            bool afford = _gold >= type.GoldCost && _lumber >= type.LumberCost;
            bool requires = RequirementsMet(type.Requires);
            bool known = HasBuilding(buildingId);
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
                Active = known,
                Action = () => BeginPlacement(id),
            });
        }
    }

    private void BeginPlacement(string buildingId)
    {
        UnitType type = UnitCatalog.Get(buildingId);
        if (type == null) return;
        if (_gold < type.GoldCost || _lumber < type.LumberCost)
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

    public float TimeScaleHint = 1f;

    private void TryPlace(Vector2 screenPosition)
    {
        if (!TryGroundPoint(screenPosition, out Vector3 ground))
        {
            _hud?.SetHint("地面位置无效");
            return;
        }
        PlaceBuildingAt(_placingId, ground, true);
    }

    private UnitState PlaceBuildingAt(string buildingId, Vector3 ground, bool report)
    {
        UnitType type = UnitCatalog.Get(buildingId);
        if (type == null) { _placingId = null; return null; }

        if (_gold < type.GoldCost || _lumber < type.LumberCost)
        {
            if (report) _hud?.SetHint("资源不足");
            _placingId = null;
            return null;
        }
        if (_pathGrid != null && !_pathGrid.IsAreaFree(ground, 96f))
        {
            if (report) _hud?.SetHint("这里放不下");
            return null;
        }

        foreach (UnitState other in _sim.Units)
        {
            if (!other.Alive || !other.IsBuilding) continue;
            float distance = new Vector2(other.Position.X - ground.X, other.Position.Z - ground.Z).Length();
            if (distance < 200f)
            {
                if (report) _hud?.SetHint("离已有建筑太近");
                return null;
            }
        }

        UnitState site = Spawn(buildingId, 0, ground.X, ground.Z, 0f, 0f, 1f);
        if (site == null)
        {
            if (report) _hud?.SetHint("建筑模型缺失");
            _placingId = null;
            return null;
        }

        _gold -= type.GoldCost;
        _lumber -= type.LumberCost;

        site.IsUnderConstruction = true;
        site.BuildTotal = Mathf.Max(2f, type.BuildSeconds * TimeScaleHint);
        site.BuildRemaining = site.BuildTotal;
        site.Hp = 1f;
        site.MaxHp = type.Hp;
        site.View?.SetHp(0.01f, true);
        site.View?.Play("Birth");
        _sim.Units.Add(site);
        _pathGrid?.AddBlocker(ground, 96f);
        _builtCounts[buildingId] = BuiltCount(buildingId) + 1;

        foreach (UnitState worker in _selected)
        {
            if (!worker.IsWorker) continue;
            worker.Tree = null;
            worker.GoldMine = null;
            worker.Path = null;
            worker.MoveTarget = ground + new Vector3(160f, 0f, 0f);
            break;
        }

        GD.Print($"[GAME] 开工 {type.Name} 位置 {ground} 需要 {site.BuildTotal:0}s，资源 {_gold}/{_lumber}");
        _placingId = null;
        RefreshHud();
        UpdateCommandCard();
        _hud?.SetHint($"{type.Name} 建造中（{site.BuildTotal:0}s）");
        return site;
    }

    private int ResearchLevel(string upgradeId)
    {
        return _researchLevels.TryGetValue(upgradeId, out int level) ? level : 0;
    }

    private bool StartTraining(UnitState building, string unitId)
    {
        UnitType type = UnitCatalog.Get(unitId);
        if (type == null || building == null || !building.Alive) return false;
        if (_gold < type.GoldCost || _lumber < type.LumberCost)
        {
            _hud?.SetHint($"资源不足：需要 {type.GoldCost} 金 / {type.LumberCost} 木");
            return false;
        }
        if (!RequirementsMet(type.Requires))
        {
            _hud?.SetHint($"缺少前置建筑：{MissingRequirement(type.Requires)}");
            return false;
        }
        if (FoodUsed() + type.FoodUsed > FoodCap())
        {
            _hud?.SetHint("人口已满，先建农场");
            return false;
        }

        _gold -= type.GoldCost;
        _lumber -= type.LumberCost;
        _orders.Add(new BuildingOrder
        {
            Building = building,
            Kind = OrderKind.Train,
            Id = unitId,
            Remaining = Mathf.Max(1f, type.TrainSeconds * TimeScaleHint),
            Total = Mathf.Max(1f, type.TrainSeconds * TimeScaleHint),
        });
        _hud?.SetHint($"开始训练 {type.Name}（{type.TrainSeconds:0}s）");
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    private bool StartResearch(UnitState building, string upgradeId)
    {
        UpgradeType upgrade = TechTree.Upgrade(upgradeId);
        if (upgrade == null || building == null || !building.Alive) return false;

        int next = ResearchLevel(upgradeId) + CountResearchOrders(upgradeId) + 1;
        if (next > upgrade.MaxLevel) return false;

        int gold = upgrade.GoldFor(next);
        int lumber = upgrade.LumberFor(next);
        if (_gold < gold || _lumber < lumber)
        {
            _hud?.SetHint($"资源不足：需要 {gold} 金 / {lumber} 木");
            return false;
        }
        if (!RequirementsMet(upgrade.Requires))
        {
            _hud?.SetHint($"缺少前置建筑：{MissingRequirement(upgrade.Requires)}");
            return false;
        }

        _gold -= gold;
        _lumber -= lumber;
        float seconds = Mathf.Max(1f, upgrade.SecondsFor(next) * TimeScaleHint);
        _orders.Add(new BuildingOrder
        {
            Building = building,
            Kind = OrderKind.Research,
            Id = upgradeId,
            Level = next,
            Remaining = seconds,
            Total = seconds,
        });
        GD.Print($"[内务] 研究入队 {upgradeId} 等级 {next} 于 {building.Type.Name}");
        _hud?.SetHint($"开始研究 {upgrade.NameFor(next)}（{seconds:0}s）");
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    private bool StartBuildingUpgrade(UnitState building, string targetId)
    {
        UnitType target = UnitCatalog.Get(targetId);
        if (target == null || building == null || !building.Alive) return false;
        if (_gold < target.GoldCost || _lumber < target.LumberCost)
        {
            _hud?.SetHint($"资源不足：需要 {target.GoldCost} 金 / {target.LumberCost} 木");
            return false;
        }
        if (!RequirementsMet(target.Requires))
        {
            _hud?.SetHint($"缺少前置建筑：{MissingRequirement(target.Requires)}");
            return false;
        }

        _gold -= target.GoldCost;
        _lumber -= target.LumberCost;
        float seconds = Mathf.Max(1f, target.BuildSeconds * TimeScaleHint);
        _orders.Add(new BuildingOrder
        {
            Building = building,
            Kind = OrderKind.Upgrade,
            Id = targetId,
            Remaining = seconds,
            Total = seconds,
        });
        _hud?.SetHint($"{building.Type.Name} 升级为 {target.Name}（{seconds:0}s）");
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    private string MissingRequirement(string[] requires)
    {
        if (requires == null) return "";
        foreach (string id in requires)
        {
            if (HasBuilding(id)) continue;
            UnitType type = UnitCatalog.Get(id);
            return type != null ? type.Name : id;
        }
        return "";
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

        _researchLevels[order.Id] = order.Level;
        _sim?.SetResearchLevel(0, order.Id, order.Level);
        GD.Print($"[TECH] 研究完成 {upgrade.Id} {upgrade.NameFor(order.Level)} 等级 {order.Level}/{upgrade.MaxLevel}");
        _hud?.SetHint($"研究完成：{upgrade.NameFor(order.Level)}");
        RefreshHud();
        UpdateCommandCard();
    }

    private void SpawnTrainedUnit(BuildingOrder order)
    {
        float angle = _allUnits.Count * 0.9f;
        float worldX = order.Building.Position.X + Mathf.Cos(angle) * 150f;
        float worldZ = order.Building.Position.Z + Mathf.Sin(angle) * 150f;
        UnitState unit = Spawn(order.Id, order.Building.Player, worldX, worldZ, Mathf.Pi + angle, 0f, 1f);
        if (unit == null) return;

        _sim?.Units.Add(unit);
        GD.Print($"[GAME] 训练完成 {unit.Type.Name} 位于 {unit.Position}，金币 {_gold}");
        RefreshHud();
    }

    private void ReplaceBuilding(UnitState building, string targetId)
    {
        Vector3 position = building.Position;
        float facing = building.Facing;
        bool selected = _selected.Contains(building);

        if (selected)
        {
            _selected.Remove(building);
            building.View?.SetSelected(false);
        }

        _sim?.Units.Remove(building);
        _allUnits.Remove(building);
        _own.Remove(building);
        building.Alive = false;
        building.View?.Root.QueueFree();

        UnitState replacement = Spawn(targetId, 0, position.X, position.Z, facing, 0f, 1f);
        if (replacement == null) return;

        replacement.IsUnderConstruction = false;
        _sim?.Units.Add(replacement);
        MarkBuildingKnown(targetId);

        if (selected)
        {
            _selected.Add(replacement);
            replacement.View?.SetSelected(true);
            _hud?.SetSelection(_selected);
        }
        GD.Print($"[GAME] 升级完成 {replacement.Type.Name} 位于 {position}");
        RefreshHud();
        UpdateCommandCard();
    }

    private void UpdateQueueLabel()
    {
        if (_hud == null) return;
        if (_orders.Count == 0)
        {
            _hud.SetQueue(Housekeeping ? "[内务] 进行中" : "");
            return;
        }

        BuildingOrder order = _orders[0];
        string label = order.Kind switch
        {
            OrderKind.Train => $"训练 {UnitCatalog.Get(order.Id)?.Name}",
            OrderKind.Research => $"研究 {TechTree.Upgrade(order.Id)?.NameFor(order.Level)}",
            _ => $"升级 {UnitCatalog.Get(order.Id)?.Name}",
        };
        _hud.SetQueue($"{order.Building.Type.Name}：{label} {Mathf.Max(0f, order.Remaining):0.0}s（队列 {_orders.Count}）");
    }

    private void Back()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        QueueFree();
        _back?.Invoke();
    }

    private bool TryFindBuildSpot(Vector3 origin, string buildingId, out Vector3 spot)
    {
        spot = Vector3.Zero;
        for (float radius = 260f; radius <= 3000f; radius += 130f)
        {
            int steps = radius < 700f ? 16 : 24;
            for (int step = 0; step < steps; step++)
            {
                float angle = Mathf.Tau * step / steps;
                var candidate = new Vector3(
                    origin.X + Mathf.Cos(angle) * radius,
                    0f,
                    origin.Z + Mathf.Sin(angle) * radius);
                candidate.Y = MapWorldBuilder.HeightAt(_map.Terrain, candidate.X, candidate.Z);
                if (!IsSpotUsable(candidate)) continue;
                spot = candidate;
                return true;
            }
        }
        return false;
    }

    private bool IsSpotUsable(Vector3 candidate)
    {
        if (_pathGrid != null && !_pathGrid.IsAreaFree(candidate, 96f)) return false;
        foreach (UnitState other in _allUnits)
        {
            if (!other.Alive || !other.IsBuilding) continue;
            float distance = new Vector2(other.Position.X - candidate.X, other.Position.Z - candidate.Z).Length();
            if (distance < 210f) return false;
        }
        return true;
    }

    public bool Housekeeping;
    private readonly HashSet<string> _hallFamily = new(StringComparer.OrdinalIgnoreCase);
    private float _houseTimer;
    private bool _houseDone;
    private bool _houseAnnounced;

    private sealed class InhouseTask
    {
        public string Kind;
        public string Id;
        public string Target;
        public int Total = 1;
        public string Label = "";
    }

    private readonly List<InhouseTask> _inhouse = new();

    public void RunHousekeeping()
    {
        Housekeeping = true;
        BuildHousekeepingPlan();
        _hud?.SetHint("[内务] 开始：铺满基地建筑、升级、训练与科技");
    }

    private void BuildHousekeepingPlan()
    {
        _inhouse.Clear();
        _houseDone = false;
        _houseAnnounced = false;

        UnitState hall = FindHall(0);
        if (hall == null)
        {
            GD.Print("[内务] 找不到主基地，无法开始");
            Housekeeping = false;
            return;
        }

        string workerId = hall.Type.Trains.Length > 0 ? hall.Type.Trains[0] : null;
        UnitType worker = UnitCatalog.Get(workerId);

        _hallFamily.Clear();
        CollectUpgradeFamily(hall.Type.Id, _hallFamily);

        var buildable = new List<string>();
        if (worker != null)
        {
            foreach (string id in worker.Builds)
            {
                if (_hallFamily.Contains(id)) continue;
                buildable.Add(id);
            }
        }

        foreach (string id in buildable)
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null) continue;
            int total = Mathf.Max(1, type.UpgradeTo.Length);
            if (type.FoodMade > 0) total = Mathf.Max(total, 2);
            _inhouse.Add(new InhouseTask
            {
                Kind = "build",
                Id = id,
                Total = total,
                Label = type.Name,
            });
        }

        foreach (string id in buildable)
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null) continue;
            foreach (string upgradeId in type.Researches)
            {
                UpgradeType upgrade = TechTree.Upgrade(upgradeId);
                if (upgrade == null) continue;
                _inhouse.Add(new InhouseTask
                {
                    Kind = "research",
                    Id = upgradeId,
                    Total = upgrade.MaxLevel,
                    Label = upgrade.Name,
                });
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
                _inhouse.Add(new InhouseTask
                {
                    Kind = "train",
                    Id = unitId,
                    Total = unitId == workerId ? 6 : 1,
                    Label = unit.Name,
                });
            }
        }

        int buildCount = 0;
        int researchCount = 0;
        foreach (InhouseTask task in _inhouse)
        {
            if (task.Kind == "build") buildCount++;
            if (task.Kind == "research") researchCount++;
        }
        GD.Print($"[内务] 计划 {_inhouse.Count} 条：建筑 {buildCount} 种、科技 {researchCount} 项、其余为升级与训练");
        foreach (InhouseTask task in _inhouse)
        {
            GD.Print($"[内务]   {task.Kind,-8} {task.Id,-6} x{task.Total}  {task.Label}");
        }
    }

    private void TickHousekeeping(float delta)
    {
        if (!Housekeeping || _houseDone) return;
        _houseTimer += delta;
        if (_houseTimer < 0.3f) return;
        _houseTimer = 0f;

        UnitState hall = FindHall(0);
        if (hall == null) return;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (_orders.Count >= 4) break;
            if (FoodCap() - FoodUsed() <= 4 && ActionFarm(hall)) continue;
            if (ActionBuild(hall)) continue;
            if (ActionUpgrade(hall)) continue;
            if (ActionResearch(hall)) continue;
            if (ActionTrain(hall)) continue;
            break;
        }

        _houseStatusTimer += 0.3f;
        if (_houseStatusTimer >= 8f)
        {
            _houseStatusTimer = 0f;
            LogHousekeepingStatus();
        }

        if (_orders.Count > 0) return;

        bool allDone = true;
        foreach (InhouseTask task in _inhouse)
        {
            if (TaskDoneCount(task) < task.Total) { allDone = false; break; }
        }
        if (!allDone) return;

        _houseDone = true;
        Housekeeping = false;
        if (!_houseAnnounced)
        {
            _houseAnnounced = true;
            int buildings = 0;
            int units = 0;
            foreach (UnitState unit in _own)
            {
                if (!unit.Alive) continue;
                if (unit.IsBuilding) buildings++;
                else units++;
            }
            string summary = $"[内务] 完成：建筑 {buildings} 座，非建筑单位 {units} 个，资源 {_gold}/{_lumber}，人口 {FoodUsed()}/{FoodCap()}";
            GD.Print(summary);
            _hud?.SetHint(summary);
            LogCombatStats();
        }
    }

    private float _houseStatusTimer;

    private void LogHousekeepingStatus()
    {
        int buildDone = 0;
        int buildTotal = 0;
        int researchDone = 0;
        int researchTotal = 0;
        int trainDone = 0;
        int trainTotal = 0;
        foreach (InhouseTask task in _inhouse)
        {
            int done = TaskDoneCount(task);
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
                default:
                    break;
            }
        }

        GD.Print($"[内务] 进度 建筑 {buildDone}/{buildTotal}  科技 {researchDone}/{researchTotal}  兵种 {trainDone}/{trainTotal}"
            + $"  队列 {_orders.Count}  人口 {FoodUsed()}/{FoodCap()}  资源 {_gold}/{_lumber}");
    }

    private void LogCombatStats()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UnitState unit in _own)
        {
            if (!unit.Alive || unit.IsBuilding) continue;
            if (!seen.Add(unit.Type.Id)) continue;

            float bonus = _sim != null ? _sim.DamageBonus(unit) : 0f;
            float armor = _sim != null ? _sim.ArmorOf(unit) : unit.Type.Armor;
            GD.Print($"[战力] {unit.Type.Name,-8} 攻击 {unit.Type.MinDamage + bonus:0}-{unit.Type.MaxDamage + bonus:0}"
                + $"（基础 {unit.Type.MinDamage:0}-{unit.Type.MaxDamage:0}）  护甲 {armor:0}  关联科技 {unit.Type.UpgradesApplied.Length}");
        }
    }

    private int TaskDoneCount(InhouseTask task)
    {
        if (task == null) return 0;
        switch (task.Kind)
        {
            case "build":
                return Mathf.Max(CountBuildings(task.Id), BuiltCount(task.Id));
            case "upgrade":
                return CountBuildings(task.Target);
            case "research":
                return ResearchLevel(task.Id) + CountResearchOrders(task.Id);
            case "train":
                return CountUnits(task.Id) + CountOrders(task.Id);
            default:
                return 0;
        }
    }

    private int CountBuildings(string id)
    {
        int count = 0;
        foreach (UnitState unit in _own)
        {
            if (unit.Alive && unit.IsBuilding && string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int BuiltCount(string id)
    {
        return _builtCounts.TryGetValue(id, out int count) ? count : 0;
    }

    private int CountBuildingsUnderConstruction(string id)
    {
        int count = 0;
        foreach (UnitState unit in _own)
        {
            if (!unit.Alive || !unit.IsBuilding || !unit.IsUnderConstruction) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountUnits(string id)
    {
        int count = 0;
        foreach (UnitState unit in _own)
        {
            if (unit.Alive && !unit.IsBuilding && string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountOrders(string id)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Kind == OrderKind.Train && string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountResearchOrders(string id)
    {
        int count = 0;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Kind == OrderKind.Research && string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private void CollectUpgradeFamily(string id, HashSet<string> family)
    {
        if (string.IsNullOrEmpty(id) || !family.Add(id)) return;
        UnitType type = UnitCatalog.Get(id);
        if (type == null) return;
        foreach (string target in type.UpgradeTo) CollectUpgradeFamily(target, family);
    }

    private bool ActionFarm(UnitState hall)
    {
        UnitState worker = FindWorker(0);
        if (worker == null) return false;

        foreach (string id in worker.Type.Builds)
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null || type.FoodMade <= 0) continue;
            if (type.Trains.Length > 0) continue;
            if (_hallFamily.Contains(id)) continue;
            if (CountBuildings(id) >= 12) continue;
            if (CountBuildingsUnderConstruction(id) > 0) continue;
            if (!RequirementsMet(type.Requires)) continue;
            return BuildOne(id, hall);
        }
        return false;
    }

    private bool ActionBuild(UnitState hall)
    {
        foreach (InhouseTask task in _inhouse)
        {
            if (task.Kind != "build") continue;
            if (TaskDoneCount(task) >= task.Total) continue;
            UnitType type = UnitCatalog.Get(task.Id);
            if (type == null || !RequirementsMet(type.Requires)) continue;
            return BuildOne(task.Id, hall);
        }
        return false;
    }

    private bool BuildOne(string buildingId, UnitState hall)
    {
        UnitType type = UnitCatalog.Get(buildingId);
        if (type == null) return false;
        if (_gold < type.GoldCost || _lumber < type.LumberCost) return false;

        if (!TryFindBuildSpot(hall.Position, buildingId, out Vector3 spot))
        {
            GD.Print($"[内务] 找不到 {type.Name} 的位置");
            return false;
        }

        UnitState site = PlaceBuildingAt(buildingId, spot, false);
        if (site == null) return false;

        UnitState worker = FindWorker(0);
        if (worker != null)
        {
            worker.MoveTarget = spot + new Vector3(150f, 0f, 0f);
            worker.Path = null;
        }
        GD.Print($"[内务] 建造 {type.Name} 位置 {spot.X:0},{spot.Z:0} 完成需 {site.BuildTotal:0}s");
        return true;
    }

    private bool ActionUpgrade(UnitState hall)
    {
        foreach (UnitState unit in _own)
        {
            if (!unit.Alive || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            if (unit.Type.UpgradeTo.Length == 0) continue;

            string targetId = null;
            foreach (string candidate in unit.Type.UpgradeTo)
            {
                if (CountBuildings(candidate) > 0) continue;
                UnitType target = UnitCatalog.Get(candidate);
                if (target == null || !RequirementsMet(target.Requires)) continue;
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
            if (type != null)
            {
                _gold += type.GoldCost;
                _lumber += type.LumberCost;
            }
            GD.Print($"[内务] 暂停训练 {type?.Name} 让位给 {building.Type.Name} 升级");
            _orders.RemoveAt(index);
        }
    }

    private bool HasPendingUpgrade(UnitState building)
    {
        foreach (string candidate in building.Type.UpgradeTo)
        {
            if (CountBuildings(candidate) > 0) continue;
            return true;
        }
        return false;
    }

    private bool ActionResearch(UnitState hall)
    {
        foreach (InhouseTask task in _inhouse)
        {
            if (task.Kind != "research") continue;
            if (TaskDoneCount(task) >= task.Total) continue;

            UpgradeType upgrade = TechTree.Upgrade(task.Id);
            if (upgrade == null || !RequirementsMet(upgrade.Requires)) continue;

            foreach (UnitState unit in _own)
            {
                if (!unit.Alive || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (Array.IndexOf(unit.Type.Researches, task.Id) < 0) continue;
                if (QueueLength(unit) >= 2) continue;
                return StartResearch(unit, task.Id);
            }
        }
        return false;
    }

    private bool ActionTrain(UnitState hall)
    {
        foreach (InhouseTask task in _inhouse)
        {
            if (task.Kind != "train") continue;
            if (TaskDoneCount(task) >= task.Total) continue;

            UnitType type = UnitCatalog.Get(task.Id);
            if (type == null || !RequirementsMet(type.Requires)) continue;
            if (FoodUsed() + type.FoodUsed > FoodCap()) continue;

            foreach (UnitState unit in _own)
            {
                if (!unit.Alive || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (Array.IndexOf(unit.Type.Trains, task.Id) < 0) continue;
                if (QueueLength(unit) >= 2) continue;
                if (HasPendingUpgrade(unit)) continue;
                return StartTraining(unit, task.Id);
            }
        }
        return false;
    }
}
