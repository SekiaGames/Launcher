using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    private void RefreshConstruction()
    {
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || !unit.IsUnderConstruction) continue;
            float progress = unit.BuildTotal <= 0f ? 1f : 1f - unit.BuildRemaining / unit.BuildTotal;
            unit.View?.SetBuildProgress(progress);
        }
    }

    private void RefreshBars()
    {
        foreach (Unit unit in _allUnits)
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

    /// <summary>技能行：只列已学与可学，带等级。</summary>
    private static string AbilityText(Unit hero)
    {
        if (hero.Abilities.Count == 0) return "-";

        var names = new List<string>();
        foreach (Ability ability in hero.Abilities)
        {
            if (ability.Type == null) continue;
            if (ability.IsPassive && !ability.Learned) continue;
            names.Add(ability.Learned ? $"{ability.Name}{ability.Level}" : $"{ability.Name}(未学)");
        }
        return names.Count == 0 ? "-" : string.Join("、", names);
    }

    private string InventoryText(Unit hero)
    {
        if (hero.ItemCount == 0) return "空";
        var names = new List<string>();
        for (int slot = 0; slot < Unit.InventorySize; slot++)
        {
            Item item = hero.Inventory[slot];
            if (item != null) names.Add(item.Type.Name);
        }
        return $"{hero.ItemCount}/{Unit.InventorySize} " + string.Join("、", names);
    }

    private string RallyText(Unit building)
    {
        if (building.RallyTarget != null)
        {
            if (building.RallyTarget.Gold > 0) return $"采金 {building.RallyTarget.Type.Name}";
            return AreAllied(building.RallyTarget.Player, building.Player)
                ? $"跟随 {building.RallyTarget.Type.Name}"
                : $"攻击 {building.RallyTarget.Type.Name}";
        }
        if (building.RallyTree != null) return building.RallyTree.Alive ? "伐木" : "伐木（已倒）";
        if (building.RallyPoint.HasValue) return $"({building.RallyPoint.Value.X:0}, {building.RallyPoint.Value.Z:0})";
        return "未设";
    }

    private string DescribeUnit(Unit unit)
    {
        if (unit.IsUnderConstruction) return $"剩余 {Mathf.Max(0f, unit.BuildRemaining):0.0}s";
        if (unit.IsChanneling) return $"引导 {unit.ChannelAbility?.Name}    剩余 {unit.ChannelRemaining:0.0}s";

        if (unit.IsHero)
        {
            float bonusDamage = _world != null ? _world.DamageBonus(unit) : 0f;
            int next = HeroRules.XpToNext(unit.Level);
            return $"等级 {unit.Level}   力量 {unit.Strength}  敏捷 {unit.Agility}  智力 {unit.Intelligence}"
                + $"   攻击 {unit.Type.MinDamage + bonusDamage:0}-{unit.Type.MaxDamage + bonusDamage:0}（{CombatCodes.AttackLabel(unit.Type.AttackType)}）"
                + $"   法力 {unit.Mana:0}/{unit.MaxMana:0}   (主属性 {unit.Type.Primary})"
                + (unit.Level >= HeroRules.MaxLevel ? "   经验 满级" : $"   经验 {unit.Xp:0}/{next}  技能点 {unit.SkillPoints}")
                + $"   物品 {InventoryText(unit)}"
                + $"\n技能 {AbilityText(unit)}";
        }

        float bonus = _world != null ? _world.DamageBonus(unit) : 0f;
        float armor = _world != null ? _world.ArmorOf(unit) : unit.Type.Armor;
        float reduction = (1f - War3GameplayConfig.ArmorFactor(armor)) * 100f;
        if (unit.Type.CanAttack)
        {
            return $"攻击 {unit.Type.MinDamage + bonus:0}-{unit.Type.MaxDamage + bonus:0}（{CombatCodes.AttackLabel(unit.Type.AttackType)}）"
                + $"    护甲 {armor:0.0}（{CombatCodes.DefenseLabel(unit.Type.DefenseType)} 减伤{reduction:0}%）"
                + $"    射程 {unit.Type.AttackRange:0}";
        }
        return $"护甲 {armor:0.0}（{CombatCodes.DefenseLabel(unit.Type.DefenseType)} 减伤{reduction:0}%）"
            + $"    速度 {unit.Type.Speed:0}    供给 {unit.Type.FoodMade}";
    }

    private bool HasBuilding(PlayerSlot slot, string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        if (slot.KnownBuildings.Contains(id)) return true;
        foreach (Unit unit in _allUnits)
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
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || unit.IsBuilding) continue;
            used += Mathf.Max(1, unit.Type.FoodUsed);
        }
        return used;
    }

    private int FoodCap(int player)
    {
        int cap = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || !unit.IsBuilding || unit.IsUnderConstruction) continue;
            cap += unit.Type.FoodMade;
        }
        return cap;
    }

    private int QueueLength(Unit building)
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
        _hud?.SetClock(ClockText());
        UpdateQueueLabel();
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

    /// <summary>击杀赏金：UnitBalance 的 bountyplus + dice(dice, sides)，直接进击杀者金库。</summary>
    private void OnBountyAwarded(int player, int gold, Unit victim)
    {
        PlayerSlot slot = Slot(player);
        if (slot == null) return;

        slot.Gold += gold;
        RefreshHud();
        if (_bountyLogBudget > 0)
        {
            _bountyLogBudget--;
            GD.Print($"[BOUNTY] P{player} 击杀 {victim?.Type?.Name} 得到 {gold} 金（当前 {slot.Gold}）");
        }
    }

    private int _bountyLogBudget = 8;

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
            Unit single = _selected.Count == 1 ? _selected[0] : null;
            if (single != null && single.IsBuilding && !single.IsUnderConstruction)
            {
                AddTrainingCommands(commands, slot, single);
                AddResearchCommands(commands, slot, single);
                AddUpgradeCommands(commands, slot, single);
                if (single.Type.IsShop) AddShopCommands(commands, slot, single);
                // 古树的扎根/拔根、神像这类建筑主动技能也挂在命令卡上
                AddUnitAbilityCommands(commands, single);
            }
            else if (single != null && single.IsHero)
            {
                AddAbilityCommands(commands, single);
            }
            else if (_selected[0].IsWorker)
            {
                AddBuildCommands(commands, slot, _selected[0]);
            }
            else if (single != null)
            {
                AddUnitAbilityCommands(commands, single);
            }
        }

        _hud.SetCommands(commands);
    }

    private void AddTrainingCommands(List<CommandEntry> commands, PlayerSlot slot, Unit building)
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

            Unit target = building;
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

    private void AddResearchCommands(List<CommandEntry> commands, PlayerSlot slot, Unit building)
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
            Unit target = building;
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

    private void AddUpgradeCommands(List<CommandEntry> commands, PlayerSlot slot, Unit building)
    {
        foreach (string targetId in building.Type.UpgradeTo)
        {
            UnitType target = UnitCatalog.Get(targetId);
            if (target == null) continue;

            bool afford = slot.Gold >= target.GoldCost && slot.Lumber >= target.LumberCost;
            bool requires = RequirementsMet(slot, target.Requires);
            string buildingId = targetId;
            Unit source = building;
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

    private void AddBuildCommands(List<CommandEntry> commands, PlayerSlot slot, Unit worker)
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

    /// <summary>
    /// 商店卡：上半是买（带库存），下半是卖（把附近友方英雄的物品按半价卖回店里）。
    /// 商品列表来自 `*UnitFunc.Sellitems`，库存来自 `ItemData.stockMax/stockRegen`。
    /// </summary>
    private void AddShopCommands(List<CommandEntry> commands, PlayerSlot slot, Unit shop)
    {
        int shown = 0;
        foreach (string itemId in shop.Type.SellItems)
        {
            ItemType type = ItemCatalog.Get(itemId);
            if (type == null) continue;

            string id = itemId;
            Unit target = shop;
            int stock = shop.StockOf(itemId);
            int max = shop.StockMaxOf(itemId);
            commands.Add(new CommandEntry
            {
                Id = id,
                Name = $"买 {type.Name}",
                Cost = $"{type.GoldCost}金",
                Tooltip = $"{type.Name}（等级 {type.Level} {type.Class}）\n{type.BonusText}\n库存 {stock}/{max}",
                Icon = TechTree.Icon(type.IconArt),
                Enabled = stock > 0 && slot.Gold >= type.GoldCost,
                Action = () => BuyItemFromShop(target, slot.Id, id),
            });

            if (++shown >= 6) break;
        }

        Unit seller = NearestFriendlyHero(slot.Id, shop.Position, 700f);
        if (seller == null) return;

        for (int index = 0; index < Unit.InventorySize; index++)
        {
            Item item = seller.Inventory[index];
            if (item?.Type == null || item.Type.GoldCost <= 0) continue;

            Unit shopRef = shop;
            Unit sellerRef = seller;
            int captured = index;
            int price = Mathf.Max(1, item.Type.GoldCost / 2);
            commands.Add(new CommandEntry
            {
                Id = item.Type.Id,
                Name = $"卖 {item.Type.Name}",
                Cost = $"+{price}金",
                Tooltip = $"卖给 {shop.Type.Name}：+{price} 金（半价）",
                Icon = TechTree.Icon(item.Type.IconArt),
                Enabled = true,
                Action = () => SellItemToShop(shopRef, sellerRef, captured),
            });
        }
    }

    private void SellItemToShop(Unit shop, Unit seller, int slot)
    {
        Item item = seller?.Inventory != null && slot < seller.Inventory.Length ? seller.Inventory[slot] : null;
        if (item?.Type == null || _world == null) return;

        int price = _world.SellItem(shop, item);
        if (price <= 0)
        {
            _hud?.SetHint($"{item.Type.Name} 卖不掉");
            return;
        }

        PlayerSlot slotData = Slot(seller.Player);
        if (slotData != null) slotData.Gold += price;

        _hud?.SetHint($"卖出 {item.Type.Name}：+{price} 金（半价）");
        RefreshHud();
        UpdateCommandCard();
    }

    /// <summary>某个玩家在给定位置附近的友方英雄（商店卖东西、物品转交都用它）。</summary>
    private Unit NearestFriendlyHero(int player, Vector3 from, float range)
    {
        Unit best = null;
        float bestDistance = range;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || !unit.IsHero) continue;
            if (!AreAllied(unit.Player, player)) continue;

            float distance = new Vector2(unit.Position.X - from.X, unit.Position.Z - from.Z).Length();
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = unit;
        }
        return best;
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
}
