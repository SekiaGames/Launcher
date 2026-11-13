using Godot;
using System;
using System.Collections.Generic;
using System.IO;

using OpenWC3.Game.UI;
using OpenWC3.Game.UI.Views;
using NVector4 = System.Numerics.Vector4;
namespace OpenWC3.Game;

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
        bool showAll = Input.IsKeyPressed(Key.Alt);
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive)
            {
                unit.View?.SetBarVisible(false);
                continue;
            }
            bool own = unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer);
            if (!own && _fog != null && _fog.Enabled && !_fog.IsVisible(unit.Position))
            {
                unit.View?.SetBarVisible(false);
                continue;
            }
            bool damaged = unit.Hp < unit.MaxHp - 0.01f;
            bool visible = damaged || _selected.Contains(unit);
            if (showAll)
            {
                visible = unit.Player == ViewPlayer
                    || _fog == null || !_fog.Enabled || _fog.IsVisible(unit.Position);
            }
            unit.View?.SetBarVisible(visible);
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
        float reduction = (1f - War3GameplayConfig.Default.ArmorFactor(armor)) * 100f;
        if (unit.Type.CanAttack)
        {
            return $"攻击 {unit.Type.MinDamage + bonus:0}-{unit.Type.MaxDamage + bonus:0}（{CombatCodes.AttackLabel(unit.Type.AttackType)}）"
                + $"    护甲 {armor:0.0}（{CombatCodes.DefenseLabel(unit.Type.DefenseType)} 减伤{reduction:0}%）"
                + $"    射程 {unit.Type.AttackRange:0}";
        }
        return $"护甲 {armor:0.0}（{CombatCodes.DefenseLabel(unit.Type.DefenseType)} 减伤{reduction:0}%）"
            + $"    速度 {unit.Type.Speed:0}    供给 {unit.Type.FoodMade}";
    }

    private static bool IsNeutralShop(Unit unit)
    {
        if (unit == null || !unit.IsBuilding || unit.Type == null) return false;
        if (unit.Player != MeleeRules.PlayerNeutralPassive && unit.Player != MeleeRules.PlayerNeutralHostile) return false;
        return unit.Type.IsShop || unit.Type.SellsUnits;
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
            UnitType type = UnitFactory.Get(id);
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

    private Texture2D LoadMinimapBaseTexture(MapSource map)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(map.Info?.MinimapTgaPath))
            candidates.Add(map.Info.MinimapTgaPath);
        candidates.Add("war3mapMap.tga");
        candidates.Add("war3mapMap.blp");
        candidates.Add("war3mapMap.png");
        candidates.Add("minimap.tga");

        foreach (string candidate in candidates)
        {
            string png = Path.ChangeExtension(candidate.Replace('\\', '/'), ".png");
            string path = "res://gmap/map/" + png;
            byte[] bytes = Assets.ReadBytes(path);
            if (bytes == null) continue;
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok) continue;
            GD.Print($"[MINIMAP] 底图={png}");
            return ImageTexture.CreateFromImage(image);
        }

        GD.PushWarning("[MINIMAP] 未找到小地图底图 (war3mapMap.png)");
        return null;
    }

    private readonly List<MinimapUnitView> _minimapUnits = new();
    private readonly List<MinimapPingView> _minimapPings = new();
    private readonly List<MinimapSpecialIconView> _minimapIcons = new();
    private int _nextMinimapPingId = 1;

    private void PingMinimap(System.Numerics.Vector2 world)
    {
        _minimapPings.Add(new MinimapPingView(_nextMinimapPingId++, world.X, world.Y,
            2.2f, 2.2f, 90, 255, 120, true));
    }

    private void UpdateMinimap(float delta)
    {
        if (_newHud == null || _map == null) return;

        for (int index = _minimapPings.Count - 1; index >= 0; index--)
        {
            MinimapPingView ping = _minimapPings[index];
            float remaining = ping.Remaining - delta;
            if (remaining <= 0f) _minimapPings.RemoveAt(index);
            else _minimapPings[index] = ping with { Remaining = remaining };
        }

        _minimapUnits.Clear();
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive) continue;
            MinimapFogLevel fog = MinimapFogFor(unit);
            if (fog == MinimapFogLevel.Masked) continue;
            bool allied = unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer);
            _minimapUnits.Add(MinimapUnitView.From(unit, fog, allied));
        }

        _newHud.Minimap.CameraRect = VisibleWorldRect();
        _newHud.Minimap.Update(_mapUiBounds, _minimapUnits, ViewPlayer, ViewPlayer,
            FogTexture(), System.Numerics.Vector2.One, _minimapPings, _minimapIcons,
            Time.GetTicksMsec() / 1000f);
    }

    private MinimapFogLevel MinimapFogFor(Unit unit)
    {
        if (_fog == null || !_fog.Enabled) return MinimapFogLevel.Visible;
        if (unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer))
            return MinimapFogLevel.Visible;
        if (_fog.IsVisible(unit.Position)) return MinimapFogLevel.Visible;
        if (!unit.IsBuilding) return MinimapFogLevel.Masked;
        return _fog.IsExplored(unit.Position) ? MinimapFogLevel.Fogged : MinimapFogLevel.Masked;
    }

    private NVector4 VisibleWorldRect()
    {
        Camera3D camera = _camera.Camera;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        Vector2[] corners =
        {
            new(0f, 0f), new(size.X, 0f), new(0f, size.Y), new(size.X, size.Y),
        };

        float planeY = _camera.Target.Y;
        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;
        int hits = 0;
        foreach (Vector2 corner in corners)
        {
            Vector3 origin = camera.ProjectRayOrigin(corner);
            Vector3 direction = camera.ProjectRayNormal(corner);
            if (Mathf.Abs(direction.Y) < 0.0001f) continue;
            float travel = (planeY - origin.Y) / direction.Y;
            if (travel <= 0f) continue;
            Vector3 point = origin + direction * travel;
            minX = Mathf.Min(minX, point.X);
            minZ = Mathf.Min(minZ, point.Z);
            maxX = Mathf.Max(maxX, point.X);
            maxZ = Mathf.Max(maxZ, point.Z);
            hits++;
        }

        if (hits < 2)
        {
            Vector3 target = _camera.Target;
            return new NVector4(-target.X - 700f, target.Z - 500f,
                -target.X + 700f, target.Z + 500f);
        }
        return new NVector4(-maxX, minZ, -minX, maxZ);
    }

    private void RefreshHud()
    {
        PlayerSlot slot = Slot(ViewPlayer);
        _hud?.SetResources(slot.Gold, slot.Lumber, FoodUsed(slot.Id), FoodCap(slot.Id));
        _newHud?.SetResources(slot.Gold, slot.Lumber, FoodUsed(slot.Id), FoodCap(slot.Id));
        _newHud?.SetClock(_todHours);
        _hud?.SetPlayerInfo(PlayerInfoText());
        _hud?.SetClock(ClockText());
        UpdateQueueLabel();
        UpdateUnitSummary();
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
        if (victim != null && player == ViewPlayer) ShowResourceFloat(victim, gold, false, true);
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
        if (player == ViewPlayer) ShowResourceFloat(FindHall(player), amount, true, false);
    }

    private void OnGoldDeposited(int player, int amount)
    {
        if (Slot(player) == null) return;
        // 维修费用：人口过高时只按比例入账（木头不扣）。
        int credited = War3GameplayConfig.Default.GoldAfterUpkeep(amount, FoodUsed(player));
        Slot(player).Gold += credited;
        RefreshHud();
        if (player == ViewPlayer) ShowResourceFloat(FindHall(player), credited, false, false);
    }

    private void UpdateCommandCard()
    {
        if (_hud == null) return;

        _hud.SetInventory(null);
        PlayerSlot slot = Slot(ViewPlayer);
        var commands = new List<CommandEntry>();
        if (_selected.Count > 0 && (_selected[0].Player == slot.Id || IsNeutralShop(_selected[0])))
        {
            Unit single = _selected.Count == 1 ? _selected[0] : null;
            if (single != null && single.IsBuilding && single.IsUnderConstruction)
            {
                AddCancelConstructionCommand(commands, single);
            }
            else if (single != null && single.IsBuilding && !single.IsUnderConstruction)
            {
                bool own = single.Player == slot.Id;
                if (own) AddTrainingCommands(commands, slot, single);
                AddSellUnitCommands(commands, slot, single);
                if (own)
                {
                    AddResearchCommands(commands, slot, single);
                    AddUpgradeCommands(commands, slot, single);
                }
                if (single.Type.IsShop) AddShopCommands(commands, slot, single);
                // 古树的扎根/拔根、神像这类建筑主动技能也挂在命令卡上（中立商店没有）
                if (own) AddUnitAbilityCommands(commands, single);
            }
            else if (single != null && single.IsHero)
            {
                AddStandardCommands(commands, true);
                AddAbilityCommands(commands, single);
            }
            else if (_selected[0].IsWorker)
            {
                AddStandardCommands(commands, true);
                AddBuildCommands(commands, slot, _selected[0]);
            }
            else if (single != null)
            {
                AddStandardCommands(commands, !single.IsBuilding);
                if (!single.IsBuilding) AddUnitAbilityCommands(commands, single);
            }
            else
            {
                foreach (Unit unit in _selected)
                {
                    if (unit.IsBuilding) continue;
                    AddStandardCommands(commands, true);
                    break;
                }
            }
        }

        _hud.SetCommands(commands);
        _newHud?.SetCommands(commands);
    }

    private void AddCancelConstructionCommand(List<CommandEntry> commands, Unit site)
    {
        Unit target = site;
        bool sealedByMine = site.Gold > 0;
        commands.Add(new CommandEntry
        {
            Id = "cmd-cancel-build",
            Name = "取消建造",
            SlotIndex = 8,
            Cost = sealedByMine ? "" : $"+{site.Type.GoldCost}金",
            Tooltip = sealedByMine
                ? "已占住金矿，不能取消"
                : $"取消建造并退回 {site.Type.GoldCost}金/{site.Type.LumberCost}木",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNCancel.blp"),
            Enabled = !sealedByMine,
            Action = () => CancelConstruction(target),
        });
    }

    private void AddTrainingCommands(List<CommandEntry> commands, PlayerSlot slot, Unit building)
    {
        foreach (string unitId in building.Type.Trains)
        {
            UnitType type = UnitFactory.Get(unitId);
            if (type == null) continue;

            bool afford = slot.Gold >= type.GoldCost && slot.Lumber >= type.LumberCost;
            bool food = FoodUsed(slot.Id) + type.FoodUsed <= FoodCap(slot.Id);
            bool requires = RequirementsMet(slot, type.Requires);
            bool room = QueueLength(building) < 5;
            bool heroRoom = !HeroSlotBlocked(slot, type);
            bool enabled = afford && food && requires && room && heroRoom;

            string cost = type.GoldCost > 0 ? $"{type.GoldCost}金" : "";
            if (type.LumberCost > 0) cost += (cost.Length > 0 ? "/" : "") + $"{type.LumberCost}木";
            if (type.FoodUsed > 0) cost += (cost.Length > 0 ? " " : "") + $"{type.FoodUsed}食";

            Unit target = building;
            string id = unitId;
            bool queued = QueueLength(building) > 0;
            commands.Add(new CommandEntry
            {
                Id = id,
                Name = queued ? $"训练 {type.Name}（队列）" : $"训练 {type.Name}",
                Cost = cost,
                Tooltip = $"训练 {type.Name}  {type.GoldCost}金 {type.LumberCost}木  {type.TrainSeconds:0}s"
                    + (heroRoom ? "" : $"\n{HeroBlockReason(slot, type)}")
                    + "\n右键：取消该建筑最后一个订单并退钱",
                Icon = TechTree.Icon(TechTree.UnitIconArt(id)),
                Enabled = enabled,
                Action = () => StartTraining(target, id),
                RightAction = () => CancelBuildingOrder(target, id),
            });
        }
    }

    /// <summary>
    /// 中立建筑卖兵：酒馆（`ntav` 卖中立英雄）、雇佣兵营地（`nmer`）、地精实验室（`ngad`）、
    /// 船坞（`nshp`）、龙巢（`ndro`）。清单来自 `*UnitFunc.Sellunits`，库存来自 UnitBalance 的
    /// stockMax/stockRegen/stockStart；买的条件与客户端一致：己方单位在购买半径内。
    /// </summary>
    private void AddSellUnitCommands(List<CommandEntry> commands, PlayerSlot slot, Unit building)
    {
        if (!building.Type.SellsUnits) return;

        float range = ShopPurchaseRange(building);
        bool buyerNearby = NearestShopBuyer(building, slot.Id, range) != null;

        foreach (string unitId in building.Type.SellUnits)
        {
            UnitType type = UnitFactory.Get(unitId);
            if (type == null) continue;

            int stock = building.UnitStockOf(unitId);
            int max = building.UnitStockMaxOf(unitId);
            bool unlimited = type.StockRegen <= 0f;

            bool afford = slot.Gold >= type.GoldCost && slot.Lumber >= type.LumberCost;
            bool food = FoodUsed(slot.Id) + type.FoodUsed <= FoodCap(slot.Id);
            bool heroRoom = !HeroSlotBlocked(slot, type);
            string[] requires = TechTree.Row(unitId)?.RequiresPrimary ?? Array.Empty<string>();
            bool requiresOk = PurchaseRequirementsMet(slot, requires);
            bool enabled = buyerNearby && stock > 0 && afford && food && heroRoom && requiresOk;

            string cost = type.GoldCost > 0 ? $"{type.GoldCost}金" : "";
            if (type.LumberCost > 0) cost += (cost.Length > 0 ? "/" : "") + $"{type.LumberCost}木";
            if (type.FoodUsed > 0) cost += (cost.Length > 0 ? " " : "") + $"{type.FoodUsed}食";

            var reasons = new List<string>();
            if (!buyerNearby) reasons.Add($"需要己方单位在 {range:0} 范围内");
            if (stock <= 0) reasons.Add(unlimited ? "没货" : $"没货（{type.StockRegen:0}s 补一件）");
            if (!afford) reasons.Add($"资源不足（{type.GoldCost}金 {type.LumberCost}木）");
            if (!food) reasons.Add("人口已满");
            if (!heroRoom) reasons.Add(HeroBlockReason(slot, type));
            if (!requiresOk) reasons.Add($"缺少前置：{DescribeMissingPurchaseRequirement(slot, requires)}");

            string stockText = unlimited ? "不限量" : $"{stock}/{max}";
            Unit target = building;
            string id = unitId;
            commands.Add(new CommandEntry
            {
                Id = id,
                Name = $"买 {type.Name}",
                Cost = cost,
                Tooltip = $"{type.Name}（库存 {stockText}）\n"
                    + (string.IsNullOrWhiteSpace(type.Tip) ? "" : type.Tip + "\n")
                    + (reasons.Count == 0 ? "点击买下，出现在建筑旁" : string.Join("\n", reasons)),
                Icon = TechTree.Icon(TechTree.UnitIconArt(id)),
                Enabled = enabled,
                Action = () => BuyUnitFromShop(target, slot.Id, id),
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
                Tooltip = $"{name}（{next}/{upgrade.MaxLevel}）\n{upgrade.Description}\n右键：取消该建筑最后一个订单并退钱",
                Icon = TechTree.Icon(upgrade.IconPath),
                Enabled = afford && requires && !busy,
                Action = () => StartResearch(target, id),
                RightAction = () => CancelBuildingOrder(target, id),
            });
        }
    }

    private void AddUpgradeCommands(List<CommandEntry> commands, PlayerSlot slot, Unit building)
    {
        foreach (string targetId in building.Type.UpgradeTo)
        {
            UnitType target = UnitFactory.Get(targetId);
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
                Tooltip = $"升级为 {target.Name}  {target.GoldCost}金 {target.LumberCost}木  {target.BuildSeconds:0}s\n右键：取消该建筑最后一个订单并退钱",
                Icon = TechTree.Icon(TechTree.UnitIconArt(buildingId)),
                Enabled = afford && requires,
                Action = () => StartBuildingUpgrade(source, buildingId),
                RightAction = () => CancelBuildingOrder(source, buildingId),
            });
        }
    }

    private void AddBuildCommands(List<CommandEntry> commands, PlayerSlot slot, Unit worker)
    {
        foreach (string buildingId in worker.Type.Builds)
        {
            UnitType type = UnitFactory.Get(buildingId);
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
        const int commandSlots = 12;
        float range = ShopPurchaseRange(shop);
        bool buyerNearby = NearestShopBuyer(shop, slot.Id, range) != null;

        int shown = 0;
        foreach (string itemId in shop.Type.SellItems)
        {
            ItemType type = ItemCatalog.Get(itemId);
            if (type == null) continue;

            string id = itemId;
            Unit target = shop;
            int stock = shop.StockOf(itemId);
            int max = shop.StockMaxOf(itemId);
            bool afford = slot.Gold >= type.GoldCost;
            commands.Add(new CommandEntry
            {
                Id = id,
                Name = $"买 {type.Name}",
                Cost = $"{type.GoldCost}金",
                Tooltip = $"{type.Name}（等级 {type.Level} {type.Class}）\n{type.BonusText}\n库存 {stock}/{max}"
                    + (buyerNearby ? "" : $"\n需要己方单位在 {range:0} 范围内")
                    + (afford ? "" : $"\n金币不足（{type.GoldCost}）"),
                Icon = TechTree.Icon(type.IconArt),
                Enabled = stock > 0 && afford && buyerNearby,
                Action = () => BuyItemFromShop(target, slot.Id, id),
            });

            if (++shown >= commandSlots) break;
        }

        Unit seller = NearestFriendlyHero(slot.Id, shop.Position, 700f);
        if (seller == null) return;

        int sellSlots = Mathf.Max(0, commandSlots - shown);
        for (int index = 0; index < Unit.InventorySize && sellSlots > 0; index++)
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
            sellSlots--;
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
        UnitType type = UnitFactory.Get(buildingId);
        if (type == null) return;
        if (slot.Gold < type.GoldCost || slot.Lumber < type.LumberCost)
        {
            PlayErrorSound();
            _hud?.SetHint($"资源不足：需要 {type.GoldCost} 金 / {type.LumberCost} 木");
            return;
        }

        _placingId = buildingId;
        CreatePlacementPreview(buildingId);
        _hud?.SetHint($"选择位置放置 {type.Name}（左键确认，右键取消）");
    }

    private void CancelPlacement()
    {
        if (_placingId == null) return;
        _placingId = null;
        ClearPlacementPreview();
        _hud?.SetHint("已取消建造");
    }

    public bool PlacementPreviewValid => _placementPreviewValid;
    public bool PlacementPreviewActive => _placementGhost != null;

    private void CreatePlacementPreview(string buildingId)
    {
        ClearPlacementPreview();
        UnitType type = UnitFactory.Get(buildingId);
        if (type == null) return;

        PackedScene scene = Assets.LoadScene(type.ModelPath);
        if (scene != null)
        {
            _placementGhost = scene.Instantiate<Node3D>();
            _placementGhost.Name = "placement_ghost";
            var material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 1f, 0.45f, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            foreach (Node node in _placementGhost.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (node is not MeshInstance3D mesh) continue;
                mesh.MaterialOverride = material;
                mesh.Transparency = 0.35f;
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            AddChild(_placementGhost);
        }

        _placementFootprintMesh = new ImmediateMesh();
        _placementFootprintNode = new MeshInstance3D
        {
            Name = "placement_footprint",
            Mesh = _placementFootprintMesh,
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_placementFootprintNode);
    }

    private void ClearPlacementPreview()
    {
        _placementGhost?.QueueFree();
        _placementGhost = null;
        _placementFootprintNode?.QueueFree();
        _placementFootprintNode = null;
        _placementFootprintMesh = null;
        _placementPreviewValid = false;
    }

    private void UpdatePlacementPreview()
    {
        if (_placingId == null || _placementGhost == null || _placementFootprintNode == null) return;
        UnitType type = UnitFactory.Get(_placingId);
        if (type == null) return;

        Vector2 mouse = GetViewport().GetMousePosition();
        if (!TryGroundPoint(mouse, out Vector3 ground))
        {
            _placementGhost.Visible = false;
            _placementFootprintNode.Visible = false;
            return;
        }

        if (!IsMineConsumingType(type))
        {
            ground = new Vector3(Mathf.Round(ground.X / 32f) * 32f, ground.Y, Mathf.Round(ground.Z / 32f) * 32f);
        }
        ground.Y = MapWorldBuilder.HeightAt(_map.Terrain, ground.X, ground.Z);
        _placementGhost.Visible = true;
        _placementGhost.Position = ground;
        _placementFootprintNode.Visible = true;
        RebuildFootprint(ground, type);
    }

    private void RebuildFootprint(Vector3 ground, UnitType type)
    {
        if (_placementFootprintMesh == null) return;

        int cells = Mathf.Max(1, type.FootprintCells);
        float cell = 32f;
        float half = cells * cell * 0.5f;
        bool allFree = true;
        float footprint = Mathf.Max(32f, type.FootprintRadius);
        bool tooClose = false;
        foreach (Unit other in _allUnits)
        {
            if (!other.Alive || !other.IsBuilding) continue;
            float distance = new Vector2(other.Position.X - ground.X, other.Position.Z - ground.Z).Length();
            if (distance < (footprint + other.Type.FootprintRadius) * 0.92f)
            {
                tooClose = true;
                break;
            }
        }
        if (tooClose) allFree = false;

        _placementFootprintMesh.ClearSurfaces();
        _placementFootprintMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int dz = 0; dz < cells; dz++)
        {
            for (int dx = 0; dx < cells; dx++)
            {
                float x0 = ground.X - half + dx * cell;
                float z0 = ground.Z - half + dz * cell;
                float x1 = x0 + cell;
                float z1 = z0 + cell;
                var center = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
                bool free = !tooClose && (_pathGrid == null || _pathGrid.IsAreaFree(center, cell * 0.5f));
                if (!free) allFree = false;
                Color color = free ? new Color(0.25f, 0.95f, 0.35f, 0.45f) : new Color(0.95f, 0.25f, 0.2f, 0.5f);
                float y = MapWorldBuilder.HeightAt(_map.Terrain, center.X, center.Z) + 2f;

                AddFootprintVertex(x0, y, z0, color);
                AddFootprintVertex(x1, y, z0, color);
                AddFootprintVertex(x1, y, z1, color);
                AddFootprintVertex(x0, y, z0, color);
                AddFootprintVertex(x1, y, z1, color);
                AddFootprintVertex(x0, y, z1, color);
            }
        }
        _placementFootprintMesh.SurfaceEnd();
        _placementPreviewValid = allFree;
    }

    private void AddFootprintVertex(float x, float y, float z, Color color)
    {
        _placementFootprintMesh.SurfaceSetColor(color);
        _placementFootprintMesh.SurfaceAddVertex(new Vector3(x, y, z));
    }

    private void TryPlace(Vector2 screenPosition)
    {
        if (!TryGroundPoint(screenPosition, out Vector3 ground))
        {
            _hud?.SetHint("地面位置无效");
            return;
        }
        Unit placed = PlaceBuildingAt(Slot(ViewPlayer), _placingId, ground, true);
        if (placed != null || _placingId == null) ClearPlacementPreview();
    }

    private void UpdateQueueLabel()
    {
        if (_hud == null) return;

        var entries = new List<QueueEntry>();
        BuildingOrder first = null;
        foreach (BuildingOrder order in _orders)
        {
            if (order.Player != ViewPlayer) continue;
            if (first == null) first = order;
            if (entries.Count >= 5) continue;

            entries.Add(new QueueEntry
            {
                Icon = QueueIconOf(order),
                Name = QueueNameOf(order),
                Remaining = order.Remaining,
                Total = order.Total,
            });
        }
        _hud.SetQueueSlots(entries);

        if (first == null)
        {
            _hud.SetQueue("");
            return;
        }

        _hud.SetQueue($"{first.Building.Type.Name}：{QueueNameOf(first)} {Mathf.Max(0f, first.Remaining):0.0}s（{entries.Count}）");
    }

    private static float XpRatioOf(Unit unit)
    {
        int need = HeroRules.XpToNext(unit.Level);
        return need <= 0 ? 0f : Mathf.Clamp(unit.Xp / need, 0f, 1f);
    }

    private static string RaceLabelOf(Unit unit) =>
        unit.Type == null ? null : (unit.Type.IsBuilding ? "建筑" : "单位");

    private static string QueueNameOf(BuildingOrder order)
    {
        return order.Kind switch
        {
            OrderKind.Train => $"训练 {UnitFactory.Get(order.Id)?.Name}",
            OrderKind.Research => $"研究 {TechTree.Upgrade(order.Id)?.NameFor(order.Level)}",
            _ => $"升级 {UnitFactory.Get(order.Id)?.Name}",
        };
    }

    private static Texture2D QueueIconOf(BuildingOrder order)
    {
        if (order.Kind == OrderKind.Research) return TechTree.Icon(TechTree.Upgrade(order.Id)?.IconPath);
        UnitType type = UnitFactory.Get(order.Id);
        return type == null || string.IsNullOrEmpty(type.IconPath) ? null : Assets.LoadTexture(type.IconPath);
    }

    private void UpdateUnitSummary()
    {
        if (_hud == null || _selected.Count != 1)
        {
            _hud?.SetUnitSummary(null, "", "");
            _hud?.SetBuffs(null);
            _newHud?.SetUnitInfo(null);
            return;
        }

        Unit unit = _selected[0];
        UnitType type = unit.Type;
        Texture2D portrait = type != null && !string.IsNullOrEmpty(type.IconPath) ? Assets.LoadTexture(type.IconPath) : null;
        string attack = type != null && type.CanAttack ? $"{type.Weapon.MinDamage:0}-{type.Weapon.MaxDamage:0}" : "—";
        float armor = _world != null ? _world.ArmorOf(unit) : 0f;
        _hud.SetUnitSummary(portrait, attack, $"{armor:0.#}");

        if (_newHud != null)
        {
            bool hero = unit.IsHero;
            _newHud.SetUnitInfo(new UnitInfoView(
                Name: type?.Name ?? unit.Name ?? "",
                IsHero: hero,
                ClassLine: hero ? $"等级 {unit.Level} {type?.Name}" : null,
                TypeLine: !hero && !unit.IsBuilding ? RaceLabelOf(unit) : null,
                XpRatio: hero ? XpRatioOf(unit) : 0f,
                AttackText: attack,
                AttackLevel: hero ? unit.Level.ToString() : null,
                ArmorText: $"{armor:0.#}",
                ArmorLevel: hero ? unit.Level.ToString() : null,
                Strength: hero ? unit.Strength : null,
                Agility: hero ? unit.Agility : null,
                Intellect: hero ? unit.Intelligence : null));
        }

        var buffs = new List<BuffEntry>();
        foreach (Buff buff in unit.Buffs)
        {
            if (buff == null || buff.Remaining <= 0f) continue;
            string art = buff.SourceAbility?.Type?.IconArt;
            buffs.Add(new BuffEntry
            {
                Icon = string.IsNullOrEmpty(art) ? null : TechTree.Icon(art),
                Name = buff.Name,
                Remaining = buff.Remaining,
            });
            if (buffs.Count >= 8) break;
        }
        if (buffs.Count < 8)
        {
            foreach (Ability ability in unit.Abilities)
            {
                if (ability == null || !ability.Learned || !ability.IsAura) continue;
                buffs.Add(new BuffEntry
                {
                    Icon = TechTree.Icon(ability.IconArt),
                    Name = ability.Name,
                    Remaining = 0f,
                    Aura = true,
                });
                if (buffs.Count >= 8) break;
            }
        }
        _hud.SetBuffs(buffs);
    }
}
