using Godot;
using System;
using System.Collections.Generic;

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
