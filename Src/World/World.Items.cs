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
                item.CooldownRemaining = Mathf.Max(0f, item.CooldownRemaining - Tick);
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
            int restocked = shop.TickShop(Tick);
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
            ApplyItemBonus(picker, item);
            ItemPickedUp?.Invoke(picker, item);
            if (_logItemBudget > 0)
            {
                _logItemBudget--;
                GD.Print($"[ITEM] {picker.Type.Name} 捡起 {item.Type.Name}（{picker.ItemCount}/{Unit.InventorySize}）");
            }
        }
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
