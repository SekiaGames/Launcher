using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    private enum OrderKind
    {
        Train,
        Research,
        Upgrade,
    }

    private sealed class BuildingOrder
    {
        public int Player;
        public Unit Building;
        public OrderKind Kind;
        public string Id;
        public int Level;
        public float Remaining;
        public float Total;
    }

    private readonly List<BuildingOrder> _orders = new();

    private Unit PlaceBuildingAt(PlayerSlot slot, string buildingId, Vector3 ground, bool report)
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

        // 客户端建筑落在 32 单位格上（金矿类必须贴在矿点上，不取整）
        if (!IsMineConsumingType(type))
        {
            ground = new Vector3(
                Mathf.Round(ground.X / 32f) * 32f,
                ground.Y,
                Mathf.Round(ground.Z / 32f) * 32f);
            ground.Y = MapWorldBuilder.HeightAt(_map.Terrain, ground.X, ground.Z);
        }

        Unit consumedMine = null;
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

        foreach (Unit other in _allUnits)
        {
            if (!other.Alive || !other.IsBuilding || other == consumedMine) continue;
            float distance = new Vector2(other.Position.X - ground.X, other.Position.Z - ground.Z).Length();
            if (distance < (footprint + other.Type.FootprintRadius) * 0.92f)
            {
                if (report) _hud?.SetHint("离已有建筑太近");
                return null;
            }
        }

        Unit site = Spawn(buildingId, slot.Id, ground.X, ground.Z, 0f, 0f, 1f);
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
        _world.Units.Add(site);
        _pathGrid?.AddBlocker(ground, footprint);
        slot.BuiltCounts[buildingId] = slot.BuiltCount(buildingId) + 1;

        if (consumedMine != null)
        {
            site.Gold = consumedMine.Gold;
            consumedMine.Alive = false;
            consumedMine.View?.Root.QueueFree();
            _allUnits.Remove(consumedMine);
            _world.Units.Remove(consumedMine);
            GD.Print($"[BUILD] P{slot.Id} {type.Name} 占据金矿，储量 {site.Gold}");
        }

        int wanted = 1;
        int selectedWorkers = 0;
        foreach (Unit worker in _selected)
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

    private void AssignBuilders(Unit site, PlayerSlot slot, int wanted)
    {
        int assigned = 0;
        foreach (Unit worker in _selected)
        {
            if (assigned >= wanted) break;
            if (!worker.IsWorker || worker.Player != slot.Id || !worker.Alive) continue;
            _world.SetBuild(worker, site);
            assigned++;
        }

        if (assigned >= wanted) return;

        var candidates = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
            if (unit.BuildSite != null || _selected.Contains(unit)) continue;
            candidates.Add(unit);
        }
        candidates.Sort((a, b) => a.DistanceTo(site).CompareTo(b.DistanceTo(site)));

        foreach (Unit worker in candidates)
        {
            if (assigned >= wanted) break;
            _world.SetBuild(worker, site);
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

    private Unit FindConsumableMine(Vector3 from, float maxDistance)
    {
        Unit best = null;
        float bestDistance = maxDistance;
        foreach (Unit unit in _allUnits)
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

    private bool StartTraining(Unit building, string unitId)
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

    private bool StartResearch(Unit building, string upgradeId)
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

    private bool StartBuildingUpgrade(Unit building, string targetId)
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
        _world?.SetResearchLevel(slot.Id, order.Id, order.Level);
        GD.Print($"[TECH] P{slot.Id} 研究完成 {upgrade.Id} {upgrade.NameFor(order.Level)} {order.Level}/{upgrade.MaxLevel}");
        PlayUiSound($"ResearchComplete{RaceSoundSuffix(slot.Id)}", order.Building.Position, slot.Id);
        RefreshHud();
        UpdateCommandCard();
    }

    /// <summary>
    /// 商店买物品：扣金 → 在商店旁边落地 → 交给拾取逻辑（英雄站在旁边就自动拿到）。
    /// 商品列表来自 `*UnitFunc` 的 Sellitems（地精商店 ngme = phea,pman,pinv,…）。
    /// </summary>
    public bool BuyItemFromShop(Unit shop, string itemId)
    {
        if (shop?.Type == null || _world == null) return false;

        ItemType type = ItemCatalog.Get(itemId);
        if (type == null) return false;

        PlayerSlot slot = Slot(shop.Player);
        if (slot == null) return false;
        if (slot.Gold < type.GoldCost)
        {
            _hud?.SetHint($"{type.Name} 需要 {type.GoldCost} 金，当前 {slot.Gold}");
            return false;
        }

        slot.Gold -= type.GoldCost;
        float angle = (float)(GD.Randi() % 628) / 100f;
        var spot = new Vector3(
            shop.Position.X + Mathf.Cos(angle) * (shop.Radius + 40f),
            shop.Position.Y,
            shop.Position.Z + Mathf.Sin(angle) * (shop.Radius + 40f));

        Item item = SpawnItem(itemId, spot);
        if (item == null)
        {
            slot.Gold += type.GoldCost;
            _hud?.SetHint($"买不了 {type.Name}（物品模型缺失）");
            return false;
        }

        _world.TickItemPickup();
        _hud?.SetHint($"{shop.Type.Name}：买入 {type.Name}（-{type.GoldCost} 金）");
        RefreshHud();
        return true;
    }

    private void SpawnTrainedUnit(BuildingOrder order)
    {
        float angle = _allUnits.Count * 0.9f;
        float worldX = order.Building.Position.X + Mathf.Cos(angle) * 150f;
        float worldZ = order.Building.Position.Z + Mathf.Sin(angle) * 150f;
        Unit unit = Spawn(order.Id, order.Player, worldX, worldZ, Mathf.Pi + angle, 0f, 1f);
        if (unit == null) return;

        _world?.Units.Add(unit);
        _world?.SendToRally(unit, order.Building);
        PlayVoice(unit, "Ready", null);
        RefreshHud();
    }

    private void ReplaceBuilding(Unit building, string targetId)
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

        _world?.Units.Remove(building);
        _allUnits.Remove(building);
        building.Alive = false;
        building.View?.Root.QueueFree();

        Unit replacement = Spawn(targetId, player, position.X, position.Z, facing, 0f, 1f);
        if (replacement == null) return;

        _world?.Units.Add(replacement);
        Slot(player)?.KnownBuildings.Add(targetId);

        if (selected)
        {
            _selected.Add(replacement);
            replacement.View?.SetSelected(true);
            _hud?.SetSelection(_selected);
        }
        GD.Print($"[BUILD] P{player} 升级完成 {replacement.Type.Name}");
        PlayUiSound($"UpgradeComplete{RaceSoundSuffix(player)}", replacement.Position, player);
        RefreshHud();
        UpdateCommandCard();
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
        foreach (Unit other in _allUnits)
        {
            if (!other.Alive || !other.IsBuilding) continue;
            float distance = new Vector2(other.Position.X - candidate.X, other.Position.Z - candidate.Z).Length();
            float spacing = (footprintRadius + other.Type.FootprintRadius) * 0.92f;
            if (distance < spacing) return false;
        }
        return true;
    }

    private void CancelTrainingOrders(Unit building)
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

    private bool HasPendingUpgrade(Unit building)
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
}
