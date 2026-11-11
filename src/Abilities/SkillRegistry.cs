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
        ["AOhx"] = () => new Hex(),           // 妖术（变随机小动物）
        ["Aply"] = () => new Polymorph(),     // 变形术（女巫，固定变羊）
        ["AOvd"] = () => new BigBadVoodoo(),  // 巫毒（引导，友军无敌）
        // —— 丛林守护者 / 恶魔猎手 / 守望者 ——
        ["AEer"] = () => new EntanglingRoots(),// 纠缠根须
        ["AEah"] = () => new ThornsAura(),     // 荆棘光环
        ["AEmb"] = () => new ManaBurn(),       // 法力燃烧
        ["AEim"] = () => new Immolation(),     // 献祭（开关型）
        ["AEfk"] = () => new FanOfKnives(),    // 刀阵旋风
        ["AEsh"] = () => new ShadowStrike(),   // 暗影突袭
        ["AEar"] = () => new TrueshotAura(),   // 强击光环（只加远程攻击）
        ["AEtq"] = () => new Tranquility(),    // 宁静（引导，友军持续回血）
        ["AEsf"] = () => new Starfall(),       // 群星坠落（引导，AoE 每波）
        ["AEme"] = () => new Metamorphosis(),  // 变身（恶魔形态）
        ["AOww"] = () => new Bladestorm(),     // 剑刃风暴（引导，自身 AoE）
        // —— 大魔法师 ——
        ["AHab"] = () => new BrillianceAura(), // 辉煌光环
        // —— 大魔法师 / 巫妖 / 守望者 / 黑暗游侠 ——
        ["AHbz"] = () => new Blizzard(),      // 暴风雪（引导 + 点目标）
        ["AUdd"] = () => new DeathAndDecay(), // 死亡腐烂（引导 + 百分比）
        ["AEbl"] = () => new Blink(),         // 闪烁（守望者，点目标瞬移）
        ["ANsi"] = () => new Silence(),       // 沉默（点目标 AoE）
        ["ANch"] = () => new Charm(),         // 魅惑（黑暗游侠大招，抢一个非英雄单位）
        ["ANfd"] = () => new FingerOfDeath(), // 死亡之指（阿克蒙德，DataC 直伤）
        ["ANsl"] = () => new SoulPreservation(), // 灵魂保存（玛尔加尼斯，到期接管并死亡）
        ["ANdc"] = () => new DarkConversion(),   // 黑暗转换（玛尔加尼斯，沉睡后换单位）
        ["AOfs"] = () => new FarSight(),      // 远视（先知，点地面揭示）
        ["Abrf"] = () => new DruidForm(),     // 熊形态（利爪德鲁伊，永久形态）
        ["Arav"] = () => new DruidForm(),     // 乌鸦形态（猛禽德鲁伊，永久形态）
        ["AOre"] = () => new Reincarnation(), // 重生（牛头人酋长，死亡后 7s 原地复活）
        ["AEsv"] = () => new SpiritOfVengeance(), // 复仇之魂（守望者，死亡召魂）
        ["AHmt"] = () => new MassTeleport(),  // 群体传送（大法师，点全图）
        ["AEev"] = () => new Evasion(),       // 闪避（被动）
        ["AUfa"] = () => new FrostArmor(),    // 霜冻护甲（巫妖，主动版）
        ["AUfu"] = () => new FrostArmor(),    // 霜冻护甲（巫妖，自动施法版）
        // —— 血法师 ——
        ["AHbn"] = () => new Banish(),        // 放逐（虚无 + 减速）
        ["AHfs"] = () => new FlameStrike(),   // 烈焰风暴（满伤阶段 + 余烬阶段）
        ["AHdr"] = () => new SiphonMana(),    // 吸魔（引导抽蓝 / 反向输蓝）
        ["AHpx"] = () => new Phoenix(),       // 凤凰（hphx，阵亡变蛋再孵化）
        ["Aphx"] = () => new PhoenixEgg(),    // 凤凰变蛋（被动）
        // —— 兽族 / 亡灵 / 暗夜终极与普攻技 ——
        ["AUls"] = () => new LocustSwarm(),   // 蝗群（地穴领主，20 只 uloc）
        ["AOeq"] = () => new Earthquake(),    // 地震（先知，引导 25s 拆塔 + 减速）
        ["AHfa"] = () => new SearingArrows(), // 灼热之箭（白虎，每箭附加火焰伤害）
        // —— 剑圣 / 黑暗游侠 ——
        ["AOmi"] = () => new MirrorImage(),   // 镜像分身（剑圣，幻象 + 本体随机换位）
        ["AOwk"] = () => new WindWalk(),      // 疾风步（剑圣，隐身 + 破隐一击）
        ["ANba"] = () => new BlackArrow(),    // 黑蚀箭（黑暗游侠，标记击杀召骷髅）
        ["ANdr"] = () => new LifeDrain(),     // 生命汲取（黑暗游侠，引导抽血回自己）
        // —— 酒馆英雄 ——
        ["ANrf"] = () => new RainOfFire(),    // 火焰雨（深渊魔王，引导多波 + 灼烧）
        ["ANto"] = () => new Tornado(),       // 龙卷风（海巫，落点召 ntor，断引导就散）
        ["ANfl"] = () => new ForkedLightning(), // 碎裂闪电（海巫，扇形 3 个目标）
        ["ANfa"] = () => new ColdArrows(),    // 冰霜箭（海巫，普攻附加 + 减速）
        ["AHca"] = () => new ColdArrows(),    // 寒冰箭（凯尔萨斯，同实现）
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
        // —— 人族单位 ——
        ["Aslo"] = () => new SlowUnit(),          // 减速（女巫，自动施法）
        ["Aivs"] = () => new InvisibilitySpell(), // 隐身术（女巫，普攻破隐）
        ["Afla"] = () => new Flare(),             // 照明弹（翔车，区域揭示）
        ["AHta"] = () => new RevealArea(),        // 魔法岗哨反隐
        ["Atru"] = () => new DetectPassive(),     // 真实视域（阴影/直升机/魔法哨塔）
        ["Agyv"] = () => new DetectPassive(),
        ["Adts"] = () => new DetectPassive(),
        ["Afbk"] = () => new Feedback(),          // 反馈（破法者，烧蓝转伤害）
        ["Afbt"] = () => new Feedback(),
        ["Abof"] = () => new BallsOfFire(),       // 火球（投石车，附加范围火焰）
        ["Acmg"] = () => new Charm(),             // 控制魔法（破法者，抢召唤物）
        // —— 兽族单位 ——
        ["Ablo"] = () => new Bloodlust(),         // 嗜血（萨满，攻速/移速）
        ["Alsh"] = () => new LightningShield(),   // 闪电护盾（萨满）
        ["Apg2"] = () => new Purge(),             // 净化（萨满）
        ["Awar"] = () => new Pulverize(),         // 粉碎（牛头人，概率范围伤害）
        ["Absk"] = () => new Berserk(),           // 狂暴（猎头者）
        ["Aliq"] = () => new LiquidFire(),        // 液体火焰（蝙蝠骑士，烧建筑）
        ["Auco"] = () => new UnstableConcoction(),// 不稳定化合物（蝙蝠骑士自爆）
        ["Aven"] = () => new VenomSpears(),       // 毒矛（双足飞龙，持续毒伤）
        ["Asal"] = () => new Pillage(),           // 掠夺（兽族步兵，拆建筑得金）
        ["Aspi"] = () => new SpikedBarricades(),  // 尖刺路障（兽族建筑反伤）
        ["Aakb"] = () => new WarDrums(),          // 战鼓光环（科多兽）
        ["AIcd"] = () => new WarDrums(),          // 物品版命令光环
        ["Acn2"] = () => new Cannibalize(),       // 食尸（憎恶）
        // —— 暗夜单位 ——
        ["Ara2"] = () => new Roar(),              // 咆哮（熊德）
        ["Aroa"] = () => new Roar(),
        ["Arej"] = () => new Rejuvenation(),      // 回春（猛禽德鲁伊）
        ["Acyc"] = () => new Cyclone(),           // 旋风（猛禽德鲁伊）
        ["Afae"] = () => new FaerieFire(),        // 精灵之火（猛禽德鲁伊）
        ["Afa2"] = () => new FaerieFire(),
        ["Aadm"] = () => new AbolishMagic(),      // 废除魔法（树妖）
        ["Aspo"] = () => new SlowPoison(),        // 慢性毒（树妖）
        ["Amfl"] = () => new ManaFlare(),         // 法力闪耀（仙女龙）
        ["Apsh"] = () => new PhaseShift(),        // 相位转移（仙女龙）
        ["Atau"] = () => new Taunt(),             // 嘲讽（山岭巨人）
        ["Assk"] = () => new HardenedSkin(),      // 硬化皮肤（山岭巨人）
        ["Aegr"] = () => new ElunesGrace(),       // 艾露恩恩泽（弓箭手）
        ["Aesn"] = () => new Sentinel(),          // 哨兵（女猎手，区域视野）
        ["Aesr"] = () => new Sentinel(),
        ["Amgl"] = () => new MoonGlaive(),        // 月刃（女猎手，弹跳）
        ["Amgr"] = () => new MoonGlaive(),
        ["Alit"] = () => new LightningAttack(),   // 闪电攻击（奇美拉）
        ["Acor"] = () => new CorrosiveBreath(),   // 腐蚀吐息（奇美拉，烧建筑）
        // —— 亡灵单位 ——
        ["Advm"] = () => new DevourMagic(),       // 吞噬魔法（毁灭者）
        ["Afak"] = () => new OrbOfAnnihilation(), // 毁灭之球（毁灭者）
        ["Aabs"] = () => new AbsorbMana(),        // 吸收法力（毁灭者）
        ["Aweb"] = () => new Web(),               // 蛛网（地穴恶魔，缠住空中）
        ["Afrb"] = () => new FrostBreath(),       // 霜冻吐息（冰霜巨龙）
        ["Afrz"] = () => new FrostBreath(),       // 冻结吐息（冰冻之魂）
        ["Agho"] = () => new Ghost(),             // 幽灵（阴影，常驻隐身）
        ["Arpl"] = () => new Replenish(),         // 补充生命（黑曜石雕像）
        ["Arpm"] = () => new Replenish(),         // 补充法力
        ["Aap2"] = () => new PlagueAura(),        // 瘟疫光环（瘟疫守卫）
        ["Aapl"] = () => new PlagueAura(),
        // —— 英雄别名（同实现跨单位复用） ——
        ["AOr3"] = () => new Reincarnation(),     // 凯恩·血蹄重生
        ["ANrn"] = () => new Reincarnation(),     // 玛诺洛斯重生
        ["AOs2"] = () => new ShockWave(),         // 凯恩震荡波
        ["ANsh"] = () => new ShockWave(),         // 加里索斯震荡波
        ["AOw2"] = () => new WarStomp(),          // 凯恩战争践踏
        ["AEIl"] = () => new Metamorphosis(),     // 伊利丹变身
        ["AEvi"] = () => new Metamorphosis(),
        ["ANav"] = () => new Avatar(),            // 加里索斯天神下凡
        ["ANhw"] = () => new HealingWave(),       // 洛克汗医疗波
        ["ANhx"] = () => new Hex(),               // 洛克汗妖术
        ["Arsw"] = () => new SerpentWard(),       // 洛克汗毒蛇守卫
        ["AOls"] = () => new LocustSwarm(),       // 洛克汗巫毒精灵
        ["Arsg"] = () => new SummonUnit(),        // 雷克萨召熊
        ["Arsq"] = () => new SummonUnit(),        // 雷克萨召豪猪
        ["ANsb"] = () => new ThunderBolt(),       // 雷克萨风暴之锤
        ["AIan"] = () => new AnimateDead(),       // 物品版操纵死尸
        ["AIrs"] = () => new Resurrection(),      // 物品版复活
        ["AIrx"] = () => new Resurrection(),
        // —— 酒馆英雄剩余 ——
        ["ANvc"] = () => new Volcano(),          // 火山（火焰领主，引导 8 波 × 5s）
        ["Acef"] = () => new StormEarthFire(),   // 风暴大地火焰（陈，召三元素）
        ["ANcf"] = () => new BreathOfFire(),     // 火焰吐息（陈，直线 + 点燃醉酒目标）
        ["Acdh"] = () => new DrunkenHaze(),      // 醉酒云雾（最高 80% 未命中 + 减速）
        ["Acdb"] = () => new DrunkenBrawler(),   // 醉拳（暴击 + 闪避）
        ["ANht"] = () => new HowlOfTerror(),     // 恐惧嚎叫（范围减攻/减甲）
        ["ANca"] = () => new CleavingAttack(),   // 分裂攻击（近战溅射）
        ["ANdo"] = () => new Doom(),             // 末日（持续伤害 + 死亡召恶魔）
        ["Arsp"] = () => new Stampede(),         // 兽群狂奔（雷克萨，引导 30s）
        // —— 暗夜被动与光环 ——
        ["Ashm"] = () => new ShadowMeld(),       // 影遁（夜间静止后隐身）
        ["Aoar"] = () => new RegenerationAura(), // 再生光环（治疗守卫）
        ["Aabr"] = () => new RegenerationAura(), // 再生光环（黑曜石雕像）
        ["Aap1"] = () => new PlagueAura(),       // 瘟疫光环（憎恶）
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
