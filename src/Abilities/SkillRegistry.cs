using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 技能注册表：技能码 → 实现类。对标客户端 Src/Skills/SkillRegistry.cs。
/// 表里没有的码退化成 <see cref="PassiveAbility"/>（只占位，不产生效果），
/// 这样单位的 abilList 即使包含未实现的技能也不会报错。
/// </summary>
public static class SkillRegistry
{
    private static readonly Dictionary<string, Func<Ability>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // —— 圣骑士 ——
        ["AHhb"] = () => new HolyLight(),     // 圣光术
        ["AHds"] = () => new DivineShield(),  // 神圣护甲
        ["AHad"] = () => new DevotionAura(),  // 专注光环
        // —— 山丘之王 ——
        ["AHtb"] = () => new ThunderBolt(),   // 风暴之锤
        ["AHtc"] = () => new ThunderClap(),   // 雷霆一击
        ["AHbh"] = () => new Bash(),          // 重击
        ["AHav"] = () => new Avatar(),        // 天神下凡
        // —— 剑圣 ——
        ["AOcr"] = () => new CriticalStrike(),// 致命一击
        // —— 召唤与复活 ——
        ["AHwe"] = () => new SummonUnit(),     // 水元素
        ["AEst"] = () => new SummonUnit(),     // 召唤树人
        ["AOsf"] = () => new SummonUnit(),     // 幽灵狼
        ["ANsg"] = () => new SummonUnit(),     // 熔岩怪
        ["AHre"] = () => new Resurrection(),   // 复活
        // —— 通用治疗（牧师 / 野怪牧师，自动施法） ——
        ["Ahea"] = () => new Heal(),
        // —— 物品技能里与英雄同源的码 ——
        ["AIbx"] = () => new Bash(),          // 物品版重击
        ["AIcs"] = () => new CriticalStrike(),// 物品版致命一击
        ["AIad"] = () => new DevotionAura(),  // 物品版专注光环
        ["AIdv"] = () => new DivineShield(),  // 物品版神圣护甲
        ["AIhl"] = () => new HolyLight(),     // 物品版圣光术
    };

    public static bool IsImplemented(string id) => Map.ContainsKey(id);

    /// <summary>按技能 ID（找不到再按基类码）建实例；都不认识就返回占位实现。</summary>
    public static Ability Create(AbilityType type)
    {
        if (type == null) return null;

        Ability ability = Map.TryGetValue(type.Id, out Func<Ability> factory)
            ? factory()
            : Map.TryGetValue(type.Code ?? "", out Func<Ability> codeFactory)
                ? codeFactory()
                : new PassiveAbility();

        ability.Type = type;
        return ability;
    }
}

/// <summary>未实现的技能占位：数据与图标都在，但没有任何效果。</summary>
public sealed class PassiveAbility : Ability
{
    public override bool IsPassive => true;

    public override bool OnCast(World world, Unit target, Vector3 point) => false;
}
