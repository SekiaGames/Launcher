using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

/// <summary>
/// 地面物品与拾取。对标客户端 War3GameScene/AutoPickup.cs：
/// 英雄走到物品附近（PickupRange）就自动捡起，没有空格则留在原地。
/// </summary>
public sealed partial class World
{
    private const float PickupRange = 150f;

    private readonly List<Item> _groundItems = new();

    public IReadOnlyList<Item> GroundItems => _groundItems;

    public event Action<Unit, Item> ItemPickedUp;
    /// <summary>新物品落地（野怪掉落 / 脚本生成），场景层据此建模型。</summary>
    public event Action<Item> ItemSpawned;

    /// <summary>落地一件物品；场景层负责先建好 Item.Node 再交给这里。</summary>
    public void DropItem(Item item, Vector3 position)
    {
        if (item == null) return;
        item.SetOnGround(position);
        if (!_groundItems.Contains(item)) _groundItems.Add(item);
    }

    public void TickItemPickup()
    {
        if (_groundItems.Count == 0) return;

        for (int index = _groundItems.Count - 1; index >= 0; index--)
        {
            Item item = _groundItems[index];
            Unit picker = NearestPicker(item.Position);
            if (picker == null) continue;

            if (!picker.AddItem(item)) continue;

            _groundItems.RemoveAt(index);
            ItemPickedUp?.Invoke(picker, item);
            if (_logItemBudget > 0)
            {
                _logItemBudget--;
                GD.Print($"[ITEM] {picker.Type.Name} 捡起 {item.Type.Name}（{picker.ItemCount}/{Unit.InventorySize}）");
            }
        }
    }

    private int _logItemBudget = 8;

    /// <summary>
    /// 死亡掉落：对每个掉落集逐件按 chance(%) 掷骰，每个集最多掉一件。
    /// 数据来自 war3mapUnits.doo 的单位实例掉落集（UnitPlacement.DroppedItemSets）。
    /// </summary>
    public void DropLootOnDeath(Unit victim)
    {
        if (victim == null || victim.DropSets.Count == 0) return;

        foreach (UnitItemDropSet set in victim.DropSets)
        {
            foreach (UnitItemDrop drop in set.Items)
            {
                if (drop.Chance > 0 && _random.Next(1, 101) > drop.Chance) continue;

                ItemType type = ItemCatalog.Get(drop.ItemId);
                if (type == null) continue;

                float angle = (float)(_random.NextDouble() * Mathf.Tau);
                float radius = Mathf.Max(40f, victim.Radius + 24f);
                var position = new Vector3(
                    victim.Position.X + Mathf.Cos(angle) * radius,
                    victim.Position.Y,
                    victim.Position.Z + Mathf.Sin(angle) * radius);

                var item = new Item { Type = type, Position = position };
                DropItem(item, position);
                ItemSpawned?.Invoke(item);
                if (_logItemBudget > 0)
                {
                    _logItemBudget--;
                    GD.Print($"[ITEM] {victim.Type.Name} 掉落 {type.Name}({drop.ItemId}) chance={drop.Chance}%");
                }
                break;
            }
        }
    }

    private Unit NearestPicker(Vector3 position)
    {
        Unit best = null;
        float bestDistance = PickupRange;
        foreach (Unit unit in Units)
        {
            if (!unit.Alive || !unit.CanCarryItems) continue;
            if (!unit.HasFreeSlot) continue;

            float dx = unit.Position.X - position.X;
            float dz = unit.Position.Z - position.Z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = unit;
        }
        return best;
    }
}
