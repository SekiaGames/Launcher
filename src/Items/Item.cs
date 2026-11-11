using Godot;

namespace Launcher;

/// <summary>
/// 物品实例。对标客户端 Src/Items/ItemBonus.cs 与 Units/Unit.Items.cs 的物品实体部分。
/// 地上物品 Owner 为空；被捡起后挂到 Unit.Inventory 的某个格子上。
/// </summary>
public sealed class Item
{
    public ItemType Type;
    public Vector3 Position;
    public Unit Owner;
    public int Slot = -1;
    public Node3D Node;
    /// <summary>已经加到持有者身上的加成，丢弃时按这份扣除。</summary>
    public ItemBonus Bonus;

    public bool OnGround => Owner == null;

    public void SetOnGround(Vector3 position)
    {
        Owner = null;
        Slot = -1;
        Position = position;
        if (Node != null)
        {
            Node.Visible = true;
            Node.Position = position;
        }
    }

    public void AttachTo(Unit owner, int slot)
    {
        Owner = owner;
        Slot = slot;
        if (Node != null) Node.Visible = false;
    }
}
