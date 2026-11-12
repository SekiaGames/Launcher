using System;
using System.Collections.Generic;

using OpenWC3.Game;
namespace OpenWC3;

/// <summary>主动物品的目标形态：无目标（药水/卷轴）、点单位、点地面。</summary>
public enum ItemTargetMode
{
    None,
    Unit,
    Point,
}

/// <summary>物品点下去之后走哪条路：一次性效果 / 复用技能实现 / 回血回蓝。</summary>
public enum ItemUseKind
{
    None,
    OneShot,
    Ability,
    Consumable,
}

/// <summary>
/// 一件物品「点一下」的执行计划：走哪条路、哪个能力码、要不要选目标、冷却几秒。
/// </summary>
public readonly struct ItemUsePlan
{
    public readonly ItemUseKind Kind;
    public readonly string Code;
    public readonly AbilityType Type;
    public readonly ItemTargetMode TargetMode;
    public readonly float Cooldown;

    public ItemUsePlan(ItemUseKind kind, string code, AbilityType type, ItemTargetMode targetMode, float cooldown)
    {
        Kind = kind;
        Code = code;
        Type = type;
        TargetMode = targetMode;
        Cooldown = cooldown;
    }

    public static readonly ItemUsePlan Empty = new(ItemUseKind.None, "", null, ItemTargetMode.None, 0f);

    public bool Usable => Kind != ItemUseKind.None;
    public bool NeedsTarget => TargetMode != ItemTargetMode.None;
}

/// <summary>
/// 物品 abilList → 「那一个」主动能力。对标客户端
/// RuntimeItemAbilityResolution.ResolveActiveAbility：一个物品可以挂着多个被动（属性/法球/光环），
/// 主动能力只有一个；并且只有 usable=1 的物品才去认它，否则纯被动装备会被误判成「要点目标」。
///
/// 判定顺序与 <see cref="World.UseItem"/> 完全一致（同一个计划驱动 UI 提示与执行），
/// 避免「UI 判 A、执行跑 B」的错位。
/// </summary>
public static class ItemUseRules
{
    private static readonly Dictionary<string, ItemUsePlan> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ItemUsePlan PlanFor(ItemType item)
    {
        if (item == null) return ItemUsePlan.Empty;
        string key = item.Id ?? "";
        if (key.Length > 0 && Cache.TryGetValue(key, out ItemUsePlan cached)) return cached;

        ItemUsePlan plan = Resolve(item);
        if (key.Length > 0) Cache[key] = plan;
        return plan;
    }

    public static ItemTargetMode TargetModeFor(ItemType item) => PlanFor(item).TargetMode;

    public static bool Usable(ItemType item) => PlanFor(item).Usable;

    private static ItemUsePlan Resolve(ItemType item)
    {
        if (!item.Usable) return ItemUsePlan.Empty;

        foreach (string raw in item.AbilList.Split(',', ';', StringSplitOptions.TrimEntries))
        {
            if (raw.Length < 4) continue;
            string code = raw[..4];
            AbilityType type = AbilityCatalog.Get(code);
            if (type == null) continue;

            // 物品专用的一次性效果（药水、卷轴、守护、揭示、保存权杖…）优先
            if (ItemUseEffect.Handles(code))
            {
                return new ItemUsePlan(ItemUseKind.OneShot, code, type,
                    ItemUseEffect.TargetModeOf(code), type.CooldownAt(1));
            }

            // 复用技能实现的那一类：码在注册表里、且实例不是被动/光环
            if (!RuntimeAbilityFactory.IsImplemented(code)) continue;
            Ability ability = RuntimeAbilityFactory.Create(type);
            if (ability == null || ability is PassiveAbility || ability.IsPassive || ability.IsAura) continue;

            return new ItemUsePlan(ItemUseKind.Ability, code, type, ModeOf(ability.TargetMode), type.CooldownAt(1));
        }

        // 兜底：属性书这类没有施法形态的物品，效果写在能力数值里（回血/回蓝）
        if (!item.Consumable.IsEmpty)
        {
            return new ItemUsePlan(ItemUseKind.Consumable, "", null, ItemTargetMode.None, 0f);
        }
        return ItemUsePlan.Empty;
    }

    private static ItemTargetMode ModeOf(AbilityTargetMode mode)
    {
        return mode switch
        {
            AbilityTargetMode.Unit => ItemTargetMode.Unit,
            AbilityTargetMode.Point => ItemTargetMode.Point,
            _ => ItemTargetMode.None,
        };
    }
}