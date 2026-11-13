using Godot;
using System;
using System.Collections.Generic;

using OpenWC3.Game;
namespace OpenWC3;

/// <summary>
/// 技能注册表：技能码 → 实现类。对标客户端 Src/Skills/RuntimeAbilityFactory.cs。
/// 表里没有的码退化成 <see cref="PassiveAbility"/>（只占位，不产生效果），
/// 这样单位的 abilList 即使包含未实现的技能也不会报错。
/// </summary>
public static class RuntimeAbilityFactory
{
    private static readonly Dictionary<string, Func<Ability>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // —— 圣骑士 ——
        ["AHhb"] = () => new HolyLight(),     // 圣光术
        ["AHds"] = () => WithObjects<RuntimeDivineShieldAbility>(),  // 神圣护甲
        ["AHad"] = () => new DevotionAura(),  // 专注光环
        // —— 山丘之王 ——
        ["AHtb"] = () => new RuntimeStormBoltAbility(),   // 风暴之锤
        ["AHtc"] = () => new RuntimeThunderClapAbility(),   // 雷霆一击
        ["AHbh"] = () => new RuntimeBashAbility(),          // 重击
        ["AHav"] = () => new Avatar(),        // 天神下凡
        // —— 剑圣 ——
        ["AOcr"] = () => new CriticalStrike(),// 致命一击
        ["AOcl"] = () => new ChainLightning(),// 闪电链（先知/暗影猎手/纳迦）
        // —— 死亡骑士 / 巫妖 / 恐惧魔王 / 地穴领主 ——
        ["AUdc"] = () => new RuntimeDeathCoilAbility(),     // 死亡缠绕
        ["AUdp"] = () => new RuntimeDeathPactAbility(),     // 死亡契约
        ["AUdr"] = () => new RuntimeDarkRitualAbility(),    // 黑暗仪式
        ["AUau"] = WithObjects<RuntimeUnholyAuraAbility>,    // 邪恶光环
        ["AUan"] = () => new RuntimeAnimateDeadAbility(),   // 操纵死尸
        ["AUfn"] = () => new RuntimeFrostNovaAbility(),     // 霜冻新星
        ["AUcs"] = () => new RuntimeCarrionSwarmAbility(),  // 腐臭蜂群
        ["AUsl"] = WithObjects<RuntimeSleepAbility>,         // 睡眠
        ["AUav"] = () => new VampiricAura(),  // 吸血光环
        ["AUin"] = () => new RuntimeInfernoAbility(),       // 地狱火
        ["AUim"] = () => new RuntimeImpaleAbility(),        // 穿刺
        ["AUts"] = () => new RuntimeSpikedCarapaceAbility(),// 尖刺甲壳
        ["AUcb"] = () => new RuntimeCarrionBeetlesAbility(),// 腐尸甲虫
        // —— 牛头人酋长 / 暗影猎手 ——
        ["AOsh"] = () => new RuntimeShockwaveAbility(),     // 震荡波
        ["AOws"] = () => new RuntimeWarStompAbility(),      // 战争践踏
        ["AOae"] = () => new EnduranceAura(), // 耐久光环（凯恩 AOr2 同实现）
        ["AOr2"] = () => new EnduranceAura(),
        ["AOhw"] = () => new RuntimeHealingWaveAbility(),   // 医疗波
        ["AOsw"] = () => new SerpentWard(),   // 毒蛇守卫（基类码 AOwd）
        ["AOhx"] = () => new RuntimeHexAbility(),           // 妖术（变随机小动物）
        ["Aply"] = () => new RuntimePolymorphAbility(),     // 变形术（女巫，固定变羊）
        ["AOvd"] = () => new RuntimeBigBadVoodooAbility(),  // 巫毒（引导，友军无敌）
        // —— 丛林守护者 / 恶魔猎手 / 守望者 ——
        ["AEer"] = () => new RuntimeEntanglingRootsAbility(),// 纠缠根须
        ["AEah"] = WithObjects<RuntimeThornsAuraAbility>,     // 荆棘光环
        ["AEmb"] = () => new RuntimeManaBurnAbility(),       // 法力燃烧
        ["AEim"] = () => new RuntimeImmolationAbility(),     // 献祭（开关型）
        ["AEfk"] = () => new RuntimeFanOfKnivesAbility(),    // 刀阵旋风
        ["AEsh"] = () => new RuntimeShadowStrikeAbility(),   // 暗影突袭
        ["AEar"] = () => new RuntimeTrueshotAuraAbility(),   // 强击光环（只加远程攻击）
        ["AEtq"] = () => new RuntimeTranquilityAbility(),    // 宁静（引导，友军持续回血）
        ["AEsf"] = () => new RuntimeStarfallAbility(),       // 群星坠落（引导，AoE 每波）
        ["AEme"] = () => new Metamorphosis(),  // 变身（恶魔形态）
        ["AOww"] = () => new Bladestorm(),     // 剑刃风暴（引导，自身 AoE）
        // —— 大魔法师 ——
        ["AHab"] = WithObjects<RuntimeBrillianceAuraAbility>, // 辉煌光环
        // —— 大魔法师 / 巫妖 / 守望者 / 黑暗游侠 ——
        ["AHbz"] = () => new RuntimeBlizzardAbility(),      // 暴风雪（引导 + 点目标）
        ["AUdd"] = () => new RuntimeDeathAndDecayAbility(), // 死亡腐烂（引导 + 百分比）
        ["AEbl"] = () => new Blink(),         // 闪烁（守望者，点目标瞬移）
        ["ANsi"] = () => new Silence(),       // 沉默（点目标 AoE）
        ["ANch"] = () => new RuntimeCharmAbility(),         // 魅惑（黑暗游侠大招，抢一个非英雄单位）
        ["ANfd"] = () => new FingerOfDeath(), // 死亡之指（阿克蒙德，DataC 直伤）
        ["ANsl"] = () => new SoulPreservation(), // 灵魂保存（玛尔加尼斯，到期接管并死亡）
        ["ANdc"] = () => new DarkConversion(),   // 黑暗转换（玛尔加尼斯，沉睡后换单位）
        ["AOfs"] = () => new FarSight(),      // 远视（先知，点地面揭示）
        ["Abrf"] = () => new RuntimeDruidFormAbility(),     // 熊形态（利爪德鲁伊，永久形态）
        ["Arav"] = () => new RuntimeDruidFormAbility(),     // 乌鸦形态（猛禽德鲁伊，永久形态）
        ["AOre"] = () => new RuntimeReincarnationAbility(), // 重生（牛头人酋长，死亡后 7s 原地复活）
        ["AEsv"] = () => new RuntimeVengeanceAbility(), // 复仇之魂（守望者，死亡召魂）
        ["AHmt"] = () => new RuntimeMassTeleportAbility(),  // 群体传送（大法师，点全图）
        ["AEev"] = () => new Evasion(),       // 闪避（被动）
        ["AUfa"] = () => new RuntimeFrostArmorAbility(),    // 霜冻护甲（巫妖，主动版）
        ["AUfu"] = () => new RuntimeFrostArmorAbility(),    // 霜冻护甲（巫妖，自动施法版）
        // —— 血法师 ——
        ["AHbn"] = () => new RuntimeBanishAbility(),        // 放逐（虚无 + 减速）
        ["AHfs"] = () => new RuntimeFlameStrikeAbility(),   // 烈焰风暴（满伤阶段 + 余烬阶段）
        ["AHdr"] = () => new SiphonMana(),    // 吸魔（引导抽蓝 / 反向输蓝）
        ["AHpx"] = () => new Phoenix(),       // 凤凰（hphx，阵亡变蛋再孵化）
        ["Aphx"] = () => new RuntimePhoenixEggBuff(),    // 凤凰变蛋（被动）
        // —— 兽族 / 亡灵 / 暗夜终极与普攻技 ——
        ["AUls"] = () => new RuntimeLocustSwarmAbility(),   // 蝗群（地穴领主，20 只 uloc）
        ["AOeq"] = () => new RuntimeEarthquakeAbility(),    // 地震（先知，引导 25s 拆塔 + 减速）
        ["AHfa"] = () => new RuntimeFlamingArrowsAbility(), // 灼热之箭（白虎，每箭附加火焰伤害）
        // —— 剑圣 / 黑暗游侠 ——
        ["AOmi"] = () => new RuntimeMirrorImageAbility(),   // 镜像分身（剑圣，幻象 + 本体随机换位）
        ["AOwk"] = () => new RuntimeWindWalkAbility(),      // 疾风步（剑圣，隐身 + 破隐一击）
        ["ANba"] = () => new RuntimeBlackArrowAbility(),    // 黑蚀箭（黑暗游侠，标记击杀召骷髅）
        ["ANdr"] = () => new LifeDrain(),     // 生命汲取（黑暗游侠，引导抽血回自己）
        // —— 酒馆英雄 ——
        ["ANrf"] = () => WithObjects<RuntimeRainOfFireAbility>(),    // 火焰雨（深渊魔王，引导多波 + 灼烧）
        ["ANto"] = () => new RuntimeTornadoAbility(),       // 龙卷风（海巫，落点召 ntor，断引导就散）
        ["ANfl"] = () => new RuntimeForkedLightningAbility(), // 碎裂闪电（海巫，扇形 3 个目标）
        ["ANfa"] = () => new RuntimeColdArrowsAbility(),    // 冰霜箭（海巫，普攻附加 + 减速）
        ["AHca"] = () => new RuntimeColdArrowsAbility(),    // 寒冰箭（凯尔萨斯，同实现）
        // —— 召唤与复活 ——
        ["AHwe"] = () => new SummonUnit(),     // 水元素（hwat/hwt2/hwt3）
        ["AEfn"] = () => new SummonUnit(),     // 树人（efon，丛林守护者）
        ["AEst"] = () => new SummonUnit(),     // 侦察猫头鹰（nowl/now2/now3，女祭司）
        ["AOsf"] = () => new SummonUnit(),     // 幽灵狼（osw1/2/3）
        ["ANsg"] = () => new SummonUnit(),     // 召唤熊（ngz1/2/3，兽王）
        ["AHre"] = () => new RuntimeResurrectionAbility(),   // 复活
        // —— 尸体利用（食尸鬼吞尸 / 亡灵巫师复活死尸） ——
        ["Acan"] = () => new RuntimeCannibalizeAbility(),
        ["Arai"] = () => new RaiseDead(),
        // —— 通用治疗（牧师 / 野怪牧师，自动施法） ——
        ["Ahea"] = () => new Heal(),
        // —— 人族单位 ——
        ["Aslo"] = () => new RuntimeSlowAbility(),          // 减速（女巫，自动施法）
        ["Aivs"] = () => WithObjects<RuntimeInvisibilityAbility>(), // 隐身术（女巫，普攻破隐）
        ["Afla"] = () => new RuntimeFlareAbility(),             // 照明弹（翔车，区域揭示）
        ["AHta"] = () => new RevealArea(),        // 魔法岗哨反隐
        ["Atru"] = () => new DetectPassive(),     // 真实视域（阴影/直升机/魔法哨塔）
        ["Agyv"] = () => new DetectPassive(),
        ["Adts"] = () => new DetectPassive(),
        ["Afbk"] = () => new RuntimeFeedbackAbility(),          // 反馈（破法者，烧蓝转伤害）
        ["Afbt"] = () => new RuntimeFeedbackAbility(),
        ["Abof"] = () => new RuntimeBurningOilAbility(),       // 火球（投石车，附加范围火焰）
        ["Acmg"] = () => new RuntimeCharmAbility(),             // 控制魔法（破法者，抢召唤物）
        // —— 兽族单位 ——
        ["Ablo"] = () => new RuntimeBloodlustAbility(),         // 嗜血（萨满，攻速/移速）
        ["Alsh"] = () => new RuntimeLightningShieldAbility(),   // 闪电护盾（萨满）
        ["Apg2"] = () => WithObjects<RuntimePurgeAbility>(),             // 净化（萨满）
        ["Aprg"] = () => WithObjects<RuntimePurgeAbility>(),             // 净化（萨满祭司 oshm）
        ["Awar"] = () => new RuntimePulverizeAbility(),         // 粉碎（牛头人，概率范围伤害）
        ["Absk"] = () => new Berserk(),           // 狂暴（猎头者）
        ["Aliq"] = () => WithObjects<RuntimeLiquidFireAbility>(),        // 液体火焰（蝙蝠骑士，烧建筑）
        ["Auco"] = () => new RuntimeUnstableConcoctionAbility(),// 不稳定化合物（蝙蝠骑士自爆）
        ["Aven"] = () => new RuntimeVenomSpearAbility(),       // 毒矛（双足飞龙，持续毒伤）
        ["Asal"] = () => new RuntimePillageAbility(),           // 掠夺（兽族步兵，拆建筑得金）
        ["Aspi"] = () => new RuntimeSpikedBarricadesAbility(),  // 尖刺路障（兽族建筑反伤）
        ["Aakb"] = WithObjects<RuntimeCommandAuraAbility>,          // 战鼓光环（科多兽）
        ["AIcd"] = WithObjects<RuntimeCommandAuraAbility>,          // 物品版命令光环
        ["Acn2"] = () => new RuntimeCannibalizeAbility(),       // 食尸（憎恶）
        // —— 暗夜单位 ——
        ["Ara2"] = () => new RuntimeRoarAbility(),              // 咆哮（熊德）
        ["Aroa"] = () => new RuntimeRoarAbility(),
        ["Arej"] = () => new RuntimeRejuvenationAbility(),      // 回春（猛禽德鲁伊）
        ["Acyc"] = () => new RuntimeCycloneAbility(),           // 旋风（猛禽德鲁伊）
        ["Afae"] = () => new RuntimeFaerieFireAbility(),        // 精灵之火（猛禽德鲁伊）
        ["Afa2"] = () => new RuntimeFaerieFireAbility(),
        ["Aadm"] = () => new AbolishMagic(),      // 废除魔法（树妖）
        ["Aspo"] = () => new RuntimeSlowPoisonAbility(),        // 慢性毒（树妖）
        ["Amfl"] = () => new RuntimeManaFlareAbility(),         // 法力闪耀（仙女龙）
        ["Apsh"] = () => WithObjects<RuntimePhaseShiftAbility>(),        // 相位转移（仙女龙）
        ["Atau"] = () => new RuntimeTauntAbility(),             // 嘲讽（山岭巨人）
        ["Assk"] = () => new RuntimeHardenedSkinAbility(),      // 硬化皮肤（山岭巨人）
        ["Aegr"] = () => new ElunesGrace(),       // 艾露恩恩泽（弓箭手）
        ["Aesn"] = () => new RuntimeSentinelAbility(),          // 哨兵（女猎手，区域视野）
        ["Aesr"] = () => new RuntimeSentinelAbility(),
        ["Amgl"] = () => new RuntimeMoonGlaiveAbility(),        // 月刃（女猎手，弹跳）
        ["Amgr"] = () => new RuntimeMoonGlaiveAbility(),
        ["Alit"] = () => new RuntimeLightningAttackAbility(),   // 闪电攻击（奇美拉）
        ["Acor"] = () => new RuntimeCorrosiveBreathAbility(),   // 腐蚀吐息（奇美拉，烧建筑）
        // —— 亡灵单位 ——
        ["Advm"] = () => new RuntimeDevourMagicAbility(),       // 吞噬魔法（毁灭者）
        ["Afak"] = () => new RuntimeOrbOfAnnihilationAbility(), // 毁灭之球（毁灭者）
        ["Aabs"] = () => new RuntimeAbsorbManaAbility(),        // 吸收法力（毁灭者）
        ["Aweb"] = () => new Web(),               // 蛛网（地穴恶魔，缠住空中）
        ["Afrb"] = () => new RuntimeFrostAttackAbility(),       // 霜冻吐息（冰霜巨龙）
        ["Afrz"] = () => new RuntimeFrostAttackAbility(),       // 冻结吐息（冰冻之魂）
        ["Agho"] = () => new RuntimeInvisibleGhostAbility(),             // 幽灵（阴影，常驻隐身）
        ["Arpl"] = () => new RuntimeStatueReplenishAbility(),         // 补充生命（黑曜石雕像）
        ["Arpm"] = () => new RuntimeStatueReplenishAbility(),         // 补充法力
        ["Aap2"] = () => new RuntimePlagueAuraAbility(),        // 瘟疫光环（瘟疫守卫）
        ["Aapl"] = () => new RuntimePlagueAuraAbility(),
        // —— 英雄别名（同实现跨单位复用） ——
        ["AOr3"] = () => new RuntimeReincarnationAbility(),     // 凯恩·血蹄重生
        ["ANrn"] = () => new RuntimeReincarnationAbility(),     // 玛诺洛斯重生
        ["AOs2"] = () => new RuntimeShockwaveAbility(),         // 凯恩震荡波
        ["ANsh"] = () => new RuntimeShockwaveAbility(),         // 加里索斯震荡波
        ["AOw2"] = () => new RuntimeWarStompAbility(),          // 凯恩战争践踏
        ["AEIl"] = () => new Metamorphosis(),     // 伊利丹变身
        ["AEvi"] = () => new Metamorphosis(),
        ["ANav"] = () => new Avatar(),            // 加里索斯天神下凡
        ["ANhw"] = () => new RuntimeHealingWaveAbility(),       // 洛克汗医疗波
        ["ANhx"] = () => new RuntimeHexAbility(),               // 洛克汗妖术
        ["Arsw"] = () => new SerpentWard(),       // 洛克汗毒蛇守卫
        ["AOls"] = () => new RuntimeLocustSwarmAbility(),       // 洛克汗巫毒精灵
        ["Arsg"] = () => new SummonUnit(),        // 雷克萨召熊
        ["Arsq"] = () => new SummonUnit(),        // 雷克萨召豪猪
        ["ANsb"] = () => new RuntimeStormBoltAbility(),       // 雷克萨风暴之锤
        ["AIan"] = () => new RuntimeAnimateDeadAbility(),       // 物品版操纵死尸
        ["AIrs"] = () => new RuntimeResurrectionAbility(),      // 物品版复活
        ["AIrx"] = () => new RuntimeResurrectionAbility(),
        // —— 酒馆英雄剩余 ——
        ["ANvc"] = () => new RuntimeVolcanoAbility(),          // 火山（火焰领主，引导 8 波 × 5s）
        ["Acef"] = () => new RuntimeSpiritOfVengeanceAbility(),   // 风暴大地火焰（陈，召三元素）
        ["ANcf"] = () => new RuntimeBreathOfFireAbility(),     // 火焰吐息（陈，直线 + 点燃醉酒目标）
        ["Acdh"] = () => new RuntimeDrunkenHazeAbility(),      // 醉酒云雾（最高 80% 未命中 + 减速）
        ["Acdb"] = () => new RuntimeDrunkenBrawlerAbility(),   // 醉拳（暴击 + 闪避）
        ["ANht"] = () => new RuntimeHowlOfTerrorAbility(),     // 恐惧嚎叫（范围减攻/减甲）
        ["ANca"] = () => new RuntimeCleavingAttackAbility(),   // 分裂攻击（近战溅射）
        ["ANdo"] = () => new RuntimeDoomAbility(),             // 末日（持续伤害 + 死亡召恶魔）
        ["Arsp"] = () => new RuntimeStampedeAbility(),         // 兽群狂奔（雷克萨，引导 30s）
        // —— 暗夜被动与光环 ——
        ["Ashm"] = () => new RuntimeShadowMeldAbility(),       // 影遁（夜间静止后隐身）
        ["Aoar"] = WithObjects<RuntimeLifeRegenerationAuraAbility>, // 再生光环（治疗守卫）
        ["Aabr"] = WithObjects<RuntimeLifeRegenerationAuraAbility>, // 再生光环（黑曜石雕像）
        ["Aap1"] = () => new RuntimePlagueAuraAbility(),       // 瘟疫光环（憎恶）
        // —— 夜视、抗性皮肤与物品光环 ——
        ["Ault"] = () => new RuntimeUltravisionAbility(),       // 夜视（暗夜单位）
        ["AIuv"] = () => new RuntimeUltravisionAbility(),       // 物品夜视
        ["Arsk"] = () => new RuntimeResistantSkinAbility(),     // 抗性皮肤（山岭巨人）
        ["AIba"] = () => new ItemAura(),          // 辉煌光环（物品）
        ["AIav"] = () => new ItemAura(),          // 吸血光环（物品）
        ["AIar"] = () => new ItemAura(),          // 强击光环（物品）
        ["AIae"] = () => new ItemAura(),          // 耐久光环（物品）
        ["AIau"] = () => new ItemAura(),          // 邪恶光环（物品）
        // —— 人族单位补充 ——
        ["Adef"] = () => new RuntimeDefendAbility(),           // 防御（步兵，远程减伤 50%）
        ["Ainf"] = () => new RuntimeInnerFireAbility(),        // 内火（牧师，攻击 +% / 护甲 +）
        ["Adis"] = () => new MassDispel(),       // 驱散魔法（牧师，区域驱散）
        ["Amls"] = () => new RuntimeMagicLeashAbility(),   // 空中枷锁（龙鹰，空中定身引导）
        ["Aflk"] = () => new FlakCannon(),       // 高射炮（直升机，对空溅射）
        ["Afsh"] = () => new FragShards(),       // 碎片（迫击炮，落地溅射）
        ["Aroc"] = () => new RuntimeBarrageAbility(),     // 火箭攻击（蒸汽机车，对空溅射）
        ["Asps"] = () => new RuntimeSpellStealAbility(),       // 窃法（破法者，偷一个增益）
        ["Asth"] = () => new RuntimeStormHammersAbility(),     // 风暴之锤（狮鹫，攻击弹射）
        ["Apxf"] = () => new RuntimePhoenixFireAbility(), // 凤凰火焰（凤凰，攻击溅射）
        ["Amim"] = () => new RuntimeMagicImmunityAbility(), // 魔法免疫（破法者 / 树妖 / 精灵龙）
        // —— 兽族单位补充 ——
        ["Aens"] = () => new Ensnare(),           // 诱捕（狼骑兵，定身）
        ["Aent"] = () => new Ensnare(),           // 缠绕（古树，定身）
        ["Ahwd"] = () => new RuntimeHealingWardAbility(),       // 治疗守卫（萨满，召 ohwd）
        ["Asta"] = () => new RuntimeStasisTrapAbility(),        // 静止陷阱（萨满，敌人靠近眩晕）
        ["Aspl"] = () => new RuntimeSpiritLinkAbility(),        // 灵魂链（灵魂行者，伤害分摊）
        ["Afrz"] = () => new RuntimeFreezingBreathAbility(),    // 冰冻吐息（冰龙，对建筑减速）
        // —— 暗夜单位补充 ——
        ["Apiv"] = () => new RuntimeInvisibleGhostAbility(),             // 永久隐身
        ["Agho"] = () => new RuntimeInvisibleGhostAbility(),             // 幽灵（阴影，永久隐身）
        ["Apsh"] = () => WithObjects<RuntimePhaseShiftAbility>(),        // 相位转移
        ["Acpf"] = () => new RuntimeCorporealFormAbility(),     // 实体形态（灵魂行者，取消虚无）
        ["Aimp"] = () => new RuntimeImpalingBoltAbility(),      // 穿刺之箭（弩车，直线穿透）
        ["Adcn"] = () => new MassDispel(),        // 驱散（灵魂行者）
        ["Afrb"] = () => new RuntimeFrostAttackAbility(),       // 霜冻吐息（冰龙，减速）
        ["Afr2"] = () => new RuntimeFrostAttackAbility(),       // 霜冻攻击（冰塔）
        ["Afra"] = () => new RuntimeFrostAttackAbility(),       // 霜冻攻击（冰霜巨龙）
        ["Adtg"] = () => new DetectPassive(),     // 通用反隐
        ["Avul"] = () => new RuntimeInvulnerableAbility(), // 常驻无敌（守卫 / 中立建筑）
        ["Atau"] = () => new RuntimeTauntAbility(),             // 嘲讽（山岭巨人）
        // —— 亡灵单位补充 ——
        ["Aam2"] = () => new AntiMagicShield(),   // 反魔法盾（女妖，吸收魔法伤害）
        ["Aams"] = () => WithObjects<RuntimeAntiMagicShellAbility>(),    // 反魔法外壳（女妖，90s 全魔法免疫）
        ["Abur"] = () => new RuntimeBurrowFormAbility(),            // 钻地（地穴恶魔，隐形禁攻）
        ["Apts"] = () => new RuntimePlagueTossAbility(),        // 投掷瘟疫（绞肉车，消耗尸体召 uplg）
        ["Astn"] = () => new RuntimeAlternateFormAbility(),         // 石像形态（石像鬼，护甲 + 定身）
        ["Auhf"] = () => new RuntimeUnholyFrenzyAbility(),      // 邪恶狂热（亡灵法师，攻速 + 掉血）
        ["Acri"] = () => WithObjects<RuntimeCrippleAbility>(),           // 残废（亡灵巫师）
        ["Acrs"] = () => new RuntimeCurseAbility(),             // 诅咒（女妖）
        ["Aps2"] = () => new RuntimeCharmAbility(),             // 占据（女妖，同魅惑）
        ["Apos"] = () => new RuntimePossessionAbility(),        // 占据（女妖，转属后消耗自身）
        ["Aeye"] = () => new RuntimeHealingWardAbility(),       // 岗哨守卫（萨满，召 oeye）
        ["Abu2"] = () => new RuntimeBurrowFormAbility(),            // 钻地（甲虫 2 级）
        ["Abu3"] = () => new RuntimeBurrowFormAbility(),            // 钻地（甲虫 3 级）
        ["Adt1"] = () => new DetectPassive(),     // 反隐（岗哨守卫）
        ["Adev"] = () => new Devour(),            // 吞噬（科多兽，吞非英雄单位）
        ["Aave"] = () => new RuntimeDestroyerFormAbility(),        // 毁灭者形态（雕像 ⇄ 毁灭者）
        ["Amil"] = () => new RuntimeDestroyerFormAbility(),        // 民兵（农民 ⇄ 民兵）
        ["Arpl"] = () => new RuntimeStatueReplenishAbility(),         // 补充生命（雕像）
        ["Arpm"] = () => new RuntimeStatueReplenishAbility(),         // 补充法力（雕像）
        // —— 野怪别名码 ——
        ["ACad"] = () => new RuntimeAnimateDeadAbility(),       // 野怪版操纵死尸
        ["ACam"] = () => new AntiMagicShield(),   // 野怪版反魔法盾
        ["ACbl"] = () => new RuntimeBloodlustAbility(),         // 野怪版嗜血
        ["ACcr"] = () => WithObjects<RuntimeCrippleAbility>(),           // 野怪版残废
        ["ACct"] = () => new CriticalStrike(),    // 野怪版致命一击
        ["ACen"] = () => new Ensnare(),           // 野怪版诱捕
        ["ACif"] = () => new RuntimeInnerFireAbility(),         // 野怪版内火
        ["ACmi"] = () => new RuntimeMagicImmunityAbility(), // 野怪版魔法免疫
        ["ACpy"] = () => new RuntimePolymorphAbility(),         // 野怪版变羊
        ["ACrk"] = () => new RuntimeResistantSkinAbility(),     // 野怪版抗性皮肤
        ["ACsk"] = () => new RuntimeResistantSkinAbility(),
        ["ACsw"] = () => new RuntimeSlowAbility(),          // 野怪版减速
        ["ACuf"] = () => new RuntimeUnholyFrenzyAbility(),      // 野怪版邪恶狂热
        ["Awfb"] = () => new RuntimeFireBoltAbility(),          // 野怪火球（伤害 + 眩晕）
        ["ACfb"] = () => new RuntimeFireBoltAbility(),
        ["Anh2"] = () => new Heal(),              // 野怪高级治疗
        ["Adsm"] = () => new MassDispel(),        // 野怪驱散
        ["ACc3"] = () => new RuntimeCarrionSwarmAbility(),      // 野怪震荡波
        ["ACm2"] = () => new RuntimeMagicImmunityAbility(), // 阿克蒙德魔法免疫
        ["SCc1"] = () => new RuntimeCycloneAbility(),           // 塞纳留斯旋风
        ["Scri"] = () => WithObjects<RuntimeCrippleAbility>(),           // 野怪术士残废
        ["Suhf"] = () => new RuntimeUnholyFrenzyAbility(),      // 野怪术士邪恶狂热
        ["Aclf"] = () => new CloudOfFog(),        // 云雾（龙鹰，区域未命中）
        ["Adtn"] = () => new RuntimeDetonateAbility(),          // 自爆（小精灵，驱散 + 伤害）
        ["Asds"] = () => new RuntimeKaboomAbility(),      // 自爆（野怪）
        ["Ambt"] = () => new RuntimeMoonWellReplenishAbility(),       // 法力电池（回蓝光环）
        ["Auns"] = () => new RuntimeUnsummonAbility(),          // 取消召唤（召回自己的召唤物）
        ["ACnr"] = WithObjects<RuntimeNeutralRegenAbility>,      // 中立恢复（只回血）
        ["ANre"] = WithObjects<RuntimeNeutralRegenAbility>,      // 中立恢复（只回蓝）
        ["Aamk"] = () => new RuntimeAttributeModSkillAbility(), // 属性修正（中立英雄）
        ["ACf3"] = () => new FingerOfDeath(),     // 痛苦之指（单体伤害）
        ["ACmo"] = () => new RuntimeMonsoonAbility(),           // 季风（引导范围伤害 + 减速）
        ["Amrf"] = () => new RuntimeDestroyerFormAbility(),        // 乌鸦形态（麦迪文）
        ["Amic"] = () => new RuntimeCallToArmsAbility(),         // 敲钟（城镇大厅，农民变民兵）
        ["Ahrp"] = () => new RuntimeRepairAbility(),     // 修理（人族农民）
        ["Arep"] = () => new RuntimeRepairAbility(),     // 修理（兽族苦工）
        ["AIcf"] = () => new CloakOfFlames(),      // 献祭斗篷（物品，持续炎伤）
        // —— 物品栏（单位获得格子的标记码） ——
        ["AInv"] = () => new InventoryPassive(),
        ["Aien"] = () => new InventoryPassive(),
        ["Aihn"] = () => new InventoryPassive(),
        ["Aion"] = () => new InventoryPassive(),
        ["Aiun"] = () => new InventoryPassive(),
        ["Apak"] = () => new InventoryPassive(),
        // —— 野怪别名：直接复用同名实现（数据列各自独立） ——
        ["ACac"] = WithObjects<RuntimeCommandAuraAbility>,          // 命令光环
        ["ACah"] = WithObjects<RuntimeThornsAuraAbility>,        // 荆棘光环
        ["ACat"] = () => new RuntimeTrueshotAuraAbility(),      // 强击光环
        ["ACav"] = () => new DevotionAura(),      // 专注光环
        ["ACba"] = WithObjects<RuntimeBrillianceAuraAbility>,    // 辉煌光环
        ["ACbb"] = () => new RuntimeBloodlustAbility(),         // 嗜血
        ["ACbc"] = () => new RuntimeBreathOfFireAbility(),      // 火焰吐息
        ["ACbf"] = () => new RuntimeFrostAttackAbility(),       // 霜冻吐息
        ["ACbh"] = () => new RuntimeBashAbility(),              // 重击
        ["ACbk"] = () => new RuntimeBlackArrowAbility(),        // 黑蚀箭
        ["ACbz"] = () => new RuntimeBlizzardAbility(),          // 暴风雪
        ["ACc2"] = () => new RuntimeCarrionSwarmAbility(),      // 震荡波
        ["ACca"] = () => new RuntimeCarrionSwarmAbility(),      // 腐臭蜂群
        ["ACce"] = () => new RuntimeCleavingAttackAbility(),    // 分裂攻击
        ["ACch"] = () => new RuntimeCharmAbility(),             // 魅惑
        ["ACcl"] = () => new ChainLightning(),    // 闪电链
        ["ACcn"] = () => new RuntimeCannibalizeAbility(),       // 食尸
        ["ACcs"] = () => new RuntimeCurseAbility(),             // 诅咒
        ["ACcv"] = () => new RuntimeCarrionSwarmAbility(),      // 震荡波
        ["ACcw"] = () => new RuntimeColdArrowsAbility(),        // 寒冰箭
        ["ACcy"] = () => new RuntimeCycloneAbility(),           // 旋风
        ["ACd2"] = () => new AbolishMagic(),      // 废除魔法
        ["ACdc"] = () => new RuntimeDeathCoilAbility(),         // 死亡缠绕
        ["ACde"] = () => new RuntimeDevourMagicAbility(),       // 吞噬魔法
        ["ACdm"] = () => new AbolishMagic(),      // 废除魔法
        ["ACdr"] = () => new LifeDrain(),         // 生命汲取
        ["ACds"] = () => WithObjects<RuntimeDivineShieldAbility>(),      // 神圣护甲
        ["ACdv"] = () => new Devour(),            // 吞噬
        ["ACev"] = () => new Evasion(),           // 闪避
        ["ACf2"] = () => new RuntimeFrostArmorAbility(),        // 霜冻护甲
        ["ACfd"] = () => new FingerOfDeath(),     // 死亡之指
        ["ACff"] = () => new RuntimeFaerieFireAbility(),        // 精灵之火
        ["ACfl"] = () => new RuntimeForkedLightningAbility(),   // 碎裂闪电
        ["ACfn"] = () => new RuntimeFrostNovaAbility(),         // 霜冻新星
        ["ACfr"] = () => new SummonUnit(),        // 自然之力
        ["AChv"] = () => new RuntimeHealingWaveAbility(),       // 治疗波
        ["AChw"] = () => new RuntimeHealingWardAbility(),       // 治疗守卫
        ["AChx"] = () => new RuntimeHexAbility(),               // 妖术
        ["ACim"] = () => new RuntimeImmolationAbility(),        // 献祭
        ["ACls"] = () => new RuntimeLightningShieldAbility(),   // 闪电盾
        ["ACps"] = () => new RuntimeCharmAbility(),             // 占据
        ["ACpu"] = () => new Purge(),             // 净化
        ["ACpv"] = () => new RuntimePulverizeAbility(),         // 粉碎
        ["ACr2"] = () => new RuntimeRejuvenationAbility(),      // 回春
        ["ACrd"] = () => new RaiseDead(),         // 复活死尸
        ["ACrf"] = () => WithObjects<RuntimeRainOfFireAbility>(),        // 火焰雨
        ["ACrj"] = () => new RuntimeRejuvenationAbility(),      // 回春
        ["ACrn"] = () => new RuntimeReincarnationAbility(),     // 重生
        ["ACro"] = () => new RuntimeRoarAbility(),              // 咆哮
        ["ACs7"] = () => new SummonUnit(),        // 野性之魂
        ["ACs9"] = () => new SummonUnit(),        // 野性之魂
        ["ACsa"] = () => new RuntimeFlamingArrowsAbility(),     // 灼热之箭
        ["ACsh"] = () => new RuntimeShockwaveAbility(),         // 震荡波
        ["ACsi"] = () => new Silence(),           // 沉默
        ["ACsl"] = WithObjects<RuntimeSleepAbility>,             // 睡眠
        ["ACss"] = () => new RuntimeShadowStrikeAbility(),      // 暗影突袭
        ["ACt2"] = () => new RuntimeThunderClapAbility(),       // 雷霆一击
        ["ACtb"] = () => new RuntimeStormBoltAbility(),       // 风暴之锤
        ["ACtc"] = () => new RuntimeThunderClapAbility(),       // 雷霆一击
        ["ACtn"] = () => new SerpentWard(),       // 毒蛇守卫
        ["ACua"] = WithObjects<RuntimeUnholyAuraAbility>,        // 邪恶光环
        ["ACvp"] = () => new VampiricAura(),      // 吸血光环
        ["ACvs"] = () => new RuntimeVenomSpearAbility(),       // 毒矛
        ["ACwb"] = () => new Web(),               // 蛛网
        ["ACwe"] = () => new SummonUnit(),        // 召唤海元素
        ["ANbf"] = () => new RuntimeBreathOfFireAbility(),      // 火焰吐息
        ["ANbh"] = () => new RuntimeBashAbility(),              // 重击
        ["ANbl"] = () => new Blink(),             // 闪烁
        ["ANcr"] = () => new Berserk(),           // 化学狂暴
        ["ANdb"] = () => new RuntimeDrunkenBrawlerAbility(),    // 醉拳
        ["ANdh"] = () => new RuntimeDrunkenHazeAbility(),       // 醉酒云雾
        ["ANef"] = () => new RuntimeSpiritOfVengeanceAbility(),    // 风暴大地火焰
        ["ANfb"] = () => new RuntimeFireBoltAbility(),          // 火球
        ["ANab"] = () => new RuntimeAcidBombAbility(),          // 酸性炸弹
        ["ANhs"] = () => new RuntimeHealingSprayAbility(),      // 治疗喷雾
        ["ANtm"] = () => new RuntimeTransmuteAbility(),         // 点金术
        ["ANcs"] = () => new RuntimeClusterRocketsAbility(),    // 集束火箭
        ["ANia"] = () => new RuntimeIncinerateAbility(),        // 焚化
        ["ANso"] = () => new RuntimeSoulBurnAbility(),          // 灵魂燃烧
        ["ANin"] = WithObjects<RuntimeInfernoAbility>,           // 地狱火
        ["ANlm"] = () => new SummonUnit(),        // 召唤熔岩
        ["ANr2"] = () => new RuntimeReincarnationAbility(),      // 重生
        ["ANsq"] = () => new SummonUnit(),        // 召唤豪猪
        ["ANst"] = () => new RuntimeStampedeAbility(),          // 兽群狂奔
        ["ANsw"] = () => new SummonUnit(),        // 召唤猎鹰
        ["ANt2"] = () => new RuntimeSpikedCarapaceAbility(),    // 荆棘盾
        ["ANth"] = () => new RuntimeSpikedCarapaceAbility(),    // 荆棘盾
        ["ANta"] = () => new RuntimeTauntAbility(),             // 嘲讽
        ["ANwk"] = () => new RuntimeWindWalkAbility(),          // 疾风步
        ["Aap3"] = () => new RuntimePlagueAuraAbility(),        // 瘟疫光环
        ["Abu5"] = () => new RuntimeBurrowFormAbility(),            // 钻地
        ["Ache"] = () => new MassDispel(),        // 连锁驱散
        ["Acht"] = () => new RuntimeHowlOfTerrorAbility(),      // 恐惧嚎叫
        ["Aenr"] = () => new RuntimeEntanglingRootsAbility(),   // 缠绕根须
        ["Aenw"] = () => new RuntimeEntanglingRootsAbility(),   // 缠绕海草
        ["Afbb"] = () => new RuntimeFeedbackAbility(),          // 反馈
        ["Ahid"] = () => new RuntimeShadowMeldAbility(),        // 影遁
        ["Ambb"] = () => new RuntimeManaBurnAbility(),          // 法力燃烧
        ["Ambd"] = () => new RuntimeManaBurnAbility(),          // 法力燃烧
        ["Anh1"] = () => new Heal(),              // 治疗
        ["ACcb"] = () => new FrostBolt(),         // 冰霜箭（伤害 + 减速）
        ["ANms"] = () => WithObjects<RuntimeManaShieldAbility>(),        // 法力盾（海巫）
        ["ANb2"] = () => new RuntimeBashAbility(),              // 重击（狂熊）
        ["ANtr"] = () => new DetectPassive(),     // 真实视域（战鹰）
        ["Andt"] = () => new RevealArea(),        // 中立反隐揭示
        ["Asdg"] = () => new RuntimeKaboomAbility(),      // 自爆（发条地精）
        ["Asd2"] = () => new RuntimeKaboomAbility(),
        ["Asd3"] = () => new RuntimeKaboomAbility(),
        ["AIgu"] = () => new RuntimeUltravisionAbility(),       // 超级视力神符
        ["ACmf"] = () => WithObjects<RuntimeManaShieldAbility>(),        // 法力盾（扣蓝抵伤）
        ["Aslp"] = () => new SummonUnit(),        // 召唤龙虾
        ["Aspd"] = () => new SummonUnit(),        // 召唤蜘蛛
        ["Aspt"] = () => new SummonUnit(),        // 孵化
        ["Aspy"] = () => new SummonUnit(),        // 召唤九头蛇
        ["Assp"] = () => new SummonUnit(),        // 召唤幼蛛
        ["Awrh"] = () => new RuntimeWarStompAbility(),          // 战争践踏
        ["Awrs"] = () => new RuntimeWarStompAbility(),          // 战争践踏
        ["SCae"] = () => new EnduranceAura(),     // 耐久光环
        ["Ashs"] = () => new DetectPassive(),     // 真实视域（物品）
        // —— 物品技能里与英雄同源的码 ——
        ["AIbx"] = () => new RuntimeBashAbility(),          // 物品版重击
        ["AIcs"] = () => new CriticalStrike(),// 物品版致命一击
        ["AIad"] = () => new DevotionAura(),  // 物品版专注光环
        ["AIgx"] = WithObjects<RuntimeLifeRegenerationAuraAbility>, // 物品版再生光环
        ["AIdv"] = () => WithObjects<RuntimeDivineShieldAbility>(),  // 物品版神圣护甲
        ["AIhl"] = () => new HolyLight(),     // 物品版圣光术
        // —— 物品主动：直接复用同源技能实现（目标形态跟着别家能力走） ——
        ["AIbk"] = () => new Blink(),         // 逃脱匕首（点地面，DataA 上限 1000 / DataB 最短 200）
        ["AIse"] = () => new Silence(),       // 沉默权杖（点地面 AoE）
        ["AIsi"] = () => new Silence(),       // 沉默杖（同名别名）
        ["AIps"] = () => new Purge(),         // 萨满图腾（点单位净化）
        ["AIwb"] = () => new Web(),           // 蜘蛛丝饰针（点空中单位）
        ["AIrr"] = () => new RuntimeRoarAbility(),          // 野兽卷轴（周围友军 +攻击）
        ["AIin"] = () => new RuntimeInfernoAbility(),       // 恶魔岩石（点地面召地狱火）
        ["AIrc"] = () => new RuntimeReincarnationAbility(),  // 重生十字章（装备后死亡自动复活）
        ["AIcy"] = () => new RuntimeCycloneAbility(),       // 风暴法杖（点敌人上天，Dur 20）
        ["AIco"] = () => new RuntimeCharmAbility(),         // 移主权杖（抢 DataA 级以内非英雄单位）
        ["AIdp"] = () => new RuntimeDeathPactAbility(),     // 死亡契约（物品版，献祭友军回血）
        ["AIfz"] = () => new FingerOfDeath(), // 死亡之指（物品版，DataC 直伤）
        ["AIcl"] = () => new ChainLightning(),// 闪电链（雷蜥钻石）
        ["AIxk"] = () => new Berserk(),       // 战渴头盔（狂暴）
        // —— 野怪 / 古树 / 召唤物自带（客户端 Runtime*Ability） ——
        ["Aeat"] = () => new RuntimeEatTreeAbility(),        // 吃树回血（古树与部分野怪）
        ["Afzy"] = () => new Frenzy(),         // 狂暴（攻速 + 移速，自动施放）
        ["Aasl"] = WithObjects<RuntimeSlowAuraAbility>,       // 缓慢光环（龙卷风）
        ["ANpi"] = () => new RuntimePermanentImmolationAbility(), // 永久献祭（地狱火）
        ["Apig"] = () => new RuntimePermanentImmolationAbility(), // 永久献祭（图形版）
        ["Atdg"] = () => WithObjects<RuntimeTornadoDamageAbility>(),  // 龙卷风每秒伤害（Atsp/Atwa 仍为占位）
        ["Agra"] = () => new RuntimeGrabTreeAbility(),       // 战棍（山岭巨人拔树：攻城攻击 + 射程）
        ["Aro1"] = () => new Root(),           // 扎根/拔根（古树）
        ["Aro2"] = () => new Root(),           // 扎根/拔根（远古守护者）
        ["AIpm"] = WithObjects<RuntimeItemPlaceMineAbility>,   // 布雷（物品）
        ["Amin"] = () => new Mine(),           // 地精地雷（踩到就引爆）
        ["Amnx"] = () => new MineBlast(),      // 地雷爆炸伤害（死亡触发）
        ["ANen"] = () => new Ensnare(),        // 诱捕（纳迦）
        ["Acny"] = () => new RuntimeCycloneAbility(),        // 旋风（纳迦）
        ["ACfu"] = () => new RuntimeFrostArmorAbility(),     // 霜冻护甲（纳迦自动施法）
        // —— 酒馆英雄：地精修补匠（客户端 Runtime*Ability 同款） ——
        ["ANrg"] = () => new RuntimeRoboGoblinAbility(),     // 机器人地精（Lv0）
        ["ANg1"] = () => new RuntimeRoboGoblinAbility(),     // 机器人地精（Lv1，工程升级后）
        ["ANg2"] = () => new RuntimeRoboGoblinAbility(),
        ["ANg3"] = () => new RuntimeRoboGoblinAbility(),
        ["ANeg"] = () => new RuntimeEngineeringUpgradeAbility(), // 工程升级（移速/攻击 + 技能升级）
        ["ANde"] = () => new RuntimeDemolishAbility(),       // 粉碎（对建筑倍率）
        ["ANd1"] = () => new RuntimeDemolishAbility(),
        ["ANd2"] = () => new RuntimeDemolishAbility(),
        ["ANd3"] = () => new RuntimeDemolishAbility(),
        ["ANsy"] = () => new RuntimePocketFactoryAbility(),  // 口袋工厂（Lv0）
        ["ANs1"] = () => new RuntimePocketFactoryAbility(),
        ["ANs2"] = () => new RuntimePocketFactoryAbility(),
        ["ANs3"] = () => new RuntimePocketFactoryAbility(),
        ["ANc1"] = () => new RuntimeClusterRocketsAbility(), // 火箭群（工程升级后的码）
        ["ANc2"] = () => new RuntimeClusterRocketsAbility(),
        ["ANc3"] = () => new RuntimeClusterRocketsAbility(),
        // —— 形态与升级（客户端 RuntimeCouple/RuntimeDecoupleAbility/AlternateForm） ——
        ["Aco2"] = () => new RuntimeCoupleAbility(),         // 弓箭手骑上角鹰兽
        ["Aco3"] = () => new RuntimeCoupleAbility(),         // 角鹰兽载起弓箭手
        ["Adec"] = () => new RuntimeDecoupleAbility(),       // 分离回弓箭手 + 角鹰兽
        ["Sbsk"] = () => new UnitTypeUpgrade(),// 猎头者研究完成后换狂暴战士
        ["Srtt"] = () => new UnitTypeUpgrade(),// 蒸汽机车研究完成后换升级机车
        ["Asb1"] = () => new RuntimeSubmergeAbility(),       // 潜水（穆戈尔）
        ["Asb2"] = () => new RuntimeSubmergeAbility(),       // 潜水（皇家卫兵）
        ["Asb3"] = () => new RuntimeSubmergeAbility(),       // 潜水（飞龙）
        ["Ansk"] = () => new RuntimeHardenedSkinAbility(),   // 硬化皮肤（龙龟）
        ["Aast"] = () => new RuntimeAncestralSpiritAbility(),// 牛头人祖灵（灵魂行者）
        // —— 绞肉车尸体货舱（客户端 RuntimeMeatCargoAbilities） ——
        ["Amel"] = () => new RuntimeMeatLoadAbility(),        // 装尸体（自动收半径内最近的尸体）
        ["Amed"] = () => new RuntimeMeatDropAbility(),         // 卸尸体（倒回车周围）
        ["Aexh"] = () => new RuntimeExhumeAbility(),           // 掘尸（定时产一具尸体进货舱）
        // —— 兽族地洞驻军（客户端 RuntimeBunkerAbility） ——
        ["Abun"] = () => new RuntimeBunkerAbility(),       // 地洞装/卸苦工
        ["Abtl"] = () => new RuntimeBattleStationsAbility(),   // 战斗平台（驻军才开火）
        ["Astd"] = () => new RuntimeStandDownAbility(),        // 放人出来
        ["Sbtl"] = () => new RuntimeBattleStationsAbility(),   // 战斗平台（混乱兽族地洞）
        // —— 载具货舱（客户端 RuntimeCargoHoldAbility） ——
        ["Sch2"] = () => new TransportHold(),    // 绞肉车尸体舱
        ["Sch3"] = () => new TransportHold(),    // 运输船 / 飞艇
        ["Sch5"] = () => new TransportHold(),    // 战船
        ["Advc"] = () => new TransportHold(),    // 吞噬者货舱
        ["Aenc"] = () => new TransportHold(),    // 金矿装载
        ["Achd"] = () => new TransportHold(),    // 尸体舱
        ["Slo2"] = () => new TransportHold(),    // 缠绕金矿装载
        ["Aloa"] = () => new RuntimeCargoLoadAbility(),   // 装载
        ["Slo3"] = () => new RuntimeCargoLoadAbility(),   // 战船装载
        ["Adro"] = () => new RuntimeCargoDropAbility(),   // 卸载全部
        ["Sdro"] = () => new RuntimeCargoDropAbility(),   // 战船卸载
        ["Adri"] = () => new RuntimeCargoDropAbility(),   // 瞬时卸载
        // —— 腐地（铺/压） ——
        ["Abgl"] = () => new RuntimeBlightGrowthAbility(),     // 腐地扩散（大）
        ["Abgs"] = () => new RuntimeBlightGrowthAbility(),     // 腐地扩散（小）
        ["Abdl"] = () => new BlightDispel(),     // 腐地净化（大）
        ["Abds"] = () => new BlightDispel(),     // 腐地净化（小）
        ["Abgm"] = () => new CAbilityBlightedGoldMine(),     // 被腐化的金矿
        // —— 修理变体与纳迦驱散 ——
        ["Aren"] = () => new RuntimeRepairAbility(),   // 更新（机械单位修理）
        ["Arst"] = () => new RuntimeRepairAbility(),   // 恢复（机械单位修理）
        ["Andm"] = () => new MassDispel(),       // 驱除魔法（纳迦）
        // —— 野怪/召唤物/战役 ——
        ["Awan"] = () => new RuntimeWanderAbility(),           // 漫游
        ["Agyd"] = () => new Graveyard(),        // 坟场产尸
        ["Anrc"] = () => new RuntimeRainOfChaosAbility(),      // 混乱之雨
        ["ANr3"] = () => new RuntimeRainOfChaosAbility(),      // 混乱之雨（按钮版）
        ["Andp"] = () => new RuntimeDarkPortalAbility(),       // 黑暗之门
        ["Anpa"] = () => new RuntimeParasiteAbility(),         // 寄生虫
        ["ACpa"] = () => new RuntimeParasiteAbility(),         // 寄生虫（艾瑞达）
        // —— 内部管线与标记（客户端同为内部被动对象，行为由订单/UI/世界系统承担） ——
        ["Ahar"] = () => new RuntimeHarvestAbility(),  // 采集（农民）
        ["Ahrl"] = () => new RuntimeHarvestAbility(),  // 伐木
        ["Awha"] = () => new RuntimeHarvestAbility(),  // 采集（小精灵）
        ["Aaha"] = () => new RuntimeHarvestAbility(),  // 采矿（侍僧）
        ["Anha"] = () => new RuntimeHarvestAbility(),  // 采集（纳迦）
        ["Ahr3"] = () => new RuntimeHarvestAbility(),  // 伐木（粉碎机）
        ["Argl"] = () => new RuntimeHarvestAbility(),  // 返还金/木
        ["Arlm"] = () => new RuntimeHarvestAbility(),  // 返还木材
        ["Aall"] = () => new RuntimeHarvestAbility(),  // 共享商店/联盟建筑
        ["Ane2"] = () => new RuntimeHarvestAbility(),  // 中立建筑（任意单位）
        ["Aneu"] = () => new RuntimeHarvestAbility(),  // 中立建筑
        ["Apit"] = () => new RuntimeHarvestAbility(),  // 商店购买
        ["Asid"] = () => new RuntimeHarvestAbility(),  // 出售物品
        ["Asud"] = () => new RuntimeHarvestAbility(),  // 出售单位
        ["Aral"] = () => new RuntimeHarvestAbility(),  // 集结点
        ["Aegm"] = () => new RuntimeHarvestAbility(),  // 缠绕金矿
        ["Agld"] = () => new RuntimeHarvestAbility(),  // 金矿
        ["Aeth"] = () => new RuntimeHarvestAbility(),  // 幽灵（可见）
        ["Aloc"] = () => new RuntimeHarvestAbility(),  // 蝗虫
        ["Aspa"] = () => new RuntimeHarvestAbility(),  // 蜘蛛攻击
        ["Asp1"] = () => new RuntimeHarvestAbility(),  // 球体（复仇之魂 1 级）
        ["Asph"] = () => new RuntimeHarvestAbility(),  // 球体
        ["Avng"] = () => new RuntimeHarvestAbility(),  // 复仇
        ["Arng"] = () => new RuntimeHarvestAbility(),  // 复仇（野怪）
        ["Arbr"] = () => new RuntimeHarvestAbility(),  // 加强型地洞
        ["Awrp"] = () => new RuntimeHarvestAbility(),  // 传送门
        ["Atol"] = () => new RuntimeHarvestAbility(),  // 生命之树挂接美术
        ["Aawa"] = () => new RuntimeHarvestAbility(),  // 立即复活英雄
        ["Agyb"] = () => new RuntimeHarvestAbility(),  // 飞行机器炸弹
        ["Asac"] = () => new RuntimeHarvestAbility(),  // 牺牲（牺牲深渊）
        ["Alam"] = () => new RuntimeHarvestAbility(),  // 牺牲（侍僧）
        ["Ahnl"] = () => new RuntimeHarvestAbility(),  // 空吼声（召唤者）
        ["Abdt"] = () => new RuntimeHarvestAbility(),  // 地穴反隐（飞行）
        ["Atsp"] = () => new RuntimeHarvestAbility(),  // 龙卷风旋转
        ["Atwa"] = () => new RuntimeHarvestAbility(),  // 龙卷风游走
        ["ANcl"] = () => new RuntimeHarvestAbility(),  // 伊利丹引导（战役）
    };

    /// <summary>召唤一个单位（客户端 RuntimeAbilityFactory.SpawnSummoned 的等价入口）。</summary>
    internal static Unit SpawnSummoned(War3ObjectData objects, Unit owner, string unitId,
        Vec2 position, float duration, string timedLifeRawcode = "BTLF", float? facing = null,
        Action<Unit> initialize = null, bool resolveCreationPlacement = true,
        bool permanentTimedLife = false, int? ownerPlayerId = null, int? ownerTeam = null,
        Func<Unit, bool> preparePlacement = null)
    {
        if (objects == null || owner?.World == null) return null;
        if (string.IsNullOrWhiteSpace(unitId) || unitId == "-" || unitId.Length < 4) return null;
        Unit summoned = UnitFactory.Create(objects, unitId[..4], ownerTeam ?? owner.Team,
            position, facing ?? owner.Facing);
        if (summoned == null) return null;
        summoned.PlayerId = ownerPlayerId ?? owner.PlayerId;
        if (preparePlacement != null && !preparePlacement(summoned)) return null;
        if (duration > 0f || permanentTimedLife) summoned.SummonRemaining = duration;
        initialize?.Invoke(summoned);
        if (resolveCreationPlacement) owner.World.SpawnViaCreateUnit(summoned);
        else owner.World.Spawn(summoned);
        return summoned;
    }

    public static bool IsImplemented(string id) => Map.ContainsKey(id);

    /// <summary>按技能 ID（找不到再按基类码）建实例；都不认识就返回占位实现。</summary>
    /// <summary>客户端技能类要构造参数里的物编对象；Launcher 侧暂用一个全局实例承接。</summary>
    public static War3ObjectData RuntimeObjects;

    static Ability WithObjects<T>() where T : Ability
    {
        var withData = typeof(T).GetConstructor(new[] { typeof(War3ObjectData) });
        return withData != null
            ? (T)withData.Invoke(new object[] { RuntimeObjects })
            : (T)Activator.CreateInstance(typeof(T));
    }

    public static string ResolveBaseCode(IRuntimeObjectData data)
    {
        if (data == null) return "";
        string code = data.GetString("code", 1, 0, null);
        return string.IsNullOrWhiteSpace(code) ? "" : code.Trim();
    }

    public static float ReadFloat(IRuntimeObjectData data, string field, int level, float fallback)
        => data?.GetFloat(field, Math.Max(1, level), 0, fallback) ?? fallback;

    public static int ReadInt(IRuntimeObjectData data, string field, int level, int fallback)
        => data?.GetInt(field, Math.Max(1, level), 0, fallback) ?? fallback;

    public static string ReadString(IRuntimeObjectData data, string field, int level, string fallback = "")
        => data?.GetString(field, Math.Max(1, level), 0, fallback) ?? fallback;

    public static TargetFlags ParseTargets(string rules)
    {
        TargetFlags flags = TargetFlags.None;
        if (string.IsNullOrWhiteSpace(rules)) return flags;
        foreach (string token in rules.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "ground": flags |= TargetFlags.Ground; break;
                case "air": flags |= TargetFlags.Air; break;
                case "structure": flags |= TargetFlags.Structure; break;
                case "debris": flags |= TargetFlags.Debris; break;
                case "item": flags |= TargetFlags.Item; break;
                case "ward": flags |= TargetFlags.Ward; break;
                case "tree": flags |= TargetFlags.Tree; break;
                case "wall": flags |= TargetFlags.Wall; break;
                case "enemy": flags |= TargetFlags.Enemy; break;
                case "neutral": flags |= TargetFlags.Neutral; break;
                case "friend": flags |= TargetFlags.Ally; break;
                case "self": flags |= TargetFlags.Self; break;
                case "notself": flags |= TargetFlags.NotSelf; break;
                case "player": flags |= TargetFlags.Player; break;
                case "ally": flags |= TargetFlags.Ally; break;
                case "dead": flags |= TargetFlags.Dead; break;
                case "organic": flags |= TargetFlags.Organic; break;
                case "vulnerable": flags |= TargetFlags.Vulnerable; break;
                case "invulnerable": flags |= TargetFlags.Invulnerable; break;
                case "nonancient": flags |= TargetFlags.NonAncient; break;
                case "nonhero": flags |= TargetFlags.NonHero; break;
                case "hero": flags |= TargetFlags.Hero; break;
                case "nonmagicimmune": flags |= TargetFlags.NonMagicImmune; break;
                case "alive": flags |= TargetFlags.Alive; break;
                case "ancient": flags |= TargetFlags.Ancient; break;
                case "mechanical": flags |= TargetFlags.Mechanical; break;
                case "sapper": flags |= TargetFlags.Sapper; break;
                case "nonsapper": flags |= TargetFlags.NonSapper; break;
            }
        }
        return flags;
    }

    public static string ReadTargetRules(IRuntimeObjectData data, int level)
    {
        string rules = ReadString(data, "targs", level);
        if (!string.IsNullOrEmpty(rules)) return rules;
        return ReadString(data, "targs1", level);
    }

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

    /// <summary>客户端给召唤物挂限时生命。</summary>
    public static void ApplyTimedLife(Unit unit, float seconds)
    {
        if (unit == null || seconds <= 0f) return;
        unit.SummonRemaining = seconds;
    }

}

/// <summary>未实现的技能占位：数据与图标都在，但没有任何效果。</summary>
public sealed class PassiveAbility : Ability
{
    public override bool IsPassive => true;

    public override bool OnCast(World world, Unit target, Vector3 point) => false;

}
