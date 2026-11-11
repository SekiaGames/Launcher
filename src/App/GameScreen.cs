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
    private bool _middleDown;
    private bool _leftDown;
    private Vector2 _dragStart;
    private float _hudTimer;
    private PathGrid _pathGrid;
    private int _gold = 500;
    private int _lumber = 200;
    private readonly Queue<TrainOrder> _trainQueue = new();
    private List<TreeState> _trees = new();
    private string _placingId;

    private sealed class TrainOrder
    {
        public UnitState Hall;
        public string WorkerId;
        public float Remaining;
        public float Total;
    }

    public void Setup(MapSource map, Action back)
    {
        _map = map;
        _back = back;

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
        _hud.SetSelection(_selected);
        RefreshHud();
        LogNearestCreep();
    }

    private void LogNearestCreep()
    {
        UnitState hall = null;
        foreach (UnitState unit in _own)
        {
            if (unit.IsBuilding) { hall = unit; break; }
        }
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

    private readonly List<UnitState> _allUnits = new();

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
    
            if (Spawn(townHall, player.Id, worldX, worldZ, 0f, 0f, 1f) != null) spawned++;
    
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
        TickTraining((float)delta);
        TickAiWaves((float)delta);
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
                if (unit.IsBuilding && Trainables.ContainsKey(unit.Type.Id) && hall == null) hall = unit;
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
        UnitState worker = null;
        foreach (UnitState unit in _own)
        {
            if (unit.IsWorker) { worker = unit; break; }
        }
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
        UnitState hall = null;
        foreach (UnitState unit in _own)
        {
            if (unit.IsBuilding && HallWorkers.ContainsKey(unit.Type.Id)) { hall = unit; break; }
        }
        if (hall == null)
        {
            GD.Print("[TEST] 没有主基地");
            return;
        }

        ClearSelection();
        _selected.Add(hall);
        hall.View?.SetSelected(true);
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        StartTraining(hall, HallWorkers[hall.Type.Id]);
        StartTraining(hall, HallWorkers[hall.Type.Id]);
        GD.Print($"[TEST] 训练队列 {_trainQueue.Count} 个，金币 {_gold}");
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
        
            UnitState worker = null;
            foreach (UnitState unit in _own)
            {
                if (unit.IsWorker) { worker = unit; break; }
            }
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
            UnitState worker = null;
            foreach (UnitState unit in _own)
            {
                if (unit.IsWorker) { worker = unit; break; }
            }
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
            UnitState worker = null;
            UnitState hall = null;
            foreach (UnitState unit in _own)
            {
                if (unit.IsWorker && worker == null) worker = unit;
                if (unit.IsBuilding && HallWorkers.ContainsKey(unit.Type.Id)) hall = unit;
            }
            if (worker == null || hall == null)
            {
                GD.Print("[TEST] 缺少农民或基地");
                return;
            }
        
            ClearSelection();
            _selected.Add(worker);
            worker.View?.SetSelected(true);
            _hud?.SetSelection(_selected);
        
            Vector3 ground = Vector3.Zero;
            bool found = false;
            for (int r = 300; r <= 1000 && !found; r += 100)
            {
                for (int a = 0; a < 16 && !found; a++)
                {
                    float angle = Mathf.Tau * a / 16f;
                    var candidate = new Vector3(
                        hall.Position.X + Mathf.Cos(angle) * r,
                        0f,
                        hall.Position.Z + Mathf.Sin(angle) * r);
                    candidate.Y = MapWorldBuilder.HeightAt(_map.Terrain, candidate.X, candidate.Z);
                    if (_pathGrid == null || _pathGrid.IsAreaFree(candidate, 96f))
                    {
                        ground = candidate;
                        found = true;
                    }
                }
            }
            if (!found)
            {
                GD.Print("[TEST] 找不到可建造位置");
                return;
            }
        
            UnitState site = PlaceBuildingAt("hbar", ground, true);
            if (site == null) return;
        
            GD.Print($"[TEST] 兵营开工 位置 {ground} 工期 {site.BuildTotal:0}s");
            SceneTreeTimer timer = GetTree().CreateTimer(site.BuildTotal + 0.5f);
            timer.Timeout += () =>
            {
                if (!site.Alive || site.IsUnderConstruction)
                {
                    GD.Print($"[TEST] 兵营未完工 进行中={site.IsUnderConstruction} 剩余 {site.BuildRemaining:0.0}s");
                    return;
                }
                GD.Print("[TEST] 兵营完工，开始训练 hfoo");
                ClearSelection();
                _selected.Add(site);
                site.View?.SetSelected(true);
                _hud?.SetSelection(_selected);
                UpdateCommandCard();
                StartTraining(site, "hfoo");
            };
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

    private static readonly Dictionary<string, string> HallWorkers = new()
    {
        ["htow"] = "hpea",
        ["ogre"] = "opeo",
        ["unpl"] = "uaco",
        ["etol"] = "ewsp",
    };

    private void RefreshHud()
    {
        int pop = 0;
        foreach (UnitState unit in _allUnits)
        {
            if (unit.Alive && unit.Player == 0 && !unit.IsBuilding) pop++;
        }
        _hud?.SetResources(_gold, _lumber, pop, 100);
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

    private static readonly Dictionary<string, string> Trainables = new()
    {
        ["htow"] = "hpea",
        ["hbar"] = "hfoo",
        ["ogre"] = "opeo",
        ["unpl"] = "uaco",
        ["etol"] = "ewsp",
    };

    private void UpdateCommandCard()
    {
        if (_hud == null) return;

        UnitState single = _selected.Count == 1 ? _selected[0] : null;
        if (single != null && single.Player == 0)
        {
            if (single.IsBuilding && !single.IsUnderConstruction
                && Trainables.TryGetValue(single.Type.Id, out string workerId))
            {
                UnitType worker = UnitCatalog.Get(workerId);
                if (worker != null)
                {
                    bool afford = _gold >= worker.GoldCost && _lumber >= worker.LumberCost;
                    _hud.ShowTrain($"训练 {worker.Name}  {worker.GoldCost}金", afford,
                        () => StartTraining(single, workerId));
                    return;
                }
            }

            if (single.IsWorker && single.Type.Id == "hpea")
            {
                UnitType barracks = UnitCatalog.Get("hbar");
                if (barracks != null)
                {
                    bool afford = _gold >= barracks.GoldCost && _lumber >= barracks.LumberCost;
                    _hud.ShowTrain($"建造 {barracks.Name}  {barracks.GoldCost}金/{barracks.LumberCost}木", afford,
                        () => BeginPlacement("hbar"));
                    return;
                }
            }
        }

        _hud.HideTrain();
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
            if (distance < 220f)
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
        site.View?.SetHp(0.01f, true);
        site.View?.Play("Birth");
        _sim.Units.Add(site);
        _pathGrid?.AddBlocker(ground, 96f);

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

    private void StartTraining(UnitState hall, string workerId)
    {
        UnitType worker = UnitCatalog.Get(workerId);
        if (worker == null || hall == null || !hall.Alive) return;
        if (_gold < worker.GoldCost || _lumber < worker.LumberCost)
        {
            _hud?.SetHint($"资源不足：需要 {worker.GoldCost} 金 / {worker.LumberCost} 木");
            return;
        }

        _gold -= worker.GoldCost;
        _lumber -= worker.LumberCost;
        _trainQueue.Enqueue(new TrainOrder
        {
            Hall = hall,
            WorkerId = workerId,
            Remaining = Mathf.Max(2f, worker.TrainSeconds * TimeScaleHint),
            Total = Mathf.Max(2f, worker.TrainSeconds * TimeScaleHint),
        });
        _hud?.SetHint($"开始训练 {worker.Name}（{worker.TrainSeconds:0}s）");
        RefreshHud();
        UpdateQueueLabel();
    }

    private void TickTraining(float delta)
    {
        if (_trainQueue.Count == 0) return;

        TrainOrder order = _trainQueue.Peek();
        if (order.Hall == null || !order.Hall.Alive)
        {
            _trainQueue.Dequeue();
            UpdateQueueLabel();
            return;
        }

        order.Remaining -= delta;
        UpdateQueueLabel();
        if (order.Remaining > 0f) return;

        _trainQueue.Dequeue();
        SpawnTrainedWorker(order);
        UpdateQueueLabel();
    }

    private void SpawnTrainedWorker(TrainOrder order)
    {
        float angle = _allUnits.Count * 0.9f;
        float worldX = order.Hall.Position.X + Mathf.Cos(angle) * 150f;
        float worldZ = order.Hall.Position.Z + Mathf.Sin(angle) * 150f;
        UnitState unit = Spawn(order.WorkerId, order.Hall.Player, worldX, worldZ, Mathf.Pi + angle, 0f, 1f);
        if (unit == null) return;

        _sim.Units.Add(unit);
        GD.Print($"[TEST] 训练完成 {unit.Type.Name} 位于 {unit.Position}，金币 {_gold}");
        RefreshHud();
    }

    private void UpdateQueueLabel()
    {
        if (_hud == null) return;
        if (_trainQueue.Count == 0)
        {
            _hud.SetQueue("");
            return;
        }

        TrainOrder order = _trainQueue.Peek();
        UnitType worker = UnitCatalog.Get(order.WorkerId);
        _hud.SetQueue($"训练中：{worker?.Name} {Mathf.Max(0f, order.Remaining):0.0}s");
    }

    private void Back()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        QueueFree();
        _back?.Invoke();
    }
}