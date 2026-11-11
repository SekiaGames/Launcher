using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

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

        if (_map.Units != null)
        {
            foreach (UnitPlacement entry in _map.Units.Units)
            {
                if (entry.IsStartingLocation) continue;

                Vector3 world = ToWorld(entry.X, entry.Y);

                if (ItemCatalog.Exists(entry.Id))
                {
                    if (SpawnItem(entry.Id, world) != null) placed++;
                    else missingScene++;
                    continue;
                }

                var player = (int)Mathf.Clamp(entry.Player, 0, 15);
                Unit unit = Spawn(entry.Id, player, world.X, world.Z, Mathf.Pi + entry.Angle,
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

    /// <summary>在地图上放一件物品：建模型节点后交给 World 管理拾取。</summary>
    private Item SpawnItem(string id, Vector3 position)
    {
        ItemType type = ItemCatalog.Get(id);
        if (type == null) return null;

        var item = new Item { Type = type, Position = position };

        PackedScene scene = Assets.LoadScene(type.ModelPath);
        if (scene != null)
        {
            var node = scene.Instantiate<Node3D>();
            node.Position = position;
            node.Scale = Vector3.One * type.Scale;
            node.Name = $"item_{id}_{_itemSerial++}";
            AddChild(node);
            item.Node = node;
        }

        _world.DropItem(item, position);
        return item;
    }

    private int _itemSerial;

    private Unit Spawn(string id, int player, float worldX, float worldZ, float yaw, float hp, float entryScale, int gold = 0)
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

        UnitSceneComponent view = UnitSceneComponent.Create(scene, UnitSceneComponent.ColorOf(player), scale, type.IsBuilding, type.FootprintRadius);
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
            LeashRange = player is >= 1 and <= 3 ? 1e9f : 260f,
            View = view,
        };

        if (type.IsHero)
        {
            // 英雄生命/法力由属性推导：生命 = 基础 + 力量*25，法力 = 智力*15
            unit.MaxHp = HeroRules.MaxHpOf(type, unit.Strength);
            unit.Hp = unit.MaxHp;
            unit.MaxMana = HeroRules.MaxManaOf(unit.Intelligence);
            unit.Mana = unit.MaxMana;
        }

        if (_world != null && type.IsBuilding)
        {
            float multiplier = _world.HpMultiplier(unit, player);
            unit.MaxHp = maxHp * multiplier;
            unit.Hp = unit.MaxHp;
        }

        view.Play("Stand");
        view.SetHp(1f, true);
        _allUnits.Add(unit);
        return unit;
    }
}
