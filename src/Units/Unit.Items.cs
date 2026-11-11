using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 单位物品栏。对标客户端 Units/Unit.Items.cs：英雄 6 格，非英雄不能带。
/// </summary>
public sealed partial class Unit
{
    public const int InventorySize = 6;

    public readonly Item[] Inventory = new Item[InventorySize];

    public bool CanCarryItems => IsHero;

    public bool HasFreeSlot
    {
        get
        {
            for (int slot = 0; slot < Inventory.Length; slot++)
            {
                if (Inventory[slot] == null) return true;
            }
            return false;
        }
    }

    public int FreeSlot
    {
        get
        {
            for (int slot = 0; slot < Inventory.Length; slot++)
            {
                if (Inventory[slot] == null) return slot;
            }
            return -1;
        }
    }

    public int ItemCount
    {
        get
        {
            int count = 0;
            for (int slot = 0; slot < Inventory.Length; slot++)
            {
                if (Inventory[slot] != null) count++;
            }
            return count;
        }
    }

    /// <summary>放进第一个空格；返回是否成功。</summary>
    public bool AddItem(Item item)
    {
        if (item == null || !CanCarryItems) return false;
        int slot = FreeSlot;
        if (slot < 0) return false;

        Inventory[slot] = item;
        item.AttachTo(this, slot);
        return true;
    }

    // —— 商店库存。对标 ItemData 的 stockMax/stockRegen/stockStart：
    //    每件商品按上限铺货，卖光后按 stockRegen 秒补一件。库存挂在商店单位上。 ——

    public readonly Dictionary<string, int> ShopStock = new(StringComparer.OrdinalIgnoreCase);
    public float ShopRestockTimer;

    public bool IsShop => Type != null && Type.IsShop;

    /// <summary>补一件货要多少秒：取店里所有商品里最短的那个 stockRegen。</summary>
    public float RestockInterval
    {
        get
        {
            if (!IsShop) return 0f;
            float interval = float.MaxValue;
            foreach (string id in Type.SellItems)
            {
                ItemType type = ItemCatalog.Get(id);
                if (type == null || type.StockMax <= 0) continue;
                interval = Mathf.Min(interval, type.StockRegen);
            }
            return interval == float.MaxValue ? 0f : Mathf.Max(5f, interval);
        }
    }

    public void InitShopStock()
    {
        ShopStock.Clear();
        ShopRestockTimer = 0f;
        if (!IsShop) return;

        foreach (string id in Type.SellItems)
        {
            ItemType type = ItemCatalog.Get(id);
            if (type == null) continue;
            ShopStock[id] = Mathf.Max(0, type.StockMax);
        }
    }

    public int StockOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        return ShopStock.TryGetValue(id, out int count) ? count : 0;
    }

    public int StockMaxOf(string id)
    {
        ItemType type = ItemCatalog.Get(id);
        return type != null ? Mathf.Max(0, type.StockMax) : 0;
    }

    /// <summary>买走一件；没货返回 false。</summary>
    public bool TakeStock(string id)
    {
        int count = StockOf(id);
        if (count <= 0) return false;
        ShopStock[id] = count - 1;
        return true;
    }

    /// <summary>卖回/补货：不超过上限就 +1。</summary>
    public void AddStock(string id)
    {
        int max = StockMaxOf(id);
        if (max <= 0) return;
        ShopStock[id] = Mathf.Min(max, StockOf(id) + 1);
    }

    /// <summary>每格补货周期到了：所有低于上限的商品各补一件。</summary>
    public void RestockOne()
    {
        if (!IsShop) return;
        foreach (string id in Type.SellItems)
        {
            int max = StockMaxOf(id);
            if (max <= 0) continue;
            if (StockOf(id) >= max) continue;
            ShopStock[id] = StockOf(id) + 1;
        }
    }

    public Item RemoveSlot(int slot)
    {
        if (slot < 0 || slot >= Inventory.Length) return null;
        Item item = Inventory[slot];
        if (item == null) return null;
        Inventory[slot] = null;
        item.Owner = null;
        item.Slot = -1;
        return item;
    }
}
