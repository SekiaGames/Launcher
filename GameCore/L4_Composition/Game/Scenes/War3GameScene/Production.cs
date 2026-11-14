using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

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

    private Unit PlaceBuildingAt(PlayerSlot slot, string buildingId, Vector3 ground, bool report,
        bool queue = false)
    {
        UnitType type = UnitFactory.Get(buildingId);
        if (type == null) { _placingId = null; ClearPlacementPreview(); return null; }

        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            if (report)
            {
                PlayNoResourceSound(slot, type);
                _hud?.SetHint("资源不足");
            }
            _placingId = null;
            ClearPlacementPreview();
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
                if (report) { PlayUiSound("CantPlaceSound", _camera.Target, ViewPlayer); _hud?.SetHint($"{type.Name} 必须建在金矿上"); }
                return null;
            }
            ground = consumedMine.Position;
        }

        if (_pathGrid != null && !_pathGrid.IsAreaFree(ground, footprint))
        {
            if (report) { PlayUiSound("CantPlaceSound", _camera.Target, ViewPlayer); _hud?.SetHint("这里放不下"); }
            return null;
        }

        foreach (Unit other in _allUnits)
        {
            if (!other.Alive || !other.IsBuilding || other == consumedMine) continue;
            float distance = new Vector2(other.Position.X - ground.X, other.Position.Z - ground.Z).Length();
            if (distance < (footprint + other.Type.FootprintRadius) * 0.92f)
            {
                if (report) { PlayErrorSound(); _hud?.SetHint("离已有建筑太近"); }
                return null;
            }
        }

        Unit site = Spawn(buildingId, slot.Id, ground.X, ground.Z, -Mathf.Pi * 0.5f, 0f, 1f);
        if (site == null)
        {
            if (report) _hud?.SetHint("建筑模型缺失");
            _placingId = null;
            ClearPlacementPreview();
            return null;
        }

        slot.Gold -= type.GoldCost;
        slot.Lumber -= type.LumberCost;

        site.IsUnderConstruction = true;
        site.BuildTotal = Mathf.Max(2f, type.BuildSeconds * TimeScaleHint);
        site.BuildRemaining = site.BuildTotal;
        site.Hp = 1f;
        site.MaxHp = type.Hp;
        site.View?.Play("Birth");
        PlayPlacementSound(ground);
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
        AssignBuilders(site, slot, wanted, queue);

        _placingId = null;
        ClearPlacementPreview();
        GD.Print($"[BUILD] P{slot.Id} 开工 {type.Name} 世界({ground.X:0},{ground.Z:0}) 工期 {site.BuildTotal:0}s 资源 {slot.Gold}/{slot.Lumber}");
        RefreshHud();
        UpdateCommandCard();
        return site;
    }

    private bool CancelConstruction(Unit site)
    {
        if (site == null || !site.Alive || !site.IsUnderConstruction) return false;

        if (site.Gold > 0)
        {
            _hud?.SetHint($"{site.Type.Name} 已经占住金矿，不能取消");
            return false;
        }

        UnitType type = site.Type;
        PlayerSlot slot = Slot(site.Player);
        if (slot != null)
        {
            slot.Gold += type.GoldCost;
            slot.Lumber += type.LumberCost;
            slot.BuiltCounts[type.Id] = Mathf.Max(0, slot.BuiltCount(type.Id) - 1);
        }

        foreach (Unit worker in _allUnits)
        {
            if (worker == null || !ReferenceEquals(worker.BuildSite, site)) continue;
            worker.BuildSite = null;
            worker.BuildingNow = false;
            worker.Path = null;
            worker.View?.Play("Stand");
        }

        float footprint = Mathf.Max(32f, type.FootprintRadius);
        _pathGrid?.RemoveBlocker(site.Position, footprint);
        _world?.Units.Remove(site);
        _allUnits.Remove(site);
        DropFromSelection(site);
        site.Alive = false;
        site.IsUnderConstruction = false;
        site.View?.Root.QueueFree();

        GD.Print($"[BUILD] P{site.Player} 取消建造 {type.Name}，退回 {type.GoldCost}金/{type.LumberCost}木");
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    private void AssignBuilders(Unit site, PlayerSlot slot, int wanted, bool queue)
    {
        int assigned = 0;
        foreach (Unit worker in _selected)
        {
            if (assigned >= wanted) break;
            if (!worker.IsWorker || worker.Player != slot.Id || !worker.Alive) continue;
            _world.SetBuild(worker, site, queue);
            assigned++;
        }

        if (assigned >= wanted) return;

        var candidates = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != slot.Id || !unit.IsWorker) continue;
            if (_selected.Contains(unit)) continue;
            if (!queue && unit.BuildSite != null) continue;
            if (unit.BuildQueue.Contains(site)) continue;
            candidates.Add(unit);
        }
        candidates.Sort((a, b) => a.DistanceTo(site).CompareTo(b.DistanceTo(site)));

        foreach (Unit worker in candidates)
        {
            if (assigned >= wanted) break;
            _world.SetBuild(worker, site, queue);
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
        if (building.ProductionBlocked) return false;
        if (Slot(building.Player) == null) return false;

        PlayerSlot slot = Slot(building.Player);
        UnitType type = UnitFactory.Get(unitId);
        if (type == null) return false;

        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            PlayErrorSound();
            _hud?.SetHint($"资源不足：需要 {type.GoldCost} 金 / {type.LumberCost} 木");
            return false;
        }
        if (!RequirementsMet(slot, type.Requires))
        {
            PlayErrorSound();
            _hud?.SetHint($"缺少前置建筑：{MissingRequirement(slot, type.Requires)}");
            return false;
        }
        if (FoodUsed(slot.Id) + type.FoodUsed > FoodCap(slot.Id))
        {
            PlayErrorSound();
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
        UnitType target = UnitFactory.Get(targetId);
        if (target == null) return false;
        if (slot.Gold < target.GoldCost || slot.Lumber < target.LumberCost) return false;
        if (!RequirementsMet(slot, target.Requires)) return false;

        slot.Gold -= target.GoldCost;
        slot.Lumber -= target.LumberCost;
        building.Upgrading = true;
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
                if (order.Building != null) order.Building.Upgrading = false;
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

            // 古树拔根期间停产：扎根回来继续倒数
            if (!order.Building.IsBuilding) continue;

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
                order.Building.Upgrading = false;
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
        _world?.RefreshUpgradeStats(slot.Id);
        RefreshSelection();
        GD.Print($"[TECH] P{slot.Id} 研究完成 {upgrade.Id} {upgrade.NameFor(order.Level)} {order.Level}/{upgrade.MaxLevel}");
        PlayUiSound($"ResearchComplete{RaceSoundSuffix(slot.Id)}", order.Building.Position, slot.Id);
        RefreshHud();
        UpdateCommandCard();
    }

    public bool BuyItemFromShop(Unit shop, int buyerPlayer, string itemId)
    {
        if (shop?.Type == null || _world == null) return false;

        ItemType type = ItemCatalog.Get(itemId);
        if (type == null) return false;

        PlayerSlot slot = Slot(buyerPlayer);
        if (slot == null) return false;

        float range = ShopPurchaseRange(shop);
        if (NearestShopBuyer(shop, buyerPlayer, range) == null)
        {
            _hud?.SetHint($"需要己方单位在 {shop.Type.Name} 的 {range:0} 范围内");
            return false;
        }

        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            _hud?.SetHint(type.LumberCost > 0
                ? $"{type.Name} 需要 {type.GoldCost} 金 / {type.LumberCost} 木，当前 {slot.Gold} 金 / {slot.Lumber} 木"
                : $"{type.Name} 需要 {type.GoldCost} 金，当前 {slot.Gold}");
            return false;
        }
        if (!shop.TakeStock(itemId))
        {
            _hud?.SetHint($"{shop.Type.Name} 的 {type.Name} 卖光了（{type.StockRegen:0}s 补一件）");
            return false;
        }

        slot.Gold -= type.GoldCost;
        slot.Lumber -= type.LumberCost;

        Unit carrier = NearestFriendlyHero(buyerPlayer, shop.Position, range);
        if (carrier != null && carrier.HasFreeSlot)
        {
            var carried = new Item { Type = type, Position = shop.Position };
            if (carrier.AddItem(carried))
            {
                _world.ApplyItemBonus(carrier, carried);
                _world.NotifyItemPickedUp(carrier, carried);
                _hud?.SetHint($"{shop.Type.Name}：买入 {type.Name}（-{type.GoldCost} 金，进了 {carrier.Type.Name} 背包）");
                RefreshHud();
                return true;
            }
        }

        if (carrier != null)
        {
            _hud?.SetHint($"{carrier.Type.Name} 的物品栏已满");
            PlayUiSound("InventoryFullSound", _camera.Target, ViewPlayer);
        }

        float angle = (float)(GD.Randi() % 628) / 100f;
        var spot = new Vector3(
            shop.Position.X + Mathf.Cos(angle) * (shop.Radius + 40f),
            shop.Position.Y,
            shop.Position.Z + Mathf.Sin(angle) * (shop.Radius + 40f));

        Item item = SpawnItem(itemId, spot);
        if (item == null)
        {
            slot.Gold += type.GoldCost;
            slot.Lumber += type.LumberCost;
            shop.AddStock(itemId);
            _hud?.SetHint($"买不了 {type.Name}（物品模型缺失）");
            return false;
        }

        _hud?.SetHint($"{shop.Type.Name}：买入 {type.Name}（-{type.GoldCost} 金，掉在店旁）");
        RefreshHud();
        return true;
    }

    /// <summary>
    /// 中立建筑买单位：酒馆卖英雄、雇佣兵营地/地精实验室/船坞/龙巢卖兵。
    /// 客户端口径（JassGameNatives.StockAndShops.PurchaseStock 的 unit 分支）：
    /// 必须有一个己方单位在购买半径内、按 Requires 判前置、占人口、英雄有名额，落在建筑旁的空地。
    /// </summary>
    public bool BuyUnitFromShop(Unit shop, int buyerPlayer, string unitId)
    {
        if (shop?.Type == null || _world == null) return false;

        PlayerSlot slot = Slot(buyerPlayer);
        if (slot == null) return false;

        UnitType type = UnitFactory.Get(unitId);
        if (type == null) return false;

        float range = ShopPurchaseRange(shop);
        if (NearestShopBuyer(shop, buyerPlayer, range) == null)
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId, $"没有己方单位在 {range:0} 范围内");
        }

        if (shop.UnitStockOf(unitId) <= 0)
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId, "没货");
        }

        if (type.IsHero && HeroSlotBlocked(slot, type))
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId, HeroBlockReason(slot, type));
        }

        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId,
                $"资源不足（需要 {type.GoldCost}金/{type.LumberCost}木，当前 {slot.Gold}/{slot.Lumber}）",
                NoResourceKey(slot, type));
        }

        if (FoodUsed(slot.Id) + type.FoodUsed > FoodCap(slot.Id))
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId,
                $"人口不足（需要 {type.FoodUsed}，当前 {FoodUsed(slot.Id)}/{FoodCap(slot.Id)}）",
                "NoFoodSound");
        }

        string[] requires = TechTree.Row(unitId)?.RequiresPrimary ?? Array.Empty<string>();
        if (!PurchaseRequirementsMet(slot, requires))
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId,
                $"缺少前置：{DescribeMissingPurchaseRequirement(slot, requires)}");
        }

        if (!TryFindPurchaseSpot(shop, type, out Vector3 spot))
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId, "建筑旁没有空地");
        }

        Unit unit = Spawn(unitId, slot.Id, spot.X, spot.Z, shop.Facing, 0f, 1f);
        if (unit == null)
        {
            return RejectUnitPurchase(shop, buyerPlayer, unitId, $"{type.Name} 模型缺失");
        }

        _world.Units.Add(unit);
        slot.Gold -= type.GoldCost;
        slot.Lumber -= type.LumberCost;
        shop.TakeUnitStock(unitId);

        PlayVoice(unit, "Ready", null);
        GD.Print($"[SHOP] P{slot.Id} 在 {shop.Type.Name} 买了 {type.Name}"
            + $"（-{type.GoldCost}金/-{type.LumberCost}木，剩货 {shop.UnitStockOf(unitId)}）");
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    /// <summary>买兵失败的唯一出口：提示 + 日志带上具体原因，便于对着跑测输出定位。</summary>
    private static string NoResourceKey(PlayerSlot slot, UnitType type)
    {
        if (slot.Gold < type.GoldCost) return "NoGoldSound";
        if (slot.Lumber < type.LumberCost) return "NoLumberSound";
        return "NoFoodSound";
    }

    private void PlayNoResourceSound(PlayerSlot slot, UnitType type)
        => PlayUiSound(NoResourceKey(slot, type), _camera.Target, ViewPlayer);

    private bool RejectUnitPurchase(Unit shop, int player, string unitId, string reason,
        string sound = "InterfaceError")
    {
        if (sound != null) PlayUiSound(sound, _camera.Target, ViewPlayer);
        _hud?.SetHint($"{shop?.Type?.Name} 买不了 {unitId}：{reason}");
        GD.Print($"[SHOP] P{player} 在 {shop?.Type?.Name} 买 {unitId} 失败：{reason}");
        return false;
    }

    /// <summary>购买半径：物编 Aneu/Aall 的 DataA1（商店 450，全员共享 600）；没有这个技能退 600。</summary>
    private static float ShopPurchaseRange(Unit shop)
    {
        if (shop?.Abilities != null)
        {
            foreach (Ability ability in shop.Abilities)
            {
                string code = ability.Type?.Code;
                if (string.IsNullOrEmpty(code)) continue;
                if (!code.Equals("Aneu", StringComparison.OrdinalIgnoreCase)
                    && !code.Equals("Aall", StringComparison.OrdinalIgnoreCase)) continue;

                float range = ability.Type.DataAt(1, 'A');
                return range > 0f ? range : ShopPurchaseFallbackRange;
            }
        }
        return ShopPurchaseFallbackRange;
    }

    private const float ShopPurchaseFallbackRange = 600f;

    /// <summary>店里有没有己方单位：客户端 FindUnitStockBuyer 只要求活着、同玩家、在半径内，建筑也算。</summary>
    private Unit NearestShopBuyer(Unit shop, int player, float range)
    {
        Unit best = null;
        float bestDistance = range;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || ReferenceEquals(unit, shop)) continue;
            if (unit.IsBuilding) continue;
            if (unit.Player != player) continue;
            float distance = unit.DistanceTo(shop);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = unit;
        }
        return best;
    }

    private int HeroCount(int player)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player || !unit.IsHero) continue;
            count++;
        }
        return count;
    }

    private int HeroTypeCount(int player, string heroId)
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != player || !unit.IsHero) continue;
            if (!string.Equals(unit.Type?.Id, heroId, StringComparison.OrdinalIgnoreCase)) continue;
            count++;
        }
        return count;
    }

    /// <summary>RuntimeBlizzardAbility.j 两条英雄限制：总数 bj_MELEE_HERO_LIMIT=3，同名 bj_MELEE_HERO_TYPE_LIMIT=1。</summary>
    private bool HeroSlotBlocked(PlayerSlot slot, UnitType type)
    {
        if (slot == null || type == null || !type.IsHero) return false;
        if (HeroCount(slot.Id) >= MeleeRules.HeroLimit) return true;
        return HeroTypeCount(slot.Id, type.Id) >= MeleeRules.HeroTypeLimit;
    }

    private string HeroBlockReason(PlayerSlot slot, UnitType type)
    {
        if (slot == null || type == null) return "";
        if (HeroCount(slot.Id) >= MeleeRules.HeroLimit) return $"英雄已到上限 {MeleeRules.HeroLimit}";
        return $"已经有 {type.Name} 了（同名英雄上限 {MeleeRules.HeroTypeLimit}）";
    }

    /// <summary>买兵前置：Requires 里的别名（TALT 任一祭坛、TWN2/TWN3 主基等级）按 ItemFunc.DependencyOr 展开成「或」。</summary>
    private bool PurchaseRequirementsMet(PlayerSlot slot, string[] requires, int depth = 0)
    {
        if (requires == null || requires.Length == 0) return true;

        foreach (string id in requires)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (HasBuilding(slot, id)) continue;
            if (depth >= 4) return false;

            string[] alternatives = TechTree.DependencyOr(id);
            bool satisfied = false;
            foreach (string alternative in alternatives)
            {
                if (!PurchaseRequirementsMet(slot, new[] { alternative }, depth + 1)) continue;
                satisfied = true;
                break;
            }
            if (satisfied) continue;
            return false;
        }
        return true;
    }

    private string DescribeMissingPurchaseRequirement(PlayerSlot slot, string[] requires)
    {
        if (requires == null) return "";
        foreach (string id in requires)
        {
            if (string.IsNullOrWhiteSpace(id) || HasBuilding(slot, id)) continue;
            string[] alternatives = TechTree.DependencyOr(id);
            if (alternatives.Length == 0)
            {
                UnitType type = UnitFactory.Get(id);
                return type != null ? type.Name : id;
            }
            var names = new List<string>();
            foreach (string alternative in alternatives)
            {
                UnitType type = UnitFactory.Get(alternative);
                names.Add(type != null ? type.Name : alternative);
            }
            return string.Join(" 或 ", names);
        }
        return "";
    }

    private Vector3 ResolveTrainedSpot(Unit building, UnitType type, out float yaw)
    {
        Vector3 center = building.Position;
        float radius = Mathf.Max(16f, type.CollisionRadius);
        float start = Mathf.Max(48f, building.Radius + radius + 24f);
        PathLayer layer = type.PathingLayer;
        yaw = building.Facing;

        if (building.RallyPoint.HasValue)
        {
            Vector3 delta = building.RallyPoint.Value - center;
            if (delta.LengthSquared() > 1f)
            {
                yaw = Mathf.Pi + Mathf.Atan2(-delta.X, delta.Z);
            }
        }

        for (float ring = 0f; ring <= 160f; ring += 32f)
        {
            int steps = ring <= 0f ? 1 : 9;
            for (int step = 0; step < steps; step++)
            {
                float offset = steps == 1 ? 0f : (step - (steps - 1) * 0.5f) * 0.42f;
                float angle = yaw + offset;
                float distance = start + ring;
                var candidate = new Vector3(
                    center.X + Mathf.Sin(angle) * distance,
                    0f,
                    center.Z - Mathf.Cos(angle) * distance);
                candidate.Y = _world != null
                    ? _world.SurfaceY(type, candidate.X, candidate.Z)
                    : MapWorldBuilder.HeightAt(_map.Terrain, candidate.X, candidate.Z);
                if (_pathGrid == null || _pathGrid.IsAreaFree(candidate, radius, layer))
                {
                    return candidate;
                }
            }
        }

        var fallback = new Vector3(
            center.X + Mathf.Sin(yaw) * start,
            0f,
            center.Z - Mathf.Cos(yaw) * start);
        fallback.Y = _world != null
            ? _world.SurfaceY(type, fallback.X, fallback.Z)
            : MapWorldBuilder.HeightAt(_map.Terrain, fallback.X, fallback.Z);
        return fallback;
    }

    /// <summary>落点：绕建筑找一圈能放下该单位碰撞体的空地（客户端 TryResolveSpawnPlacement 的近似）。</summary>
    private bool TryFindPurchaseSpot(Unit shop, UnitType type, out Vector3 spot)
    {
        float radius = shop.Radius + Mathf.Max(24f, type.CollisionRadius) + 32f;
        PathLayer layer = type.PathingLayer;
        int steps = 16;
        for (int step = 0; step < steps; step++)
        {
            float angle = Mathf.Tau * step / steps;
            var candidate = new Vector3(
                shop.Position.X + Mathf.Cos(angle) * radius,
                0f,
                shop.Position.Z + Mathf.Sin(angle) * radius);
            candidate.Y = _world != null
                ? _world.SurfaceY(type, candidate.X, candidate.Z)
                : MapWorldBuilder.HeightAt(_map.Terrain, candidate.X, candidate.Z);
            if (_pathGrid != null && !_pathGrid.IsAreaFree(candidate, Mathf.Max(16f, type.CollisionRadius), layer)) continue;
            spot = candidate;
            return true;
        }
        spot = Vector3.Zero;
        return false;
    }

    private void SpawnTrainedUnit(BuildingOrder order)
    {
        UnitType type = UnitFactory.Get(order.Id);
        if (type == null) return;

        Vector3 spot = ResolveTrainedSpot(order.Building, type, out float yaw);
        Unit unit = Spawn(order.Id, order.Player, spot.X, spot.Z, yaw, 0f, 1f);
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
        float hpRatio = Mathf.Clamp(building.Hp / Mathf.Max(1f, building.MaxHp), 0.01f, 1f);
        Vector3? rallyPoint = building.RallyPoint;
        Unit rallyTarget = building.RallyTarget;
        TreeState rallyTree = building.RallyTree;
        int gold = building.Gold;

        if (selected)
        {
            DropFromSelection(building);
            building.View?.SetSelected(false);
        }

        _world?.Units.Remove(building);
        _allUnits.Remove(building);
        building.Alive = false;
        building.View?.Root.QueueFree();

        Unit replacement = Spawn(targetId, player, position.X, position.Z, facing, 0f, 1f);
        if (replacement == null) return;

        replacement.Hp = Mathf.Clamp(replacement.MaxHp * hpRatio, 1f, replacement.MaxHp);
        replacement.RallyPoint = rallyPoint;
        replacement.RallyTarget = rallyTarget;
        replacement.RallyTree = rallyTree;
        replacement.Gold = gold;

        _world?.Units.Add(replacement);
        Slot(player)?.KnownBuildings.Add(targetId);

        if (selected)
        {
            _selected.Add(replacement);
            replacement.View?.SetSelected(true);
            RefreshSelection();
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

    private bool CancelBuildingOrder(Unit building, string preferredId)
    {
        if (building == null) return false;

        int found = -1;
        for (int index = _orders.Count - 1; index >= 0; index--)
        {
            BuildingOrder order = _orders[index];
            if (!ReferenceEquals(order.Building, building)) continue;
            if (found < 0) found = index;
            if (string.IsNullOrEmpty(preferredId)
                || !string.Equals(order.Id, preferredId, StringComparison.OrdinalIgnoreCase)) continue;
            found = index;
            break;
        }
        if (found < 0)
        {
            _hud?.SetHint($"{building.Type.Name} 没有可取消的订单");
            return false;
        }

        BuildingOrder cancelled = _orders[found];
        _orders.RemoveAt(found);
        RefundOrder(cancelled);
        if (cancelled.Kind == OrderKind.Upgrade && !HasOrderFor(building))
        {
            building.Upgrading = false;
            building.View?.Play("Stand");
        }
        _hud?.SetHint($"取消 {DescribeBuildingOrder(cancelled)}，已退款");
        RefreshHud();
        UpdateCommandCard();
        UpdateQueueLabel();
        return true;
    }

    private bool HasOrderFor(Unit building)
    {
        for (int index = 0; index < _orders.Count; index++)
        {
            if (ReferenceEquals(_orders[index].Building, building)) return true;
        }
        return false;
    }

    private void RefundOrder(BuildingOrder order)
    {
        PlayerSlot slot = Slot(order.Player);
        if (slot == null) return;

        switch (order.Kind)
        {
            case OrderKind.Train:
            case OrderKind.Upgrade:
            {
                UnitType type = UnitFactory.Get(order.Id);
                if (type == null) return;
                slot.Gold += type.GoldCost;
                slot.Lumber += type.LumberCost;
                break;
            }
            case OrderKind.Research:
            {
                UpgradeType upgrade = TechTree.Upgrade(order.Id);
                if (upgrade == null) return;
                slot.Gold += upgrade.GoldFor(order.Level);
                slot.Lumber += upgrade.LumberFor(order.Level);
                break;
            }
        }
        GD.Print($"[ORDER] P{order.Player} 取消 {DescribeBuildingOrder(order)} 退款：{slot.Gold}/{slot.Lumber}");
    }

    private static string DescribeBuildingOrder(BuildingOrder order)
    {
        return order.Kind switch
        {
            OrderKind.Train => $"训练 {UnitFactory.Get(order.Id)?.Name}",
            OrderKind.Research => $"研究 {TechTree.Upgrade(order.Id)?.NameFor(order.Level)}",
            _ => $"升级 {UnitFactory.Get(order.Id)?.Name}",
        };
    }

    private void CancelTrainingOrders(Unit building)
    {
        for (int index = _orders.Count - 1; index >= 0; index--)
        {
            BuildingOrder order = _orders[index];
            if (!ReferenceEquals(order.Building, building) || order.Kind != OrderKind.Train) continue;

            UnitType type = UnitFactory.Get(order.Id);
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
