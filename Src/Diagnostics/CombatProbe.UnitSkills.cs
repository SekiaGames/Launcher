using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    private static void CheckMeleeUnitSkills()
    {
        GD.Print("==== 对战单位技能自检 ====");

        CheckSlowUnit();
        CheckInvisibility();
        CheckFeedback();
        CheckAbolishMagic();
        CheckPurge();
        CheckAntiMagicShell();
        CheckPossession();
        CheckHardenedSkin();
        CheckElunesGrace();
        CheckWeb();
        CheckCyclone();
        CheckFaerieFire();
        CheckBloodlust();
        CheckRoar();
        CheckReplenish();
        CheckPlagueAura();
        CheckOrbOfAnnihilation();
        CheckAbsorbMana();
        CheckMoonGlaive();
        CheckPillage();
        CheckWarDrums();
        CheckDetection();
        CheckSentinel();
        CheckCreepAbilities();
        CheckHeroAliases();
    }

    private static Unit ProbeUnit(World world, string typeId, int player, Vector3 position, params string[] abilities)
    {
        UnitType type = UnitCatalog.Get(typeId);
        if (type == null) return null;

        Unit unit = MakeUnit(type, player);
        unit.Position = position;
        unit.MaxMana = 200f;
        unit.Mana = 200f;
        world.Units.Add(unit);

        foreach (string id in abilities) unit.GiveAbility(id, 1);
        return unit;
    }

    private static void CheckSlowUnit()
    {
        UnitType sorceressType = UnitCatalog.Get("hsor");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        AbilityType slowType = AbilityCatalog.Get("Aslo");
        if (sorceressType == null || footmanType == null || slowType == null) return;

        var world = new World(null, null);
        Unit sorceress = ProbeUnit(world, "hsor", 0, Vector3.Zero, "Aslo");
        Unit footman = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));

        Ability slow = sorceress.FindAbility("Aslo");
        bool cast = world.TryCast(sorceress, slow, footman, footman.Position);

        Verify("减速施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("减速移速=DataA", slowType.DataAt(1, 'A'), footman.SlowMovePct, 0.001f);
        Verify("减速攻速=DataB", slowType.DataAt(1, 'B'), footman.SlowAttackPct, 0.001f);
        Verify("减速持续时间=Dur", slowType.DurationAt(1), footman.SlowRemaining, 0.01f);
        Verify("减速扣法力=Cost", 200f - slowType.CostAt(1), sorceress.Mana, 0.01f);
    }

    private static void CheckInvisibility()
    {
        UnitType sorceressType = UnitCatalog.Get("hsor");
        if (sorceressType == null) return;

        var world = new World(null, null);
        Unit sorceress = ProbeUnit(world, "hsor", 0, Vector3.Zero, "Aivs");
        Unit footman = ProbeUnit(world, "hfoo", 0, new Vector3(150f, 0f, 0f));

        Ability invisibility = sorceress.FindAbility("Aivs");
        bool cast = world.TryCast(sorceress, invisibility, footman, footman.Position);
        Verify("隐身施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("隐身状态生效", 1f, footman.State.Invisible ? 1f : 0f, 0f);

        world.ApplyLaunchModifiers(footman, null, 10f);
        Verify("出手后破隐", 0f, footman.State.Invisible ? 1f : 0f, 0f);
    }

    private static void CheckFeedback()
    {
        UnitType breakerType = UnitCatalog.Get("hspt");
        UnitType priestType = UnitCatalog.Get("hmpr");
        if (breakerType == null || priestType == null) return;

        var world = new World(null, null);
        Unit breaker = ProbeUnit(world, "hspt", 0, Vector3.Zero, "Afbk");
        Unit priest = ProbeUnit(world, "hmpr", 1, new Vector3(120f, 0f, 0f));
        priest.Mana = 200f;
        priest.MaxMana = 200f;

        Ability feedback = breaker.FindAbility("Afbk");
        float raw = feedback.ModifyOutgoingAttack(world, priest, 10f);
        float drained = Mathf.Min(200f, feedback.Type.DataAt(1, 'A'));

        Verify("反馈烧蓝=DataA", 200f - drained, priest.Mana, 0.01f);
        Verify("反馈加伤=烧蓝×DataB", 10f + drained, raw, 0.01f);
    }

    private static void CheckAbolishMagic()
    {
        UnitType dryadType = UnitCatalog.Get("edry");
        AbilityType abolishType = AbilityCatalog.Get("Aadm");
        if (dryadType == null || abolishType == null) return;

        var world = new World(null, null);
        Unit dryad = ProbeUnit(world, "edry", 0, Vector3.Zero, "Aadm");
        Unit footman = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));

        world.ApplyBuff(footman, new StatBuff { DamagePct = 0.5f }, 60f);
        Unit summon = ProbeUnit(world, "hfoo", 1, new Vector3(220f, 0f, 0f));
        summon.SummonRemaining = 60f;

        Ability abolish = dryad.FindAbility("Aadm");
        bool cast = world.TryCast(dryad, abolish, footman, footman.Position);
        Verify("废除魔法施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("敌方增益被清掉", 0f, footman.BonusDamagePct, 0.001f);

        float before = summon.Hp;
        world.TryCast(dryad, abolish, summon, summon.Position);
        Verify("召唤物吃 DataB 伤害", before - abolishType.DataAt(1, 'B'), summon.Hp, 0.01f);
    }

    private static void CheckPurge()
    {
        UnitType shamanType = UnitCatalog.Get("oshm");
        AbilityType purgeType = AbilityCatalog.Get("Apg2");
        if (shamanType == null || purgeType == null) return;

        var world = new World(null, null);
        Unit shaman = ProbeUnit(world, "oshm", 0, Vector3.Zero, "Apg2");
        Unit summon = ProbeUnit(world, "osw1", 1, new Vector3(300f, 0f, 0f));
        summon.SummonRemaining = 60f;
        summon.MaxHp = 2000f;
        summon.Hp = 2000f;

        float before = summon.Hp;
        Ability purge = shaman.FindAbility("Apg2");
        bool cast = world.TryCast(shaman, purge, summon, summon.Position);

        Verify("净化施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("净化对召唤物伤害=DataC", Mathf.Max(0f, before - purgeType.DataAt(1, 'C')), summon.Hp, 0.5f);
        Verify("净化初始满减速", 0.95f, summon.SlowMovePct, 0.01f);
        int moveStages = Mathf.Max(1, Mathf.RoundToInt(purgeType.DataAt(1, 'A')));
        for (int index = 0; index < 65; index++) world.TickAbilities();
        Verify("净化按 DataA 阶数恢复", 0.95f - 0.95f / moveStages, summon.SlowMovePct, 0.02f);

        // 萨满祭司用的码是 Aprg（Apg2 只是同名别名），除了减速还要把目标身上的增益驱散掉
        AbilityType shamanPurgeType = AbilityCatalog.Get("Aprg");
        if (shamanPurgeType == null) return;

        Unit shamanReal = ProbeUnit(world, "oshm", 0, new Vector3(600f, 0f, 0f), "Aprg");
        Unit buffed = ProbeUnit(world, "hfoo", 1, new Vector3(900f, 0f, 0f));
        world.ApplyBuff(buffed, new StatBuff { DamagePct = 0.5f }, 60f);
        bool real = world.TryCast(shamanReal, shamanReal.FindAbility("Aprg"), buffed, buffed.Position);
        Verify("净化(Aprg)施放成功", 1f, real ? 1f : 0f, 0f);
        Verify("净化把敌方增益驱散", 0f, buffed.BonusDamagePct, 0.001f);
        Verify("净化(Aprg)同样满减速", 0.95f, buffed.SlowMovePct, 0.01f);
    }

    /// <summary>Aams：对所有的魔法免疫，Dur=90s；魔法伤害进不来，物理伤害照旧。</summary>
    private static void CheckAntiMagicShell()
    {
        AbilityType shellType = AbilityCatalog.Get("Aams");
        if (shellType == null || UnitCatalog.Get("uban") == null) return;

        var world = new World(null, null);
        Unit banshee = ProbeUnit(world, "uban", 0, Vector3.Zero, "Aams");
        banshee.Mana = 300f;
        banshee.MaxMana = 300f;
        Unit footman = ProbeUnit(world, "hfoo", 0, new Vector3(200f, 0f, 0f));
        Unit enemy = ProbeUnit(world, "hfoo", 1, new Vector3(600f, 0f, 0f));

        bool cast = world.TryCast(banshee, banshee.FindAbility("Aams"), footman, footman.Position);
        Verify("反魔法外壳施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("外壳挂上 Buff", 1f, footman.FindBuff<AntiMagicShellBuff>() != null ? 1f : 0f, 0f);
        Verify("外壳期间魔法免疫", 1f, footman.State.MagicImmune ? 1f : 0f, 0f);

        footman.Hp = footman.MaxHp;
        world.DealDamage(World.MakeAbilityDamage(enemy, footman, 100f, AttackType.Spells, DamageType.Magic));
        Verify("魔法伤害被完全吸收", footman.MaxHp, footman.Hp, 0.01f);
        world.DealDamage(World.MakeAbilityDamage(enemy, footman, 100f, AttackType.Normal, DamageType.Normal));
        Verify("物理伤害照常结算", footman.MaxHp - 100f, footman.Hp, 0.01f);

        Buff shell = footman.FindBuff<AntiMagicShellBuff>();
        Verify("外壳持续 Dur", Mathf.Min(shellType.DurationAt(1), 90f), shell?.Remaining ?? 0f, 0.5f);
    }

    /// <summary>Apos：抢一个非英雄、非飞行、等级不超 DataA 的敌人，女妖自身被消耗。</summary>
    private static void CheckPossession()
    {
        AbilityType possessionType = AbilityCatalog.Get("Apos");
        if (possessionType == null || UnitCatalog.Get("uban") == null) return;

        var world = new World(null, null);
        Unit banshee = ProbeUnit(world, "uban", 0, Vector3.Zero, "Apos");
        banshee.Mana = 400f;
        banshee.MaxMana = 400f;
        Unit footman = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));

        Ability possession = banshee.FindAbility("Apos");
        Verify("英雄不能被占据", 0f, possession.CheckTarget(world,
            ProbeUnit(world, "Hpal", 1, new Vector3(300f, 0f, 0f)), Vector3.Zero) ? 1f : 0f, 0f);

        bool cast = world.TryCast(banshee, possession, footman, footman.Position);
        Verify("占据施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("目标转到我方", 0f, footman.Player, 0f);

        // 女妖的自毁延后两帧，避免施法管线收尾时对着已死单位扣法力
        for (int tick = 0; tick < 4; tick++) world.Advance();
        Verify("女妖代价生命归零", 1f, banshee.Alive ? 0f : 1f, 0f);
    }

    private static void CheckHardenedSkin()
    {
        UnitType giantType = UnitCatalog.Get("emtg");
        if (giantType == null) return;

        var world = new World(null, null);
        Unit giant = ProbeUnit(world, "emtg", 0, Vector3.Zero, "Assk");

        var info = new DamageInfo
        {
            Source = ProbeUnit(world, "hfoo", 1, new Vector3(100f, 0f, 0f)),
            Target = giant,
            Amount = 100f,
            DamageType = DamageType.Normal,
            IsAttack = true,
        };

        Ability skin = giant.FindAbility("Assk");
        skin.ModifyIncomingDamage(world, info);
        Verify("硬化皮肤减伤=DataC", 100f - skin.Type.DataAt(1, 'C'), info.Amount, 0.01f);

        info.Amount = 10f;
        skin.ModifyIncomingDamage(world, info);
        Verify("硬化皮肤保底=DataB", skin.Type.DataAt(1, 'B'), info.Amount, 0.01f);
    }

    private static void CheckElunesGrace()
    {
        UnitType archerType = UnitCatalog.Get("earc");
        if (archerType == null) return;

        var world = new World(null, null);
        Unit archer = ProbeUnit(world, "earc", 0, Vector3.Zero, "Aegr");

        var info = new DamageInfo
        {
            Source = ProbeUnit(world, "hfoo", 1, new Vector3(100f, 0f, 0f)),
            Target = archer,
            Amount = 100f,
            DamageType = DamageType.Normal,
            IsAttack = true,
            Ranged = true,
        };

        Ability grace = archer.FindAbility("Aegr");
        grace.ModifyIncomingDamage(world, info);
        Verify("艾露恩恩泽减伤=1-DataA", 100f * (1f - grace.Type.DataAt(1, 'A')), info.Amount, 0.01f);

        info.Amount = 100f;
        info.Ranged = false;
        grace.ModifyIncomingDamage(world, info);
        Verify("艾露恩恩泽不减免近战", 100f, info.Amount, 0.01f);
    }

    private static void CheckWeb()
    {
        UnitType fiendType = UnitCatalog.Get("ucry");
        UnitType copterType = UnitCatalog.Get("hgyr");
        if (fiendType == null || copterType == null) return;

        var world = new World(null, null);
        Unit fiend = ProbeUnit(world, "ucry", 0, Vector3.Zero, "Aweb");
        Unit copter = ProbeUnit(world, "hgyr", 1, new Vector3(300f, 0f, 0f));

        Ability web = fiend.FindAbility("Aweb");
        bool cast = world.TryCast(fiend, web, copter, copter.Position);
        Verify("蛛网施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("空中单位被定身", 1f, copter.State.Snared ? 1f : 0f, 0f);
    }

    private static void CheckCyclone()
    {
        UnitType druidType = UnitCatalog.Get("edot");
        AbilityType cycloneType = AbilityCatalog.Get("Acyc");
        if (druidType == null || cycloneType == null) return;

        var world = new World(null, null);
        Unit druid = ProbeUnit(world, "edot", 0, Vector3.Zero, "Acyc");
        Unit footman = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));

        Ability cyclone = druid.FindAbility("Acyc");
        bool cast = world.TryCast(druid, cyclone, footman, footman.Position);
        Verify("旋风施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("旋风期间不能行动", 1f, footman.State.Stunned ? 1f : 0f, 0f);
        Verify("旋风期间无敌", 1f, footman.State.Invulnerable ? 1f : 0f, 0f);
        Verify("旋风时长=Dur", cycloneType.DurationAt(1), footman.State.Remaining(StateMod.Pause), 0.01f);
    }

    private static void CheckFaerieFire()
    {
        UnitType druidType = UnitCatalog.Get("edot");
        AbilityType fireType = AbilityCatalog.Get("Afae");
        if (druidType == null || fireType == null) return;

        var world = new World(null, null);
        Unit druid = ProbeUnit(world, "edot", 0, Vector3.Zero, "Afae");
        Unit footman = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));
        float before = footman.BonusArmor;

        Ability fire = druid.FindAbility("Afae");
        bool cast = world.TryCast(druid, fire, footman, footman.Position);
        Verify("精灵之火施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("精灵之火减护甲=DataA", before - fireType.DataAt(1, 'A'), footman.BonusArmor, 0.01f);
    }

    private static void CheckBloodlust()
    {
        UnitType shamanType = UnitCatalog.Get("oshm");
        AbilityType lustType = AbilityCatalog.Get("Ablo");
        if (shamanType == null || lustType == null) return;

        var world = new World(null, null);
        Unit shaman = ProbeUnit(world, "oshm", 0, Vector3.Zero, "Ablo");
        Unit grunt = ProbeUnit(world, "ogru", 0, new Vector3(200f, 0f, 0f));
        float baseSpeed = World.SpeedOf(grunt);

        Ability lust = shaman.FindAbility("Ablo");
        bool cast = world.TryCast(shaman, lust, grunt, grunt.Position);
        Verify("嗜血施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("嗜血攻速=DataA", lustType.DataAt(1, 'A'), grunt.BonusAttackSpeedPct, 0.001f);
        Verify("嗜血移速=DataB", baseSpeed * lustType.DataAt(1, 'B'), World.SpeedOf(grunt) - baseSpeed, 0.01f);
    }

    private static void CheckRoar()
    {
        UnitType bearType = UnitCatalog.Get("edoc");
        AbilityType roarType = AbilityCatalog.Get("Ara2");
        if (bearType == null || roarType == null) return;

        var world = new World(null, null);
        Unit bear = ProbeUnit(world, "edoc", 0, Vector3.Zero, "Ara2");
        Unit ally = ProbeUnit(world, "earc", 0, new Vector3(300f, 0f, 0f));

        Ability roar = bear.FindAbility("Ara2");
        bool cast = world.TryCast(bear, roar, null, bear.Position);
        Verify("咆哮施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("咆哮加攻=DataA", roarType.DataAt(1, 'A'), ally.BonusDamagePct, 0.001f);
    }

    private static void CheckReplenish()
    {
        UnitType statueType = UnitCatalog.Get("uobs");
        AbilityType replenishType = AbilityCatalog.Get("Arpl");
        if (statueType == null || replenishType == null) return;

        var world = new World(null, null);
        Unit statue = ProbeUnit(world, "uobs", 0, Vector3.Zero, "Arpl");
        Unit ally = ProbeUnit(world, "ugho", 0, new Vector3(200f, 0f, 0f));
        ally.Hp = 100f;

        float before = ally.Hp;
        Ability replenish = statue.FindAbility("Arpl");
        bool cast = world.TryCast(statue, replenish, null, statue.Position);
        Verify("补充施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("补充回血=DataE", replenishType.DataAt(1, 'E'), ally.Hp - before, 0.01f);
    }

    private static void CheckPlagueAura()
    {
        UnitType wardType = UnitCatalog.Get("uplg");
        AbilityType plagueType = AbilityCatalog.Get("Aap2");
        if (wardType == null || plagueType == null) return;

        var world = new World(null, null);
        ProbeUnit(world, "uplg", 0, Vector3.Zero, "Aap2");
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(150f, 0f, 0f));

        float before = victim.Hp;
        for (int tick = 0; tick < 80; tick++) world.TickAbilities();

        Verify("瘟疫光环挂上中毒", 1f, victim.FindBuff<VenomBuff>() != null ? 1f : 0f, 0f);
        Verify("瘟疫每秒=DataB（3 秒 3 跳）", plagueType.DataAt(1, 'B') * 3f, before - victim.Hp, 0.6f);
    }

    private static void CheckOrbOfAnnihilation()
    {
        UnitType destroyerType = UnitCatalog.Get("ubsp");
        AbilityType orbType = AbilityCatalog.Get("Afak");
        if (destroyerType == null || orbType == null) return;

        var world = new World(null, null);
        Unit destroyer = ProbeUnit(world, "ubsp", 0, Vector3.Zero, "Afak");

        Ability orb = destroyer.FindAbility("Afak");
        float raw = orb.ModifyOutgoingAttack(world, ProbeUnit(world, "hfoo", 1, new Vector3(100f, 0f, 0f)), 20f);
        Verify("毁灭之球加伤=DataA", 20f + orbType.DataAt(1, 'A'), raw, 0.01f);
    }

    private static void CheckAbsorbMana()
    {
        UnitType destroyerType = UnitCatalog.Get("ubsp");
        if (destroyerType == null) return;

        var world = new World(null, null);
        Unit destroyer = ProbeUnit(world, "ubsp", 0, Vector3.Zero, "Aabs");
        destroyer.Mana = 0f;
        Unit target = ProbeUnit(world, "hmpr", 1, new Vector3(300f, 0f, 0f));
        target.Mana = 80f;
        target.MaxMana = 200f;

        Ability absorb = destroyer.FindAbility("Aabs");
        bool cast = world.TryCast(destroyer, absorb, target, target.Position);
        Verify("吸收法力施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("目标法力被抽干", 0f, target.Mana, 0.01f);
        Verify("法力转给施法者", 80f, destroyer.Mana, 0.01f);
    }

    private static void CheckMoonGlaive()
    {
        UnitType huntressType = UnitCatalog.Get("esen");
        if (huntressType == null) return;

        var world = new World(null, null);
        Unit huntress = ProbeUnit(world, "esen", 0, Vector3.Zero, "Amgl");
        Unit first = ProbeUnit(world, "hfoo", 1, new Vector3(100f, 0f, 0f));
        Unit second = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));

        var info = new DamageInfo { Source = huntress, Target = first, Amount = 40f, IsAttack = true };
        huntress.FindAbility("Amgl").OnAttackLanded(world, info);

        float expected = War3GameplayConfig.Resolve(info.Amount * 0.5f, AttackType.Pierce, second.Type.DefenseType,
            DamageType.Normal, 0f, false, false);
        GD.Print($"[月刃] 主目标 {first.Hp:0.0}/{first.MaxHp:0.0} 弹射目标 {second.Hp:0.0}/{second.MaxHp:0.0} 预期弹射伤害 {expected:0.00}");
        Verify("月刃弹射到第二个目标", first.Hp - expected, second.Hp, 0.5f);
    }

    private static void CheckPillage()
    {
        UnitType gruntType = UnitCatalog.Get("ogru");
        UnitType buildingType = UnitCatalog.Get("hbla");
        if (gruntType == null || buildingType == null) return;

        var world = new World(null, null);
        Unit grunt = ProbeUnit(world, "ogru", 0, Vector3.Zero, "Asal");
        Unit building = ProbeUnit(world, "hbla", 1, new Vector3(120f, 0f, 0f));

        int gold = 0;
        world.BountyAwarded += (player, amount, victim) => gold += amount;

        var info = new DamageInfo { Source = grunt, Target = building, Amount = 40f, IsAttack = true };
        grunt.FindAbility("Asal").OnAttackLanded(world, info);
        Verify("掠夺拆建筑得金", 1f, gold > 0 ? 1f : 0f, 0f);

        gold = 0;
        info.Target = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));
        grunt.FindAbility("Asal").OnAttackLanded(world, info);
        Verify("掠夺对非建筑无效", 0f, gold, 0f);
    }

    private static void CheckWarDrums()
    {
        UnitType kodoType = UnitCatalog.Get("okod");
        AbilityType drumsType = AbilityCatalog.Get("Aakb");
        if (kodoType == null || drumsType == null) return;

        var world = new World(null, null);
        ProbeUnit(world, "okod", 0, Vector3.Zero, "Aakb");
        Unit ally = ProbeUnit(world, "ogru", 0, new Vector3(300f, 0f, 0f));

        world.RefreshAuras();
        Verify("战鼓光环加攻=DataA", drumsType.DataAt(1, 'A'), ally.AuraDamagePct, 0.001f);
    }

    private static void CheckDetection()
    {
        UnitType shadeType = UnitCatalog.Get("ushd");
        if (shadeType == null) return;

        var world = new World(null, null);
        Unit shade = ProbeUnit(world, "ushd", 0, Vector3.Zero, "Atru");
        Unit hidden = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));
        hidden.State.Add(StateMod.Invisible, 60f);

        Verify("真实视域发现隐身", 1f, world.IsDetectedBy(0, hidden) ? 1f : 0f, 0f);

        hidden.Position = new Vector3(5000f, 0f, 0f);
        Verify("超出视域范围就看不见", 0f, world.IsDetectedBy(0, hidden) ? 1f : 0f, 0f);
    }

    private static void CheckSentinel()
    {
        UnitType huntressType = UnitCatalog.Get("esen");
        if (huntressType == null) return;

        var world = new World(null, null);
        Unit huntress = ProbeUnit(world, "esen", 0, Vector3.Zero, "Aesn");

        Ability sentinel = huntress.FindAbility("Aesn");
        bool cast = world.TryCast(huntress, sentinel, null, new Vector3(2000f, 0f, 1500f));
        Verify("哨兵施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("哨兵登记视野区域", 1f, world.Reveals.Count, 0f);
    }

    private static void CheckHeroAliases()
    {
        var aliases = new (string Code, Type Expected)[]
        {
            ("AOr3", typeof(Reincarnation)),
            ("AOs2", typeof(ShockWave)),
            ("AOw2", typeof(WarStomp)),
            ("AEIl", typeof(Metamorphosis)),
            ("ANhw", typeof(HealingWave)),
            ("ANhx", typeof(Hex)),
            ("Arsw", typeof(SerpentWard)),
            ("AOls", typeof(LocustSwarm)),
            ("Arsg", typeof(SummonUnit)),
            ("ANsb", typeof(ThunderBolt)),
        };

        int matched = 0;
        foreach ((string code, Type expected) in aliases)
        {
            AbilityType type = AbilityCatalog.Get(code);
            if (type == null) continue;
            Ability ability = SkillRegistry.Create(type);
            if (ability != null && expected.IsInstanceOfType(ability)) matched++;
        }
        Verify("英雄别名全部映射到位", aliases.Length, matched, 0f);
    }
}