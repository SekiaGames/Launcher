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
    public bool HasStock => Type != null && Type.HasStock;

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

    // —— 单位库存（UnitFunc.Sellunits）：酒馆卖英雄、雇佣兵营地/地精实验室卖兵。 ——
    //    口径同客户端 AddConfiguredStock/StepStocks/ConsumeStock：
    //    开局按 stockStart 决定有没有货；stockRegen=0 表示不限量（酒馆英雄买不完）。

    public readonly Dictionary<string, int> UnitStock = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>每件单位离下次进货还剩的秒数（开局这段就是 stockStart）。</summary>
    public readonly Dictionary<string, float> UnitStockTimers = new(StringComparer.OrdinalIgnoreCase);

    public bool SellsUnits => Type != null && Type.SellUnits.Length > 0;

    public int UnitStockOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        return UnitStock.TryGetValue(id, out int count) ? count : 0;
    }

    public int UnitStockMaxOf(string id)
    {
        UnitType type = UnitFactory.Get(id);
        return type != null ? Mathf.Max(1, type.StockMax) : 0;
    }

    /// <summary>开店：stockStart<=0 直接满仓，否则先空着等 stockStart 秒。</summary>
    public void InitUnitStock()
    {
        UnitStock.Clear();
        UnitStockTimers.Clear();
        if (!SellsUnits) return;

        foreach (string id in Type.SellUnits)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null) continue;

            int max = Mathf.Max(1, type.StockMax);
            if (type.StockStart <= 0f)
            {
                UnitStock[id] = max;
                UnitStockTimers[id] = 0f;
                continue;
            }
            UnitStock[id] = 0;
            UnitStockTimers[id] = type.StockStart;
        }
    }

    /// <summary>买走一个；没货返回 false。stockRegen=0 的建筑不扣库存（酒馆英雄随时可买）。</summary>
    public bool TakeUnitStock(string id)
    {
        int count = UnitStockOf(id);
        if (count <= 0) return false;

        UnitType type = UnitFactory.Get(id);
        if (type == null) return false;
        if (type.StockRegen <= 0f) return true;

        UnitStock[id] = count - 1;
        if (!UnitStockTimers.ContainsKey(id) || UnitStockTimers[id] <= 0f)
        {
            UnitStockTimers[id] = Mathf.Max(1f, type.StockRegen);
        }
        return true;
    }

    /// <summary>推进进货：到点补一件；stockRegen=0 的建筑到点直接补满。返回这次补上的件数。</summary>
    public int TickUnitStock(float delta)
    {
        if (!SellsUnits || delta <= 0f) return 0;

        int restocked = 0;
        foreach (string id in Type.SellUnits)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null) continue;

            int max = Mathf.Max(1, type.StockMax);
            int current = UnitStockOf(id);
            if (current >= max)
            {
                UnitStockTimers[id] = 0f;
                continue;
            }

            float timer = (UnitStockTimers.TryGetValue(id, out float value) ? value : 0f) - delta;
            if (timer > 0f)
            {
                UnitStockTimers[id] = timer;
                continue;
            }

            if (type.StockRegen <= 0f)
            {
                restocked += max - current;
                current = max;
                timer = 0f;
            }
            else
            {
                float regen = Mathf.Max(1f, type.StockRegen);
                while (timer <= 0f && current < max)
                {
                    current++;
                    restocked++;
                    timer += regen;
                }
            }

            UnitStock[id] = current;
            UnitStockTimers[id] = current >= max ? 0f : timer;
        }
        return restocked;
    }

    public void InitShopStock()
    {
        ShopStock.Clear();
        ShopRestockTimers.Clear();
        ShopAge = 0f;
        InitUnitStock();
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

    /// <summary>买走一件；没货返回 false。stockRegen<=0 表示不消耗库存（客户端 StockAndShops.cs:268-284）。</summary>
    public bool TakeStock(string id)
    {
        ItemType type = ItemCatalog.Get(id);
        if (type != null && type.StockRegen <= 0f && type.StockMax > 0) return true;
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
        if (!HasStock) return 0;

        ShopAge += delta;
        int restocked = TickUnitStock(delta);

        foreach (string id in Type.SellItems)
        {
            ItemType type = ItemCatalog.Get(id);
            if (type == null || type.StockMax <= 0) continue;
            if (StockOf(id) >= type.StockMax) continue;
            if (ShopAge < type.StockStart) continue;
            // 客户端口径：stockRegen<=0 表示不消耗库存（无限量），不需要补货。
            if (type.StockRegen <= 0f) continue;

            float interval = type.StockRegen;
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
