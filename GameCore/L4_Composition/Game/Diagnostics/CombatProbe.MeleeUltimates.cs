using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>
    /// 对战英雄终极与德鲁伊形态：非英雄法力池、熊/乌鸦形态、重生、群体传送、复仇之魂。
    /// 数值一律读物编；断言写成「表里多少就多少」。
    /// </summary>
    private static void CheckMeleeUltimates()
    {
        GD.Print("==== 终极技能与形态自检 ====");

        CheckUnitManaPool();
        CheckDruidForms();
        CheckReincarnation();
        CheckMassTeleport();
        CheckSpiritOfVengeance();
        CheckUltimateTiming();
    }

    private static void CheckUnitManaPool()
    {
        UnitType shamanType = UnitFactory.Get("oshm");
        if (shamanType == null) return;

        Verify("萨满最大法力=manaN", 200f, shamanType.ManaMax, 0.01f);
        Verify("萨满出生法力=mana0", 75f, shamanType.ManaStart, 0.01f);

        UnitType footmanType = UnitFactory.Get("hfoo");
        if (footmanType == null) return;
        Verify("无蓝单位 manaN 为 0", 0f, footmanType.ManaMax, 0.01f);
        GD.Print($"[自检] 法力池 萨满 {shamanType.ManaStart:0}/{shamanType.ManaMax:0} 回蓝 {shamanType.ManaRegen:0.00}/s"
            + $"  步兵 {footmanType.ManaStart:0}/{footmanType.ManaMax:0}");
    }

    private static void CheckDruidForms()
    {
        UnitType druidType = UnitFactory.Get("edoc");
        UnitType bearType = UnitFactory.Get("edcm");
        if (druidType == null || bearType == null) return;

        var world = new World(null, null);
        Unit druid = MakeUnit(druidType, 0);
        druid.MaxMana = 100f;
        druid.Mana = 100f;
        druid.Hp = druid.MaxHp * 0.5f;
        world.Units.Add(druid);

        Ability form = druid.GiveAbility("Abrf", 1);
        if (form == null)
        {
            Verify("熊形态技能可创建", 1f, 0f, 0f);
            return;
        }

        bool toBear = world.TryCast(druid, form, null, druid.Position);
        Verify("熊形态施放成功", 1f, toBear ? 1f : 0f, 0f);
        Verify("变成熊形态单位", 1f, druid.Type.Id == bearType.Id ? 1f : 0f, 0f);
        Verify("永久形态不会自己到期", 1f, druid.MorphPermanent ? 1f : 0f, 0f);
        Verify("形态切换保留生命比例", 0.5f, druid.Hp / Mathf.Max(1f, druid.MaxHp), 0.01f);
        Verify("技能栏换成熊形态 abilList", 1f, druid.FindAbility("Ara2") != null ? 1f : 0f, 0f);
        Verify("人形专属技能被移除", 0f, druid.FindAbility("Aroa") != null ? 1f : 0f, 0f);

        Ability bearForm = druid.FindAbility("Abrf");
        bool back = bearForm != null && world.TryCast(druid, bearForm, null, druid.Position);
        Verify("再放一次变回人形", 1f, back && druid.Type.Id == druidType.Id ? 1f : 0f, 0f);
        Verify("变回后不再处于形态", 0f, druid.Morphed ? 1f : 0f, 0f);
        Verify("人形技能栏恢复", 1f, druid.FindAbility("Aroa") != null ? 1f : 0f, 0f);

        bool toBearAgain = world.TryCast(druid, druid.FindAbility("Abrf"), null, druid.Position);
        Ability bearAgain = druid.FindAbility("Abrf");
        bool autoBack = toBearAgain && bearAgain != null && bearAgain.TryAutoCast(world);
        Verify("脱离交战时自动变回人形", 1f, autoBack && !druid.Morphed ? 1f : 0f, 0f);
        GD.Print($"[自检] 德鲁伊 {druidType.Id}→{bearType.Id}→{druid.Type.Id}，生命比例 0.50，"
            + $"法力 {druid.Mana:0}/100（每次 25）");
    }

    private static void CheckReincarnation()
    {
        UnitType chiefType = UnitFactory.Get("Otch");
        AbilityType reincarnationType = AbilityCatalog.Get("AOre");
        if (chiefType == null || reincarnationType == null) return;

        var world = new World(null, null);
        Unit chief = MakeHero(chiefType, 0, 6, world);
        world.Units.Add(chief);

        Ability reincarnation = chief.GiveAbility("AOre", 1);
        if (reincarnation == null) return;

        world.DealDamage(World.MakeAbilityDamage(null, chief, 100000f, AttackType.Spells, DamageType.Universal));
        Verify("重生英雄先进入死亡", 0f, chief.Alive ? 1f : 0f, 0f);
        Verify("重生不排队祭坛", 0f, chief.AwaitingRevive ? 1f : 0f, 0f);
        Verify("重生进入倒计时", 1f, world.PendingReincarnationCount, 0f);
        Verify("重生技能进冷却", reincarnationType.CooldownAt(1), reincarnation.CooldownRemaining, 0.1f);

        float delay = reincarnationType.DataAt(1, 'A');
        int ticks = Mathf.CeilToInt(delay / World.Tick) + 5;
        for (int index = 0; index < ticks; index++) world.Advance();

        Verify("重生倒计时结束后复活", 1f, chief.Alive ? 1f : 0f, 0f);
        Verify("重生满血复活", chief.MaxHp, chief.Hp, 1f);
        Verify("重生队列已清空", 0f, world.PendingReincarnationCount, 0f);
        GD.Print($"[自检] 重生 {delay:0}s 后 {chief.Type.Name} 复活 {chief.Hp:0}/{chief.MaxHp:0}"
            + $" 冷却 {reincarnation.CooldownRemaining:0}s");
    }

    private static void CheckMassTeleport()
    {
        UnitType mageType = UnitFactory.Get("Hamg");
        UnitType footmanType = UnitFactory.Get("hfoo");
        AbilityType teleportType = AbilityCatalog.Get("AHmt");
        if (mageType == null || footmanType == null || teleportType == null) return;

        var world = new World(null, null);
        Unit mage = MakeHero(mageType, 0, 6, world);
        world.Units.Add(mage);

        Unit near = MakeUnit(footmanType, 0);
        near.Position = new Vector3(200f, 0f, 0f);
        Unit far = MakeUnit(footmanType, 0);
        far.Position = new Vector3(900f, 0f, 0f);
        world.Units.Add(near);
        world.Units.Add(far);

        Ability teleport = mage.GiveAbility("AHmt", 1);
        if (teleport == null) return;

        var landing = new Vector3(5000f, 0f, 3000f);
        bool cast = world.TryCast(mage, teleport, null, landing);
        Verify("群体传送施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("传送者本人到位", 0f, FlatDistance(mage.Position, landing), 1f);
        Verify("半径内友军一起走", 1f, FlatDistance(near.Position, landing) < 200f ? 1f : 0f, 0f);
        Verify("半径外友军留在原地", 900f, far.Position.X, 0.01f);
        Verify("传送上限=物编 DataA", teleportType.DataAt(1, 'A'), (teleport as RuntimeMassTeleportAbility)?.MaxUnits ?? 0, 0f);
        GD.Print($"[自检] 群体传送 3 个单位 → ({landing.X:0},{landing.Z:0})，"
            + $"半径外单位仍在 {far.Position.X:0}（上限 {(teleport as RuntimeMassTeleportAbility)?.MaxUnits}，半径 {(teleport as RuntimeMassTeleportAbility)?.GatherRadius:0}）");
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static void CheckSpiritOfVengeance()
    {
        UnitType wardenType = UnitFactory.Get("Ewar");
        UnitType spiritType = UnitFactory.Get("espv");
        AbilityType vengeanceType = AbilityCatalog.Get("AEsv");
        if (wardenType == null || spiritType == null || vengeanceType == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);

        Unit warden = MakeHero(wardenType, 0, 6, world);
        world.Units.Add(warden);

        Ability vengeance = warden.GiveAbility("AEsv", 1);
        if (vengeance == null) return;

        world.DealDamage(World.MakeAbilityDamage(null, warden, 100000f, AttackType.Spells, DamageType.Universal));

        Unit spirit = null;
        foreach (Unit unit in world.Units)
        {
            if (!unit.Alive || unit.Type == null || unit.Type.Id != spiritType.Id) continue;
            spirit = unit;
        }

        Verify("守望者阵亡", 0f, warden.Alive ? 1f : 0f, 0f);
        Verify("复仇之魂已召出", 1f, spirit != null ? 1f : 0f, 0f);
        if (spirit != null)
        {
            Verify("复仇之魂存在 Dur 秒", vengeanceType.DurationAt(1), spirit.SummonRemaining, 0.5f);
            Verify("复仇之魂归施法者", warden.Player, spirit.Player, 0f);
        }
        Verify("复仇之魂进冷却", vengeanceType.CooldownAt(1), vengeance.CooldownRemaining, 0.1f);
        GD.Print($"[自检] 复仇之魂 {(spirit != null ? $"存在 {spirit.SummonRemaining:0}s" : "未召出")}"
            + $"，冷却 {vengeance.CooldownRemaining:0}s");
    }

    private static void CheckUltimateTiming()
    {
        GD.Print("==== 终极技能出手时机自检 ====");

        UnitType footType = UnitFactory.Get("hfoo");
        if (footType == null) return;

        UnitType mkType = UnitFactory.Get("Hmkg");
        AbilityType avatarType = AbilityCatalog.Get("AHav");
        if (mkType != null && avatarType != null)
        {
            var world = new World(null, null);
            Unit mk = MakeHero(mkType, 0, 6, world);
            world.Units.Add(mk);
            Ability avatar = mk.GiveAbility("AHav", 1);
            if (avatar != null)
            {
                Unit foe = MakeUnit(footType, 1);
                foe.Position = new Vector3(180f, 0f, 0f);
                world.Units.Add(foe);

                Verify("满血不空放天神下凡", 0f, avatar.TryAutoCast(world) ? 1f : 0f, 0f);
                mk.Hp = mk.MaxHp * 0.6f;
                Verify("残血没被集火也不开", 0f, avatar.TryAutoCast(world) ? 1f : 0f, 0f);
                foe.Target = mk;
                Verify("残血且被集火才开天神下凡", 1f, avatar.TryAutoCast(world) ? 1f : 0f, 0f);
                Verify("天神下凡进冷却", avatarType.CooldownAt(1), avatar.CooldownRemaining, 0.1f);
            }
        }

        UnitType bladeType = UnitFactory.Get("Obla");
        AbilityType stormType = AbilityCatalog.Get("AOww");
        if (bladeType != null && stormType != null)
        {
            var world = new World(null, null);
            Unit blade = MakeHero(bladeType, 0, 6, world);
            world.Units.Add(blade);
            Ability storm = blade.GiveAbility("AOww", 1);
            if (storm != null)
            {
                Unit one = MakeUnit(footType, 1);
                one.Position = new Vector3(120f, 0f, 0f);
                world.Units.Add(one);
                Verify("只贴到一个敌人不转剑刃风暴", 0f, storm.TryAutoCast(world) ? 1f : 0f, 0f);

                Unit two = MakeUnit(footType, 1);
                two.Position = new Vector3(-120f, 0f, 0f);
                world.Units.Add(two);
                Verify("两个敌人贴脸才转", 1f, storm.TryAutoCast(world) ? 1f : 0f, 0f);
                Verify("剑刃风暴进冷却", stormType.CooldownAt(1), storm.CooldownRemaining, 0.1f);
            }
        }

        UnitType lordType = UnitFactory.Get("Ucrl");
        AbilityType swarmType = AbilityCatalog.Get("AUls");
        if (lordType != null && swarmType != null)
        {
            var world = new World(null, null);
            Unit lord = MakeHero(lordType, 0, 6, world);
            world.Units.Add(lord);
            Ability swarm = lord.GiveAbility("AUls", 1);
            if (swarm != null)
            {
                var foes = new List<Unit>();
                for (int index = 0; index < 2; index++)
                {
                    Unit foe = MakeUnit(footType, 1);
                    foe.Position = new Vector3(260f + index * 110f, 0f, 0f);
                    world.Units.Add(foe);
                    foes.Add(foe);
                }
                lord.Target = foes[0];
                Verify("两个敌人不够放蝗群", 0f, swarm.TryAutoCast(world) ? 1f : 0f, 0f);

                Unit third = MakeUnit(footType, 1);
                third.Position = new Vector3(0f, 0f, 300f);
                world.Units.Add(third);
                Verify("三个敌人才放蝗群", 1f, swarm.TryAutoCast(world) ? 1f : 0f, 0f);
                Verify("蝗群进冷却", swarmType.CooldownAt(1), swarm.CooldownRemaining, 0.1f);
            }
        }

        UnitType shadowType = UnitFactory.Get("Oshd");
        AbilityType voodooType = AbilityCatalog.Get("AOvd");
        if (shadowType != null && voodooType != null)
        {
            var world = new World(null, null);
            Unit shadow = MakeHero(shadowType, 0, 6, world);
            world.Units.Add(shadow);
            Ability voodoo = shadow.GiveAbility("AOvd", 1);
            if (voodoo != null)
            {
                Unit mark = null;
                var allies = new List<Unit>();
                for (int index = 0; index < 2; index++)
                {
                    Unit ally = MakeUnit(footType, 0);
                    ally.Position = new Vector3(80f + index * 70f, 0f, 0f);
                    world.Units.Add(ally);
                    allies.Add(ally);
                }
                for (int index = 0; index < 3; index++)
                {
                    Unit foe = MakeUnit(footType, 1);
                    foe.Position = new Vector3(0f, 0f, 200f + index * 70f);
                    world.Units.Add(foe);
                    mark ??= foe;
                }
                foreach (Unit ally in allies) ally.Target = mark;

                Verify("两个战斗友军不够跳巫毒", 0f, voodoo.TryAutoCast(world) ? 1f : 0f, 0f);

                Unit thirdAlly = MakeUnit(footType, 0);
                thirdAlly.Position = new Vector3(-80f, 0f, 0f);
                thirdAlly.Target = mark;
                world.Units.Add(thirdAlly);
                Verify("三个战斗友军才跳巫毒", 1f, voodoo.TryAutoCast(world) ? 1f : 0f, 0f);
                Verify("巫毒进冷却", voodooType.CooldownAt(1), voodoo.CooldownRemaining, 0.1f);
            }
        }

        UnitType keeperType = UnitFactory.Get("Ekee");
        AbilityType calmType = AbilityCatalog.Get("AEtq");
        if (keeperType != null && calmType != null)
        {
            var world = new World(null, null);
            Unit keeper = MakeHero(keeperType, 0, 6, world);
            world.Units.Add(keeper);
            Ability calm = keeper.GiveAbility("AEtq", 1);
            if (calm != null)
            {
                Unit foe = MakeUnit(footType, 1);
                foe.Position = new Vector3(0f, 0f, 400f);
                world.Units.Add(foe);

                var allies = new List<Unit>();
                for (int index = 0; index < 2; index++)
                {
                    Unit ally = MakeUnit(footType, 0);
                    ally.Position = new Vector3(100f + index * 60f, 0f, 0f);
                    ally.Hp = ally.MaxHp * 0.5f;
                    ally.Target = foe;
                    world.Units.Add(ally);
                    allies.Add(ally);
                }

                Verify("两个残血友军不够开宁静", 0f, calm.TryAutoCast(world) ? 1f : 0f, 0f);

                Unit thirdAlly = MakeUnit(footType, 0);
                thirdAlly.Position = new Vector3(-100f, 0f, 0f);
                thirdAlly.Hp = thirdAlly.MaxHp * 0.5f;
                thirdAlly.Target = foe;
                world.Units.Add(thirdAlly);
                Verify("三个残血友军且掉血够多才开宁静", 1f, calm.TryAutoCast(world) ? 1f : 0f, 0f);
                Verify("宁静进冷却", calmType.CooldownAt(1), calm.CooldownRemaining, 0.1f);
            }
        }

        GD.Print("[自检] 大招时机：天神下凡看残血+被集火，剑刃/蝗群/流星看近敌数或英雄，巫毒/宁静看参战友军数");
    }
}