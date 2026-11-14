using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

/// <summary>拾取订单结果（客户端 JassGameNatives.PickupItemResult）。</summary>
public enum PickupItemResult
{
    None,
    Issued,
    InventoryFull,
}

/// <summary>
/// 地面物品与拾取。对标客户端 JassGameNatives.PlayerPickupItemOrder：
/// 玩家右键物品下发拾取订单，单位寻路走到 Misc.PickupItemRange 内再入包。
/// </summary>
public sealed partial class World
{
    private readonly List<Item> _groundItems = new();

    public IReadOnlyList<Item> GroundItems => _groundItems;

    public event Action<Unit, Item> ItemPickedUp;
    public event Action<Unit, Item> ItemDropped;
    /// <summary>新物品落地（野怪掉落 / 脚本生成），场景层据此建模型。</summary>
    public event Action<Item> ItemSpawned;
    /// <summary>物品被使用掉（药水/卷轴）。</summary>
    public event Action<Unit, Item> ItemUsed;

    /// <summary>
    /// 使用物品：按 <see cref="ItemUseRules"/> 的计划分流——
    /// 物品专用一次性效果 → 复用技能实现（带目标）→ 回血回蓝兜底。
    /// 目标型物品（逃脱匕首/沉默权杖/蛛网/保存权杖/偷蓝法杖）由场景层先收目标再传进来。
    /// </summary>
    public bool UseItem(Unit unit, Item item, Unit target = null, Vector3? point = null)
    {
        if (unit == null || item?.Type == null || !unit.Alive) return false;

        ItemUsePlan plan = ItemUseRules.PlanFor(item.Type);
        if (!plan.Usable) return false;
        if (item.CooldownRemaining > 0f) return false;
        if (plan.TargetMode == ItemTargetMode.Unit && (target == null || !target.Alive)) return false;

        Vector3 at = point ?? (target != null ? target.Position : unit.Position);
        bool used = plan.Kind switch
        {
            ItemUseKind.OneShot => ItemUseEffect.TryUse(this, unit, item, target, at),
            ItemUseKind.Ability => UseItemAbility(unit, plan, target, at),
            ItemUseKind.Consumable => UseItemConsumable(unit, item),
            _ => false,
        };
        if (!used) return false;

        item.CooldownRemaining = Mathf.Max(item.CooldownRemaining, plan.Cooldown);
        if (SpendOneCharge(item)) ConsumeItem(item);
        ItemUsed?.Invoke(unit, item);
        return true;
    }

    /// <summary>
    /// 复用技能实现的那一类物品（逃脱匕首→闪烁、沉默权杖→沉默、蛛网饰针→蛛网…）。
    /// 物品不带法力消耗也不看施法状态（被沉默也能用道具），冷却记在物品上而不是技能实例上。
    /// </summary>
    private bool UseItemAbility(Unit unit, ItemUsePlan plan, Unit target, Vector3 point)
    {
        Ability ability = RuntimeAbilityFactory.Create(plan.Type);
        if (ability == null || ability is PassiveAbility) return false;

        ability.Owner = unit;
        ability.Level = 1;

        if (!ability.CheckTarget(this, target, point)) return false;
        if (!ability.OnCast(this, target, point)) return false;

        AbilityCast?.Invoke(unit, ability, target);
        return true;
    }

