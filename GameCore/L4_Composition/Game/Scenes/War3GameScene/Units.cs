using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private Unit FindHall(int player)
    {
        Unit fallback = null;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != player || !unit.IsBuilding) continue;
            if (unit.Type.FoodMade > 0 && unit.Type.Trains.Length > 0) return unit;
            if (fallback == null && unit.Type.Trains.Length > 0) fallback = unit;
        }
        return fallback;
    }

    private void SpawnUnits()
    {
        int placed = 0;
        int missingType = 0;
        int missingScene = 0;
        int lootCount = 0;
        int tableLoot = 0;

        if (_map.Units != null)
        {
            foreach (UnitPlacement entry in _map.Units.Units)
            {
                if (entry.IsStartingLocation) continue;

                Vector3 world = ToWorld(entry.X, entry.Y);

                if (entry.IsItem || ItemCatalog.Exists(entry.Id))
                {
                    if (SpawnItem(entry.Id, world) != null) placed++;
                    else missingScene++;
                    continue;
                }

                var player = (int)Mathf.Clamp(entry.Player, 0, 15);
                Unit unit = Spawn(entry.Id, player, world.X, world.Z, Mathf.Pi + entry.Angle,
                    entry.Hitpoints > 0 ? entry.Hitpoints : 0f, entry.ScaleX, entry.Gold);

                if (unit != null)
                {
                    placed++;
                    foreach (UnitItemDropSet set in entry.DroppedItemSets) unit.DropSets.Add(set);
                    if (AttachDropTable(unit, entry.DroppedItemTable)) tableLoot++;
                    if (unit.DropSets.Count > 0) lootCount++;
                    ApplyPlacementInventory(unit, entry);
                }
                else if (UnitFactory.Get(entry.Id) == null) missingType++;
                else missingScene++;
            }
        }

        int cleared = ClearExcessUnits();
        int melee = SpawnMeleeStarts();

        GD.Print($"[INIT] 地图单位 {placed} 个（缺类型 {missingType} 缺模型 {missingScene}，带掉落集 {lootCount}，"
            + $"引用物品表 {tableLoot}，表 {_map.Info?.ItemTables.Count ?? 0} 张），"
            + $"出生点清场移除 {cleared} 个，开局基地单位 {melee} 个");
    }

    /// <summary>doo 的 DroppedItemTable 是 w3i 随机物品表的下标，按组展开成和实例掉落集同一种结构。</summary>
    private bool AttachDropTable(Unit unit, int tableIndex)
    {
        if (tableIndex < 0) return false;
        List<W3iItemTable> tables = _map.Info?.ItemTables;
        if (tables == null || tableIndex >= tables.Count) return false;

        W3iItemTable table = tables[tableIndex];
        foreach (List<W3iItemDrop> group in table.Sets)
        {
            var dropSet = new UnitItemDropSet();
            foreach (W3iItemDrop drop in group) dropSet.Items.Add(new UnitItemDrop(drop.ItemId, drop.Chance));
            if (dropSet.Items.Count > 0) unit.DropSets.Add(dropSet);
        }
        return table.Sets.Count > 0;
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
            Unit unit = _allUnits[index];
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
    private Unit FindNearestGoldMine(Vector2 startMap)
    {
        Unit best = null;
        float bestDistance = MeleeRules.MineSearchRadius;
        foreach (Unit unit in _allUnits)
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
            Unit mine = FindNearestGoldMine(start);
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
                Unit unit = Spawn(entry.Id, player.Id, world.X, world.Z, 0f, 0f, 1f);
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

    private void ApplyStartMineEffect(W3iPlayer player, Unit mine, MeleeRules.StartPlan plan)
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

        Unit replacement = Spawn(replacementId, player.Id, position.X, position.Z, yaw, 0f, 1f, gold);
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

    /// <summary>地图上单位自带的背包物品（war3mapUnits.doo 的 Inventory）。</summary>
    private void ApplyPlacementInventory(Unit unit, UnitPlacement entry)
    {
        if (entry.Inventory.Count == 0 || !unit.CanCarryItems) return;

        foreach (UnitInventoryItem placed in entry.Inventory)
        {
            ItemType type = ItemCatalog.Get(placed.ItemId);
            if (type == null) continue;
            var item = new Item { Type = type };
            if (placed.Slot >= 0 && placed.Slot < Unit.InventorySize && unit.Inventory[placed.Slot] == null)
            {
                unit.Inventory[placed.Slot] = item;
                item.AttachTo(unit, placed.Slot);
            }
            else
            {
                unit.AddItem(item);
            }
            _world?.ApplyItemBonus(unit, item);
        }
    }

    /// <summary>在地图上放一件物品：建模型节点后交给 World 管理拾取。</summary>
    private Item SpawnItem(string id, Vector3 position)
    {
        ItemType type = ItemCatalog.Get(id);
        if (type == null) return null;

        var item = new Item { Type = type, Position = position };
        CreateItemNode(item);
        _world.DropItem(item, position);
        return item;
    }

    /// <summary>给新落地的物品建模型节点（掉落与手动摆放共用）。</summary>
    private void CreateItemNode(Item item)
    {
        if (item?.Type == null || item.Node != null) return;

        PackedScene scene = Assets.LoadScene(item.Type.ModelPath);
        if (scene == null) return;

        var node = scene.Instantiate<Node3D>();
        node.Position = item.Position;
        node.Scale = Vector3.One * item.Type.Scale;
        node.Name = $"item_{item.Type.Id}_{_itemSerial++}";
        AddChild(node);
        item.Node = node;
    }

    private void OnItemSpawned(Item item) => CreateItemNode(item);

    private void OnItemPickedUp(Unit unit, Item item)
    {
        if (_loadingStage != LoadingStage.Complete) return;
        PlayUiSound("ItemGet");
    }

    private void OnItemDropped(Unit unit, Item item)
    {
        if (_loadingStage != LoadingStage.Complete) return;
        PlayUiSound("ItemDrop");
    }

    private int _itemSerial;

    private Unit Spawn(string id, int player, float worldX, float worldZ, float yaw, float hp, float entryScale, int gold = 0)
    {
        UnitType type = UnitFactory.Get(id);
        if (type == null) return null;

        PackedScene scene = Assets.LoadScene(type.ModelPath);
        if (scene == null)
        {
            GD.PushWarning($"[GAME] 缺模型 {id} -> {type.ModelPath}");
            return null;
        }

        float worldY = _world != null
            ? _world.SurfaceY(type, worldX, worldZ)
            : MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ);
        float scale = type.ModelScale * (entryScale > 0f ? entryScale : 1f);

        UnitSceneComponent view = UnitSceneComponent.Create(scene, player, scale, type.IsBuilding, type.FootprintRadius, type.ModelPath, _sceneLighting?.Units ?? SceneLight.Fallback);
        view.Root.Position = new Vector3(worldX, worldY, worldZ);
        view.Root.Rotation = new Vector3(0f, yaw, 0f);
        view.Root.Name = $"{id}_{_allUnits.Count}";
        AddChild(view.Root);

        float maxHp = type.Hp;
        if (hp > 0f) maxHp = hp <= 100f ? type.Hp * hp / 100f : hp;

        var unit = new Unit
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
            IsHero = type.IsHero,
            Level = 1,
            Strength = type.Strength,
            Agility = type.Agility,
            Intelligence = type.Intelligence,
            AutoAcquire = Aggression && (player <= 3 || player == 12),
            AutoCast = player != ViewPlayer,
            LeashRange = player is >= 1 and <= 3 ? 1e9f : 260f,
            View = view,
        };

        view.Owner = unit;
        unit.BaseMaxHp = maxHp;

        if (type.IsHero)
        {
            // 英雄生命/法力由属性推导：生命 = 基础 + 力量*25，法力 = 智力*15；1 级自带 1 点技能点
            unit.MaxHp = HeroRules.MaxHpOf(type, unit.Strength);
            unit.Hp = unit.MaxHp;
            unit.MaxMana = HeroRules.MaxManaOf(unit.Intelligence);
            unit.Mana = unit.MaxMana;
            unit.SkillPoints = 1;
        }
        else if (type.ManaMax > 0f)
        {
            unit.BaseMaxMana = type.ManaMax;
            unit.MaxMana = type.ManaMax;
            unit.Mana = Mathf.Min(type.ManaMax, type.ManaStart);
        }

        // 自带技能（abilList）直接 1 级；英雄技能（heroAbilList）从 0 级开始等加点
        foreach (string abilityId in type.Abilities) unit.GiveAbility(abilityId, 1);
        foreach (string abilityId in type.HeroAbilList) unit.GiveAbility(abilityId, 0);
        if (type.HasStock) unit.InitShopStock();

        _world?.RefreshUpgradeStats(unit);

        view.Play("Stand");
        if (type.CanSleep && player == MeleeRules.PlayerNeutralHostile)
        {
            unit.IsCreepSleeper = true;
            unit.State.Add(StateMod.Sleep);
            view.Play("Sleep");
        }
        _allUnits.Add(unit);
        _world?.RecordHeroAcquisition(unit);
        return unit;
    }

    private int _morphSerial;

    /// <summary>物编写 hpxe 与 hphx 共用 phoenix 模型，蛋形态用导出时单独拆出来的 RuntimePhoenixEggBuff 场景。</summary>
    private static string ModelSceneOf(UnitType type)
    {
        if (type == null) return null;
        if (string.Equals(type.Id, "hpxe", StringComparison.OrdinalIgnoreCase))
        {
            string egg = PathResolve.ModelScene("Units/Human/Phoenix/RuntimePhoenixEggBuff");
            if (egg != null) return egg;
        }
        return type.ModelPath;
    }

    /// <summary>
    /// 变形后重建模型：旧节点丢掉，按新类型重新实例化，位置/朝向/选中状态/血条都接上。
    /// 模型缺失时保持原样（只是外形不对，不至于把单位弄没）。
    /// </summary>
    private void OnUnitMorphed(Unit unit)
    {
        if (unit?.View == null || unit.Type == null) return;

        PackedScene scene = Assets.LoadScene(ModelSceneOf(unit.Type));
        if (scene == null)
        {
            GD.PushWarning($"[MORPH] 缺模型 {unit.Type.Id} -> {unit.Type.ModelPath}");
            return;
        }

        Vector3 position = unit.View.Root.Position;
        float yaw = unit.View.Root.Rotation.Y;
        bool selected = _selected.Contains(unit);

        unit.View.Root.QueueFree();

        UnitSceneComponent view = UnitSceneComponent.Create(scene, unit.Player,
            unit.Type.ModelScale, unit.Type.IsBuilding, unit.Type.FootprintRadius, unit.Type.ModelPath,
            _sceneLighting?.Units ?? SceneLight.Fallback);
        view.Root.Position = position;
        view.Root.Rotation = new Vector3(0f, yaw, 0f);
        view.Root.Name = $"morph_{unit.Type.Id}_{_morphSerial++}";
        AddChild(view.Root);

        view.Owner = unit;
        unit.View = view;
        view.SetSelected(selected);
        view.Play(unit.Alive ? "Stand" : "Death");

        string puff = @"Abilities\Spells\Human\Polymorph\PolyMorphDoneGround.mdl";
        if (PathResolve.ModelScene(puff) != null)
            _world?.AddSpecialEffect(puff, new Vec2(position.X, position.Z), lifeSpan: 1.2f);

        GD.Print($"[MORPH] {unit.Type.Id} 重建模型 {(selected ? "（选中中）" : "")}"
            + $" 生命 {unit.Hp:0}/{unit.MaxHp:0}"
            + (unit.Morphed ? $" 剩余变形 {unit.MorphRemaining:0.0}s" : " 已还原"));
    }

    /// <summary>驻军/载具里的单位不能再被选中：进了地洞就从选中列表里摘掉。</summary>
    private void PruneCargoedSelection()
    {
        if (_selected.Count == 0) return;
        for (int index = _selected.Count - 1; index >= 0; index--)
        {
            Unit unit = _selected[index];
            if (unit == null) _selected.RemoveAt(index);
            else if (unit.IsCargoed) DropFromSelection(unit);
        }
    }

    /// <summary>骑乘合成 / 分离把单位从世界上拿掉：模型与选中列表同步清掉，避免残留空节点。</summary>
    private void OnUnitRemoved(Unit unit)
    {
        if (unit == null) return;

        DropFromSelection(unit);
        _allUnits.Remove(unit);
        unit.View?.Root.QueueFree();
        unit.View = null;

        GD.Print($"[UNIT] {unit.Type?.Id ?? "?"} 已离场（骑乘/分离）");
    }
}
