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
        // —— 夜视、抗性皮肤与物品光环 ——
        ["Ault"] = () => new Ultravision(),       // 夜视（暗夜单位）
        ["AIuv"] = () => new Ultravision(),       // 物品夜视
        ["Arsk"] = () => new ResistantSkin(),     // 抗性皮肤（山岭巨人）
        ["AIba"] = () => new ItemAura(),          // 辉煌光环（物品）
        ["AIav"] = () => new ItemAura(),          // 吸血光环（物品）
        ["AIar"] = () => new ItemAura(),          // 强击光环（物品）
        ["AIae"] = () => new ItemAura(),          // 耐久光环（物品）
        ["AIau"] = () => new ItemAura(),          // 邪恶光环（物品）
        // —— 人族单位补充 ——
        ["Adef"] = () => new Defend(),           // 防御（步兵，远程减伤 50%）
        ["Ainf"] = () => new InnerFire(),        // 内火（牧师，攻击 +% / 护甲 +）
        ["Adis"] = () => new MassDispel(),       // 驱散魔法（牧师，区域驱散）
        ["Amls"] = () => new AerialShackles(),   // 空中枷锁（龙鹰，空中定身引导）
        ["Aflk"] = () => new FlakCannon(),       // 高射炮（直升机，对空溅射）
        ["Afsh"] = () => new FragShards(),       // 碎片（迫击炮，落地溅射）
        ["Aroc"] = () => new RocketAttack(),     // 火箭攻击（蒸汽机车，对空溅射）
        ["Asps"] = () => new SpellSteal(),       // 窃法（破法者，偷一个增益）
        ["Asth"] = () => new StormHammers(),     // 风暴之锤（狮鹫，攻击弹射）
        ["Apxf"] = () => new PhoenixFireAbility(), // 凤凰火焰（凤凰，攻击溅射）
        ["Amim"] = () => new MagicImmunityPassive(), // 魔法免疫（破法者 / 树妖 / 精灵龙）
        // —— 兽族单位补充 ——
        ["Aens"] = () => new Ensnare(),           // 诱捕（狼骑兵，定身）
        ["Aent"] = () => new Ensnare(),           // 缠绕（古树，定身）
        ["Ahwd"] = () => new HealingWard(),       // 治疗守卫（萨满，召 ohwd）
        ["Asta"] = () => new StasisTrap(),        // 静止陷阱（萨满，敌人靠近眩晕）
        ["Aspl"] = () => new SpiritLink(),        // 灵魂链（灵魂行者，伤害分摊）
        ["Afrz"] = () => new FreezingBreath(),    // 冰冻吐息（冰龙，对建筑减速）
        // —— 暗夜单位补充 ——
        ["Apiv"] = () => new Ghost(),             // 永久隐身
        ["Agho"] = () => new Ghost(),             // 幽灵（阴影，永久隐身）
        ["Apsh"] = () => new PhaseShift(),        // 相位转移
        ["Acpf"] = () => new CorporealForm(),     // 实体形态（灵魂行者，取消虚无）
        ["Aimp"] = () => new ImpalingBolt(),      // 穿刺之箭（弩车，直线穿透）
        ["Adcn"] = () => new MassDispel(),        // 驱散（灵魂行者）
        ["Afrb"] = () => new FrostBreath(),       // 霜冻吐息（冰龙，减速）
        ["Afr2"] = () => new FrostBreath(),       // 霜冻攻击（冰塔）
        ["Afra"] = () => new FrostBreath(),       // 霜冻攻击（冰霜巨龙）
        ["Adtg"] = () => new DetectPassive(),     // 通用反隐
        ["Avul"] = () => new InvulnerablePassive(), // 常驻无敌（守卫 / 中立建筑）
        ["Atau"] = () => new Taunt(),             // 嘲讽（山岭巨人）
        // —— 亡灵单位补充 ——
        ["Aam2"] = () => new AntiMagicShield(),   // 反魔法盾（女妖，吸收魔法伤害）
        ["Abur"] = () => new Burrow(),            // 钻地（地穴恶魔，隐形禁攻）
        ["Apts"] = () => new PlagueToss(),        // 投掷瘟疫（绞肉车，消耗尸体召 uplg）
        ["Astn"] = () => new StoneForm(),         // 石像形态（石像鬼，护甲 + 定身）
        ["Auhf"] = () => new UnholyFrenzy(),      // 邪恶狂热（亡灵法师，攻速 + 掉血）
        ["Acri"] = () => new Cripple(),           // 残废（亡灵巫师）
        ["Acrs"] = () => new Curse(),             // 诅咒（女妖）
        ["Aps2"] = () => new Charm(),             // 占据（女妖，同魅惑）
        ["Aeye"] = () => new HealingWard(),       // 岗哨守卫（萨满，召 oeye）
        ["Abu2"] = () => new Burrow(),            // 钻地（甲虫 2 级）
        ["Abu3"] = () => new Burrow(),            // 钻地（甲虫 3 级）
        ["Adt1"] = () => new DetectPassive(),     // 反隐（岗哨守卫）
        ["Adev"] = () => new Devour(),            // 吞噬（科多兽，吞非英雄单位）
        ["Aave"] = () => new TwoWayForm(),        // 毁灭者形态（雕像 ⇄ 毁灭者）
        ["Amil"] = () => new TwoWayForm(),        // 民兵（农民 ⇄ 民兵）
        ["Arpl"] = () => new Replenish(),         // 补充生命（雕像）
        ["Arpm"] = () => new Replenish(),         // 补充法力（雕像）
        // —— 野怪别名码 ——
        ["ACad"] = () => new AnimateDead(),       // 野怪版操纵死尸
        ["ACam"] = () => new AntiMagicShield(),   // 野怪版反魔法盾
        ["ACbl"] = () => new Bloodlust(),         // 野怪版嗜血
        ["ACcr"] = () => new Cripple(),           // 野怪版残废
        ["ACct"] = () => new CriticalStrike(),    // 野怪版致命一击
        ["ACen"] = () => new Ensnare(),           // 野怪版诱捕
        ["ACif"] = () => new InnerFire(),         // 野怪版内火
        ["ACmi"] = () => new MagicImmunityPassive(), // 野怪版魔法免疫
        ["ACpy"] = () => new Polymorph(),         // 野怪版变羊
        ["ACrk"] = () => new ResistantSkin(),     // 野怪版抗性皮肤
        ["ACsk"] = () => new ResistantSkin(),
        ["ACsw"] = () => new SlowUnit(),          // 野怪版减速
        ["ACuf"] = () => new UnholyFrenzy(),      // 野怪版邪恶狂热
        ["Awfb"] = () => new FireBolt(),          // 野怪火球（伤害 + 眩晕）
        ["ACfb"] = () => new FireBolt(),
        ["Anh2"] = () => new Heal(),              // 野怪高级治疗
        ["Adsm"] = () => new MassDispel(),        // 野怪驱散
        ["ACc3"] = () => new CarrionSwarm(),      // 野怪震荡波
        ["ACm2"] = () => new MagicImmunityPassive(), // 阿克蒙德魔法免疫
        ["SCc1"] = () => new Cyclone(),           // 塞纳留斯旋风
        ["Scri"] = () => new Cripple(),           // 野怪术士残废
        ["Suhf"] = () => new UnholyFrenzy(),      // 野怪术士邪恶狂热
        ["Aclf"] = () => new CloudOfFog(),        // 云雾（龙鹰，区域未命中）
        ["Adtn"] = () => new Detonate(),          // 自爆（小精灵，驱散 + 伤害）
        ["Asds"] = () => new SelfDestruct(),      // 自爆（野怪）
        ["Ambt"] = () => new ManaBattery(),       // 法力电池（回蓝光环）
        ["Auns"] = () => new Unsummon(),          // 取消召唤（召回自己的召唤物）
        ["ACnr"] = () => new NeutralRegen(),      // 中立恢复（只回血）
        ["ANre"] = () => new NeutralRegen(),      // 中立恢复（只回蓝）
        ["Aamk"] = () => new AttributeModifier(), // 属性修正（中立英雄）
        ["ACf3"] = () => new FingerOfDeath(),     // 痛苦之指（单体伤害）
        ["ACmo"] = () => new Monsoon(),           // 季风（引导范围伤害 + 减速）
        ["Amrf"] = () => new TwoWayForm(),        // 乌鸦形态（麦迪文）
        ["Amic"] = () => new CallToArms(),         // 敲钟（城镇大厅，农民变民兵）
        ["Ahrp"] = () => new RepairBuilding(),     // 修理（人族农民）
        ["Arep"] = () => new RepairBuilding(),     // 修理（兽族苦工）
        ["AIcf"] = () => new CloakOfFlames(),      // 献祭斗篷（物品，持续炎伤）
        // —— 物品栏（单位获得格子的标记码） ——
        ["AInv"] = () => new InventoryPassive(),
        ["Aien"] = () => new InventoryPassive(),
        ["Aihn"] = () => new InventoryPassive(),
        ["Aion"] = () => new InventoryPassive(),
        ["Aiun"] = () => new InventoryPassive(),
        ["Apak"] = () => new InventoryPassive(),
        // —— 野怪别名：直接复用同名实现（数据列各自独立） ——
        ["ACac"] = () => new WarDrums(),          // 命令光环
        ["ACah"] = () => new ThornsAura(),        // 荆棘光环
        ["ACat"] = () => new TrueshotAura(),      // 强击光环
        ["ACav"] = () => new DevotionAura(),      // 专注光环
        ["ACba"] = () => new BrillianceAura(),    // 辉煌光环
        ["ACbb"] = () => new Bloodlust(),         // 嗜血
        ["ACbc"] = () => new BreathOfFire(),      // 火焰吐息
        ["ACbf"] = () => new FrostBreath(),       // 霜冻吐息
        ["ACbh"] = () => new Bash(),              // 重击
        ["ACbk"] = () => new BlackArrow(),        // 黑蚀箭
        ["ACbz"] = () => new Blizzard(),          // 暴风雪
        ["ACc2"] = () => new CarrionSwarm(),      // 震荡波
        ["ACca"] = () => new CarrionSwarm(),      // 腐臭蜂群
        ["ACce"] = () => new CleavingAttack(),    // 分裂攻击
        ["ACch"] = () => new Charm(),             // 魅惑
        ["ACcl"] = () => new ChainLightning(),    // 闪电链
        ["ACcn"] = () => new Cannibalize(),       // 食尸
        ["ACcs"] = () => new Curse(),             // 诅咒
        ["ACcv"] = () => new CarrionSwarm(),      // 震荡波
        ["ACcw"] = () => new ColdArrows(),        // 寒冰箭
        ["ACcy"] = () => new Cyclone(),           // 旋风
        ["ACd2"] = () => new AbolishMagic(),      // 废除魔法
        ["ACdc"] = () => new DeathCoil(),         // 死亡缠绕
        ["ACde"] = () => new DevourMagic(),       // 吞噬魔法
        ["ACdm"] = () => new AbolishMagic(),      // 废除魔法
        ["ACdr"] = () => new LifeDrain(),         // 生命汲取
        ["ACds"] = () => new DivineShield(),      // 神圣护甲
        ["ACdv"] = () => new Devour(),            // 吞噬
        ["ACev"] = () => new Evasion(),           // 闪避
        ["ACf2"] = () => new FrostArmor(),        // 霜冻护甲
        ["ACfd"] = () => new FingerOfDeath(),     // 死亡之指
        ["ACff"] = () => new FaerieFire(),        // 精灵之火
        ["ACfl"] = () => new ForkedLightning(),   // 碎裂闪电
        ["ACfn"] = () => new FrostNova(),         // 霜冻新星
        ["ACfr"] = () => new SummonUnit(),        // 自然之力
        ["AChv"] = () => new HealingWave(),       // 治疗波
        ["AChw"] = () => new HealingWard(),       // 治疗守卫
        ["AChx"] = () => new Hex(),               // 妖术
        ["ACim"] = () => new Immolation(),        // 献祭
        ["ACls"] = () => new LightningShield(),   // 闪电盾
        ["ACps"] = () => new Charm(),             // 占据
        ["ACpu"] = () => new Purge(),             // 净化
        ["ACpv"] = () => new Pulverize(),         // 粉碎
        ["ACr2"] = () => new Rejuvenation(),      // 回春
        ["ACrd"] = () => new RaiseDead(),         // 复活死尸
        ["ACrf"] = () => new RainOfFire(),        // 火焰雨
        ["ACrj"] = () => new Rejuvenation(),      // 回春
        ["ACrn"] = () => new Reincarnation(),     // 重生
        ["ACro"] = () => new Roar(),              // 咆哮
        ["ACs7"] = () => new SummonUnit(),        // 野性之魂
        ["ACs9"] = () => new SummonUnit(),        // 野性之魂
        ["ACsa"] = () => new SearingArrows(),     // 灼热之箭
        ["ACsh"] = () => new ShockWave(),         // 震荡波
        ["ACsi"] = () => new Silence(),           // 沉默
        ["ACsl"] = () => new Sleep(),             // 睡眠
        ["ACss"] = () => new ShadowStrike(),      // 暗影突袭
        ["ACt2"] = () => new ThunderClap(),       // 雷霆一击
        ["ACtb"] = () => new ThunderBolt(),       // 风暴之锤
        ["ACtc"] = () => new ThunderClap(),       // 雷霆一击
        ["ACtn"] = () => new SerpentWard(),       // 毒蛇守卫
        ["ACua"] = () => new UnholyAura(),        // 邪恶光环
        ["ACvp"] = () => new VampiricAura(),      // 吸血光环
        ["ACvs"] = () => new VenomSpears(),       // 毒矛
        ["ACwb"] = () => new Web(),               // 蛛网
        ["ACwe"] = () => new SummonUnit(),        // 召唤海元素
        ["ANbf"] = () => new BreathOfFire(),      // 火焰吐息
        ["ANbh"] = () => new Bash(),              // 重击
        ["ANbl"] = () => new Blink(),             // 闪烁
        ["ANcr"] = () => new Berserk(),           // 化学狂暴
        ["ANdb"] = () => new DrunkenBrawler(),    // 醉拳
        ["ANdh"] = () => new DrunkenHaze(),       // 醉酒云雾
        ["ANef"] = () => new StormEarthFire(),    // 风暴大地火焰
        ["ANfb"] = () => new FireBolt(),          // 火球
        ["ANab"] = () => new AcidBomb(),          // 酸性炸弹
        ["ANhs"] = () => new HealingSpray(),      // 治疗喷雾
        ["ANtm"] = () => new Transmute(),         // 点金术
        ["ANcs"] = () => new ClusterRockets(),    // 集束火箭
        ["ANia"] = () => new Incinerate(),        // 焚化
        ["ANso"] = () => new SoulBurn(),          // 灵魂燃烧
        ["ANin"] = () => new Inferno(),           // 地狱火
        ["ANlm"] = () => new SummonUnit(),        // 召唤熔岩
        ["ANr2"] = () => new Reincarnation(),      // 重生
        ["ANsq"] = () => new SummonUnit(),        // 召唤豪猪
        ["ANst"] = () => new Stampede(),          // 兽群狂奔
        ["ANsw"] = () => new SummonUnit(),        // 召唤猎鹰
        ["ANt2"] = () => new SpikedCarapace(),    // 荆棘盾
        ["ANth"] = () => new SpikedCarapace(),    // 荆棘盾
        ["ANta"] = () => new Taunt(),             // 嘲讽
        ["ANwk"] = () => new WindWalk(),          // 疾风步
        ["Aap3"] = () => new PlagueAura(),        // 瘟疫光环
        ["Abu5"] = () => new Burrow(),            // 钻地
        ["Ache"] = () => new MassDispel(),        // 连锁驱散
        ["Acht"] = () => new HowlOfTerror(),      // 恐惧嚎叫
        ["Aenr"] = () => new EntanglingRoots(),   // 缠绕根须
        ["Aenw"] = () => new EntanglingRoots(),   // 缠绕海草
        ["Afbb"] = () => new Feedback(),          // 反馈
        ["Ahid"] = () => new ShadowMeld(),        // 影遁
        ["Ambb"] = () => new ManaBurn(),          // 法力燃烧
        ["Ambd"] = () => new ManaBurn(),          // 法力燃烧
        ["Anh1"] = () => new Heal(),              // 治疗
        ["ACcb"] = () => new FrostBolt(),         // 冰霜箭（伤害 + 减速）
        ["ANms"] = () => new ManaShield(),        // 法力盾（海巫）
        ["ANb2"] = () => new Bash(),              // 重击（狂熊）
        ["ANtr"] = () => new DetectPassive(),     // 真实视域（战鹰）
        ["Andt"] = () => new RevealArea(),        // 中立反隐揭示
        ["Asdg"] = () => new SelfDestruct(),      // 自爆（发条地精）
        ["Asd2"] = () => new SelfDestruct(),
        ["Asd3"] = () => new SelfDestruct(),
        ["AIgu"] = () => new Ultravision(),       // 超级视力神符
        ["ACmf"] = () => new ManaShield(),        // 法力盾（扣蓝抵伤）
        ["Aslp"] = () => new SummonUnit(),        // 召唤龙虾
        ["Aspd"] = () => new SummonUnit(),        // 召唤蜘蛛
        ["Aspt"] = () => new SummonUnit(),        // 孵化
        ["Aspy"] = () => new SummonUnit(),        // 召唤九头蛇
        ["Assp"] = () => new SummonUnit(),        // 召唤幼蛛
        ["Awrh"] = () => new WarStomp(),          // 战争践踏
        ["Awrs"] = () => new WarStomp(),          // 战争践踏
        ["SCae"] = () => new EnduranceAura(),     // 耐久光环
        ["Ashs"] = () => new DetectPassive(),     // 真实视域（物品）
        // —— 物品技能里与英雄同源的码 ——
        ["AIbx"] = () => new Bash(),          // 物品版重击
        ["AIcs"] = () => new CriticalStrike(),// 物品版致命一击
        ["AIad"] = () => new DevotionAura(),  // 物品版专注光环
        ["AIgx"] = () => new RegenerationAura(), // 物品版再生光环
        ["AIdv"] = () => new DivineShield(),  // 物品版神圣护甲
        ["AIhl"] = () => new HolyLight(),     // 物品版圣光术
        // —— 物品主动：直接复用同源技能实现（目标形态跟着别家能力走） ——
        ["AIbk"] = () => new Blink(),         // 逃脱匕首（点地面，DataA 上限 1000 / DataB 最短 200）
        ["AIse"] = () => new Silence(),       // 沉默权杖（点地面 AoE）
        ["AIsi"] = () => new Silence(),       // 沉默杖（同名别名）
        ["AIps"] = () => new Purge(),         // 萨满图腾（点单位净化）
        ["AIwb"] = () => new Web(),           // 蜘蛛丝饰针（点空中单位）
        ["AIrr"] = () => new Roar(),          // 野兽卷轴（周围友军 +攻击）
        ["AIin"] = () => new Inferno(),       // 恶魔岩石（点地面召地狱火）
        ["AIrc"] = () => new Reincarnation(),  // 重生十字章（装备后死亡自动复活）
        ["AIcy"] = () => new Cyclone(),       // 风暴法杖（点敌人上天，Dur 20）
        ["AIco"] = () => new Charm(),         // 移主权杖（抢 DataA 级以内非英雄单位）
        ["AIdp"] = () => new DeathPact(),     // 死亡契约（物品版，献祭友军回血）
        ["AIfz"] = () => new FingerOfDeath(), // 死亡之指（物品版，DataC 直伤）
        ["AIcl"] = () => new ChainLightning(),// 闪电链（雷蜥钻石）
        ["AIxk"] = () => new Berserk(),       // 战渴头盔（狂暴）
        // —— 野怪 / 古树 / 召唤物自带（客户端 Runtime*Ability） ——
        ["Aeat"] = () => new EatTree(),        // 吃树回血（古树与部分野怪）
        ["Afzy"] = () => new Frenzy(),         // 狂暴（攻速 + 移速，自动施放）
        ["Aasl"] = () => new SlowAura(),       // 缓慢光环（龙卷风）
        ["ANpi"] = () => new PermanentImmolation(), // 永久献祭（地狱火）
        ["Apig"] = () => new PermanentImmolation(), // 永久献祭（图形版）
        ["Atdg"] = () => new TornadoDamage(),  // 龙卷风每秒伤害（Atsp/Atwa 仍为占位）
        ["Agra"] = () => new GrabTree(),       // 战棍（山岭巨人拔树：攻城攻击 + 射程）
        ["Aro1"] = () => new Root(),           // 扎根/拔根（古树）
        ["Aro2"] = () => new Root(),           // 扎根/拔根（远古守护者）
        ["Amin"] = () => new Mine(),           // 地精地雷（踩到就引爆）
        ["Amnx"] = () => new MineBlast(),      // 地雷爆炸伤害（死亡触发）
        ["ANen"] = () => new Ensnare(),        // 诱捕（纳迦）
        ["Acny"] = () => new Cyclone(),        // 旋风（纳迦）
        ["ACfu"] = () => new FrostArmor(),     // 霜冻护甲（纳迦自动施法）
        // —— 酒馆英雄：地精修补匠（客户端 Runtime*Ability 同款） ——
        ["ANrg"] = () => new RoboGoblin(),     // 机器人地精（Lv0）
        ["ANg1"] = () => new RoboGoblin(),     // 机器人地精（Lv1，工程升级后）
        ["ANg2"] = () => new RoboGoblin(),
        ["ANg3"] = () => new RoboGoblin(),
        ["ANeg"] = () => new EngineeringUpgrade(), // 工程升级（移速/攻击 + 技能升级）
        ["ANde"] = () => new Demolish(),       // 粉碎（对建筑倍率）
        ["ANd1"] = () => new Demolish(),
        ["ANd2"] = () => new Demolish(),
        ["ANd3"] = () => new Demolish(),
        ["ANsy"] = () => new SummonFactory(),  // 口袋工厂（Lv0）
        ["ANs1"] = () => new SummonFactory(),
        ["ANs2"] = () => new SummonFactory(),
        ["ANs3"] = () => new SummonFactory(),
        ["ANc1"] = () => new ClusterRockets(), // 火箭群（工程升级后的码）
        ["ANc2"] = () => new ClusterRockets(),
        ["ANc3"] = () => new ClusterRockets(),
        // —— 形态与升级（客户端 RuntimeCouple/Decouple/AlternateForm） ——
        ["Aco2"] = () => new Couple(),         // 弓箭手骑上角鹰兽
        ["Aco3"] = () => new Couple(),         // 角鹰兽载起弓箭手
        ["Adec"] = () => new Decouple(),       // 分离回弓箭手 + 角鹰兽
        ["Sbsk"] = () => new UnitTypeUpgrade(),// 猎头者研究完成后换狂暴战士
        ["Srtt"] = () => new UnitTypeUpgrade(),// 蒸汽机车研究完成后换升级机车
        ["Asb1"] = () => new Submerge(),       // 潜水（穆戈尔）
        ["Asb2"] = () => new Submerge(),       // 潜水（皇家卫兵）
        ["Asb3"] = () => new Submerge(),       // 潜水（飞龙）
        ["Ansk"] = () => new HardenedSkin(),   // 硬化皮肤（龙龟）
        ["Aast"] = () => new AncestralSpirit(),// 牛头人祖灵（灵魂行者）
        // —— 绞肉车尸体货舱（客户端 RuntimeMeatCargoAbilities） ——
        ["Amel"] = () => new MeatCargo(),        // 装尸体（自动收半径内最近的尸体）
        ["Amed"] = () => new MeatDrop(),         // 卸尸体（倒回车周围）
        ["Aexh"] = () => new Exhume(),           // 掘尸（定时产一具尸体进货舱）
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
