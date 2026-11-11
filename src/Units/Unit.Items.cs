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
