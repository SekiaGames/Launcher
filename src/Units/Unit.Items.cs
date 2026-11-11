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

    // —— 商店库存。对标 ItemData 的 stockMax / stockRegen / stockStart：
    //    开局按 stockMax 铺满；每件商品各自按自己的 stockRegen 补货（不是整店一个节奏）；
    //    stockStart 是「开店后多久才开始补货」的初始延迟。 ——

    public readonly Dictionary<string, int> ShopStock = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>每件商品各自累积的补货秒数。</summary>
    public readonly Dictionary<string, float> ShopRestockTimers = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>开店至今的秒数，用来判 stockStart。</summary>
    public float ShopAge;

    public bool IsShop => Type != null && Type.IsShop;

    /// <summary>店里最短的补货周期（只用于显示与探针；实际按每件商品自己的节奏走）。</summary>
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
        ShopRestockTimers.Clear();
        ShopAge = 0f;
        if (!IsShop) return;

        foreach (string id in Type.SellItems)
        {
            ItemType type = ItemCatalog.Get(id);
            if (type == null) continue;
            ShopStock[id] = Mathf.Max(0, type.StockMax);
            ShopRestockTimers[id] = 0f;
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

    /// <summary>卖回：不超过上限就 +1。</summary>
    public void AddStock(string id)
    {
        int max = StockMaxOf(id);
        if (max <= 0) return;
        ShopStock[id] = Mathf.Min(max, StockOf(id) + 1);
    }

    /// <summary>
    /// 推进补货：每件商品单独计时，到自己的 stockRegen 且已过 stockStart 就补一件。
    /// 返回这次补上的商品种类数（0 表示什么都没补）。
    /// </summary>
    public int TickShop(float delta)
    {
        if (!IsShop) return 0;

        ShopAge += delta;
        int restocked = 0;

        foreach (string id in Type.SellItems)
        {
            ItemType type = ItemCatalog.Get(id);
            if (type == null || type.StockMax <= 0) continue;
            if (StockOf(id) >= type.StockMax) continue;
            if (ShopAge < type.StockStart) continue;

            float interval = Mathf.Max(5f, type.StockRegen);
            float elapsed = (ShopRestockTimers.TryGetValue(id, out float value) ? value : 0f) + delta;
            if (elapsed < interval)
            {
                ShopRestockTimers[id] = elapsed;
                continue;
            }

            ShopRestockTimers[id] = elapsed - interval;
            ShopStock[id] = StockOf(id) + 1;
            restocked++;
        }

        return restocked;
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
