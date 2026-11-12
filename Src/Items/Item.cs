using Godot;

using OpenWC3.Game;
namespace OpenWC3;

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

    /// <summary>物品自己的技能冷却（沉默权杖 20s、逃脱匕首 30s 这类永久可用物品靠它限制节奏）。</summary>
    public float CooldownRemaining;

    /// <summary>已用掉的次数（物编 uses>0 的物品有次数，用完才销毁）；0 表示还没用过。</summary>
    public int Charges;

    /// <summary>这件物品授予的被动/光环技能（装备时给，卸下时收回）。</summary>
    public readonly System.Collections.Generic.List<string> GrantedAbilities = new();

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
