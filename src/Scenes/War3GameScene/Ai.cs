using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
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

            Unit hall = FindHall(player.Id);
            if (hall == null) continue;

            int race = Mathf.Clamp(player.Race, 1, 4);
            string unitId = RaceMelee[race];
            float angle = Mathf.Pi;
            float worldX = hall.Position.X + Mathf.Cos(angle) * 220f;
            float worldZ = hall.Position.Z + Mathf.Sin(angle) * 220f;
            Unit attacker = Spawn(unitId, player.Id, worldX, worldZ, Mathf.Pi + angle, 0f, 1f);
            if (attacker == null) continue;

            _world.Units.Add(attacker);
            attacker.Target = NearestTargetOf(ViewPlayer, hall.Position);

            // 有活着的英雄就一起上（英雄自己会施法/治疗）
            Unit hero = null;
            foreach (Unit unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != player.Id || !unit.IsHero) continue;
                hero = unit;
                break;
            }
            if (hero != null)
            {
                hero.Target = NearestTargetOf(ViewPlayer, hall.Position);
                hero.MoveTarget = null;
                hero.Path = null;
            }
        }
    }

    private Unit NearestTargetOf(int player, Vector3 from)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
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

    private Unit FindWorker(int player)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit.Alive && unit.Player == player && unit.IsWorker) return unit;
        }
        return null;
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

        Unit hall = FindHall(slot.Id);
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

        Unit hall = FindHall(slot.Id);
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
        foreach (Unit unit in _allUnits)
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
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountBuildingsUnderConstruction(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || !unit.IsUnderConstruction) continue;
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountUnits(PlayerSlot slot, string id)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
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
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.GoldMine != null) golders++;
            else if (unit.Tree != null) choppers++;
        }

        if (slot.Id == ViewPlayer)
        {
            foreach (Unit worker in _allUnits)
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
        foreach (Unit site in _allUnits)
        {
            if (!site.Alive || site.Player != slot.Id || !site.IsBuilding || !site.IsUnderConstruction) continue;
            if (SiteHasBuilder(site)) continue;

            Unit best = null;
            float bestDistance = float.MaxValue;
            foreach (Unit unit in _allUnits)
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
            _world.SetBuild(best, site);
            return true;
        }
        return false;
    }

    private bool ActionGather(PlayerSlot slot)
    {
        Unit worker = null;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (!unit.Type.CanGatherGold && !unit.Type.CanGatherLumber) continue;
            if (!IsIdle(unit)) continue;
            worker = unit;
            break;
        }
        if (worker == null) return false;

        Unit mine = null;
        float mineDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
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
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id) continue;
            if (unit.GoldMine != null) goldWorkers++;
        }

        if (worker.BuildSite != null) return false;

        if (worker.Type.CanGatherGold && mine != null && goldWorkers < 3)
        {
            _world.SetGather(worker, mine);
            return true;
        }

        if (!worker.Type.CanGatherLumber) return false;
        TreeState tree = NearestTreeTo(worker.Position);
        if (tree == null) return false;
        _world.SetChop(worker, tree);
        return true;
    }

    private static bool IsIdle(Unit unit)
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

    private bool ActionFarm(PlayerSlot slot, Unit hall)
    {
        Unit worker = FindWorker(slot.Id);
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

    private bool ActionBuild(PlayerSlot slot, Unit hall)
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
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding) continue;
            if (unit.IsUnderConstruction) count++;
        }
        return count;
    }

    private bool SiteHasBuilder(Unit site)
    {
        foreach (Unit unit in _allUnits)
        {
            if (unit.Alive && ReferenceEquals(unit.BuildSite, site)) return true;
        }
        return false;
    }

    private bool BuildOne(PlayerSlot slot, string buildingId, Unit hall)
    {
        UnitType type = UnitCatalog.Get(buildingId);
        if (type == null) return false;
        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost) return false;
        if (CountActiveSites(slot) >= 3) return false;

        // 不能把所有工人都派去盖房子，否则采集直接归零（亡灵 3 侍僧尤其明显）
        int freeWorkers = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
            if (unit.BuildSite == null) freeWorkers++;
        }
        if (freeWorkers == 0) return false;

        Vector3 spot;
        if (IsMineConsumingType(type))
        {
            Unit mine = FindConsumableMine(hall.Position, 2400f);
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

        Unit site = PlaceBuildingAt(slot, buildingId, spot, false);
        if (site == null) return false;
        return true;
    }

    private bool ActionUpgrade(PlayerSlot slot, Unit hall)
    {
        foreach (Unit unit in _allUnits)
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

    private bool ActionResearch(PlayerSlot slot, Unit hall)
    {
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "research") continue;
            if (TaskDone(slot, task) >= task.Total) continue;

            UpgradeType upgrade = TechTree.Upgrade(task.Id);
            if (upgrade == null || !RequirementsMet(slot, upgrade.Requires)) continue;

            foreach (Unit unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != slot.Id || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (Array.IndexOf(unit.Type.Researches, task.Id) < 0) continue;
                if (QueueLength(unit) >= 2) continue;
                return StartResearch(unit, task.Id);
            }
        }
        return false;
    }

    private bool ActionTrain(PlayerSlot slot, Unit hall)
    {
        foreach (InhouseTask task in slot.Tasks)
        {
            if (task.Kind != "train") continue;
            if (TaskDone(slot, task) >= task.Total) continue;

            UnitType type = UnitCatalog.Get(task.Id);
            if (type == null || !RequirementsMet(slot, type.Requires)) continue;
            if (FoodUsed(slot.Id) + type.FoodUsed > FoodCap(slot.Id)) continue;

            foreach (Unit unit in _allUnits)
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