    private static bool UseItemConsumable(Unit unit, Item item)
    {
        ItemConsumable effect = item.Type.Consumable;
        if (effect.IsEmpty) return false;

        // 原版口径：回血的药在满血时喝不了，回蓝的在满蓝时喝不了（双回的要有一项没满）
        bool needHeal = effect.Heal > 0f && unit.Hp < unit.MaxHp;
        bool needMana = effect.Mana > 0f && unit.MaxMana > 0f && unit.Mana < unit.MaxMana;
        if (!needHeal && !needMana) return false;

        if (needHeal) unit.Heal(effect.Heal);
        if (needMana) unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + effect.Mana);
        return true;
    }

    /// <summary>
    /// 扣一次使用次数（物编 `uses`）：扣到 0 且 perishable 的才销毁；
    /// `uses` 为空的永久可用物品（沉默权杖/逃脱匕首）不扣也不毁。返回 true 表示该销毁。
    /// </summary>
    private static bool SpendOneCharge(Item item)
    {
        int uses = item.Type.Uses;
        if (uses <= 0 && !item.Type.Perishable) return false;

        int remaining = item.Charges > 0 ? item.Charges - 1 : Mathf.Max(0, uses - 1);
        item.Charges = remaining;
        return item.Type.Perishable && remaining <= 0;
    }

    /// <summary>命令卡显示的剩余次数（uses 为空的物品没有次数概念）。</summary>
    public static int ChargesLeft(Item item)
    {
        if (item?.Type == null || item.Type.Uses <= 0) return 0;
        return item.Charges > 0 ? item.Charges : item.Type.Uses;
    }

    /// <summary>物品冷却：每 tick 递减（永久可用物品靠它限制节奏）。</summary>
    public void TickItemCooldowns()
    {
        foreach (Unit unit in Units)
        {
            if (unit?.Inventory == null) continue;
            foreach (Item item in unit.Inventory)
            {
                if (item == null || item.CooldownRemaining <= 0f) continue;
                item.CooldownRemaining = Mathf.Max(0f, item.CooldownRemaining - FrameSeconds);
            }
        }
    }

    /// <summary>金币/木材道具（金袋、木材堆）：直接进玩家资源，走的是采集同一套事件。</summary>
    public void GrantGold(int player, int amount)
    {
        if (amount <= 0) return;
        GoldDeposited?.Invoke(player, amount);
    }

    public void GrantLumber(int player, int amount)
    {
        if (amount <= 0) return;
        LumberDeposited?.Invoke(player, amount);
    }

    /// <summary>把物品从格子里拿掉并销毁模型（用掉/被吞）。</summary>
    private void ConsumeItem(Item item)
    {
        Unit owner = item.Owner;
        if (owner != null && item.Slot >= 0) owner.RemoveSlot(item.Slot);
        else item.SetOnGround(item.Position);

        if (item.Node != null)
        {
            item.Node.QueueFree();
            item.Node = null;
        }
    }

    /// <summary>丢弃格子里的物品：扣除加成、落在英雄旁边、重新可拾取。</summary>
    public bool DropItemFromSlot(Unit unit, int slot)
    {
        if (unit == null) return false;
        Item item = unit.RemoveSlot(slot);
        if (item == null) return false;

        RemoveItemBonus(unit, item);

        float angle = (float)(_random.NextDouble() * Mathf.Tau);
        var position = new Vector3(
            unit.Position.X + Mathf.Cos(angle) * 64f,
            unit.Position.Y,
            unit.Position.Z + Mathf.Sin(angle) * 64f);

        DropItem(item, position);
        if (item.Node == null) ItemSpawned?.Invoke(item);
        ItemDropped?.Invoke(unit, item);
        return true;
    }

    /// <summary>格子之间换位（客户端 MoveItemSlot）：目标格里的东西不能丢就不许换。</summary>
    public bool MoveInventorySlot(Unit unit, int fromSlot, int toSlot)
    {
        if (unit == null || fromSlot < 0 || toSlot < 0) return false;
        if (fromSlot >= Unit.InventorySize || toSlot >= Unit.InventorySize) return false;
        if (fromSlot == toSlot) return true;

        Item moving = unit.Inventory[fromSlot];
        if (moving == null) return false;
        Item displaced = unit.Inventory[toSlot];
        if (displaced != null && displaced.Type != null && !displaced.Type.Droppable) return false;

        unit.Inventory[fromSlot] = displaced;
        unit.Inventory[toSlot] = moving;
        moving.Slot = toSlot;
        if (displaced != null) displaced.Slot = fromSlot;
        return true;
    }

    /// <summary>丢到指定地面点（客户端 DropInventorySlotAt）；不可丢弃的物品返回 false。</summary>
    public bool DropItemFromSlotAt(Unit unit, int slot, Vector3 position)
    {
        if (unit == null) return false;
        Item item = unit.Inventory != null && slot >= 0 && slot < unit.Inventory.Length
            ? unit.Inventory[slot] : null;
        if (item?.Type == null || !item.Type.Droppable) return false;

        item = unit.RemoveSlot(slot);
        if (item == null) return false;

        RemoveItemBonus(unit, item);
        DropItem(item, position);
        if (item.Node == null) ItemSpawned?.Invoke(item);
        ItemDropped?.Invoke(unit, item);
        return true;
    }

    /// <summary>落地一件物品；场景层负责先建好 Item.Node 再交给这里。</summary>
    public void DropItem(Item item, Vector3 position)
    {
        if (item == null) return;
        item.SetOnGround(position);
        if (!_groundItems.Contains(item)) _groundItems.Add(item);
    }

    /// <summary>商店补货：每个补货周期给所有没满的商品各 +1。</summary>
    public void TickShops()
    {
        foreach (Unit shop in Units)
        {
            if (!shop.Alive || !shop.HasStock) continue;
            int restocked = shop.TickShop(FrameSeconds);
            if (restocked > 0 && _shopStockLogBudget > 0)
            {
                _shopStockLogBudget--;
                GD.Print($"[SHOP] {shop.Type.Name} 补货 {restocked} 件（开店 {shop.ShopAge:0}s）");
            }
        }
    }

    private int _shopStockLogBudget = 6;

    /// <summary>
    /// 把物品卖给商店：按原版对战口径给**半价**（`goldcost / 2`，至少 1 金），
    /// 物品从背包消失、加成撤回、库存回收一件。返回卖出所得（0 表示卖不掉）。
    /// </summary>
    public int SellItem(Unit shop, Item item)
    {
        if (shop?.Type == null || item?.Type == null) return 0;
        if (!shop.IsShop) return 0;

        Unit owner = item.Owner;
        if (owner == null || item.Slot < 0) return 0;

        int price = Mathf.Max(1, item.Type.GoldCost / 2);
        RemoveItemBonus(owner, item);
        owner.RemoveSlot(item.Slot);
        if (item.Node != null)
        {
            item.Node.QueueFree();
            item.Node = null;
        }

        shop.AddStock(item.Type.Id);
        return price;
    }

    /// <summary>把物品转交另一个单位（英雄给英雄）；目标没空格就原样退回。</summary>
    public bool GiveItem(Unit from, int slot, Unit to)
    {
        if (from == null || to == null || ReferenceEquals(from, to)) return false;
        if (!to.CanCarryItems || !to.HasFreeSlot) return false;

        Item item = from.RemoveSlot(slot);
        if (item == null) return false;

        RemoveItemBonus(from, item);
        if (!to.AddItem(item))
        {
            from.AddItem(item);
            ApplyItemBonus(from, item);
            return false;
        }

        ApplyItemBonus(to, item);
        return true;
    }

    /// <summary>玩家右键地面物品：能收下就下拾取订单（客户端 IssuePickupItem）。</summary>
    public PickupItemResult IssuePickupItem(Unit unit, Item item)
    {
        if (unit?.Type == null || item?.Type == null) return PickupItemResult.None;
        if (!unit.Alive || unit.IsBuilding || !unit.CanCarryItems) return PickupItemResult.None;
        if (item.Owner != null || !_groundItems.Contains(item)) return PickupItemResult.None;
        if (!unit.HasFreeSlot) return PickupItemResult.InventoryFull;

        IssueOrder(unit, UnitOrder.Pickup(item), false);
        return PickupItemResult.Issued;
    }

    private void StepPickupItem(Unit unit)
    {
        Item item = unit.PickupTarget;
        if (item == null || item.Type == null || item.Owner != null || !_groundItems.Contains(item))
        {
            CancelPickup(unit);
            AdvanceOrder(unit);
            return;
        }

        float range = GameplayConfig.PickupItemRange;
        float dx = unit.Position.X - item.Position.X;
        float dz = unit.Position.Z - item.Position.Z;
        if (dx * dx + dz * dz <= range * range)
        {
            CancelPickup(unit);
            CompletePickup(unit, item);
            AdvanceOrder(unit);
            return;
        }

        unit.MoveTarget = item.Position;
        Advance(unit, item.Position, range);
    }

    private static void CancelPickup(Unit unit)
    {
        unit.PickupTarget = null;
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.View?.Play("Stand");
    }

    private bool CompletePickup(Unit unit, Item item)
    {
        if (!unit.AddItem(item)) return false;

        _groundItems.Remove(item);
        ApplyItemBonus(unit, item);
        ItemPickedUp?.Invoke(unit, item);
        if (_logItemBudget > 0)
        {
            _logItemBudget--;
            GD.Print($"[ITEM] {unit.Type.Name} 拾取 {item.Type.Name}（{unit.ItemCount}/{Unit.InventorySize}）");
        }
        return true;
    }

    private int _logItemBudget = 8;

    public void NotifyItemPickedUp(Unit picker, Item item)
    {
        if (picker == null || item == null) return;
        ItemPickedUp?.Invoke(picker, item);
    }

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
                ItemDropped?.Invoke(victim, item);
                if (_logItemBudget > 0)
                {
                    _logItemBudget--;
                    GD.Print($"[ITEM] {victim.Type.Name} 掉落 {type.Name}({drop.ItemId}) chance={drop.Chance}%");
                }
                break;
            }
        }
    }

    }
