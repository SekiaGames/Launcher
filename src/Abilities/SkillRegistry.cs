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
        ["AOcl"] = () => new ChainLightning(),// 闪电链（先知/暗影猎手/纳迦）
        // —— 死亡骑士 / 巫妖 / 恐惧魔王 / 地穴领主 ——
        ["AUdc"] = () => new DeathCoil(),     // 死亡缠绕
        ["AUdp"] = () => new DeathPact(),     // 死亡契约
        ["AUdr"] = () => new DarkRitual(),    // 黑暗仪式
        ["AUau"] = () => new UnholyAura(),    // 邪恶光环
        ["AUan"] = () => new AnimateDead(),   // 操纵死尸
        ["AUfn"] = () => new FrostNova(),     // 霜冻新星
        ["AUcs"] = () => new CarrionSwarm(),  // 腐臭蜂群
        ["AUsl"] = () => new Sleep(),         // 睡眠
        ["AUav"] = () => new VampiricAura(),  // 吸血光环
        ["AUin"] = () => new Inferno(),       // 地狱火
        ["AUim"] = () => new Impale(),        // 穿刺
        ["AUts"] = () => new SpikedCarapace(),// 尖刺甲壳
        ["AUcb"] = () => new CarrionScarabs(),// 腐尸甲虫
        // —— 牛头人酋长 / 暗影猎手 ——
        ["AOsh"] = () => new ShockWave(),     // 震荡波
        ["AOws"] = () => new WarStomp(),      // 战争践踏
        ["AOae"] = () => new EnduranceAura(), // 耐久光环（凯恩 AOr2 同实现）
        ["AOr2"] = () => new EnduranceAura(),
        ["AOhw"] = () => new HealingWave(),   // 医疗波
        ["AOsw"] = () => new SerpentWard(),   // 毒蛇守卫（基类码 AOwd）
        // —— 丛林守护者 / 恶魔猎手 / 守望者 ——
        ["AEer"] = () => new EntanglingRoots(),// 纠缠根须
        ["AEah"] = () => new ThornsAura(),     // 荆棘光环
        ["AEmb"] = () => new ManaBurn(),       // 法力燃烧
        ["AEim"] = () => new Immolation(),     // 献祭（开关型）
        ["AEfk"] = () => new FanOfKnives(),    // 刀阵旋风
        ["AEsh"] = () => new ShadowStrike(),   // 暗影突袭
        ["AEar"] = () => new TrueshotAura(),   // 强击光环（只加远程攻击）
        // —— 大魔法师 ——
        ["AHab"] = () => new BrillianceAura(), // 辉煌光环
        // —— 大魔法师 / 巫妖 / 守望者 / 黑暗游侠 ——
        ["AHbz"] = () => new Blizzard(),      // 暴风雪（引导 + 点目标）
        ["AUdd"] = () => new DeathAndDecay(), // 死亡腐烂（引导 + 百分比）
        ["AEbl"] = () => new Blink(),         // 闪烁（守望者，点目标瞬移）
        ["ANsi"] = () => new Silence(),       // 沉默（点目标 AoE）
        ["AEev"] = () => new Evasion(),       // 闪避（被动）
        ["AUfa"] = () => new FrostArmor(),    // 霜冻护甲
        // —— 召唤与复活 ——
        ["AHwe"] = () => new SummonUnit(),     // 水元素（hwat/hwt2/hwt3）
        ["AEfn"] = () => new SummonUnit(),     // 树人（efon，丛林守护者）
        ["AEst"] = () => new SummonUnit(),     // 侦察猫头鹰（nowl/now2/now3，女祭司）
        ["AOsf"] = () => new SummonUnit(),     // 幽灵狼（osw1/2/3）
        ["ANsg"] = () => new SummonUnit(),     // 召唤熊（ngz1/2/3，兽王）
        ["AHre"] = () => new Resurrection(),   // 复活
        // —— 尸体利用（食尸鬼吞尸 / 亡灵巫师复活死尸） ——
        ["Acan"] = () => new Cannibalize(),
        ["Arai"] = () => new RaiseDead(),
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
