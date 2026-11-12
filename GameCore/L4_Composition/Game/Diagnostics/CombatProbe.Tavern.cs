using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    /// <summary>
    /// 酒馆英雄技能：火焰雨 / 龙卷风 / 碎裂闪电 / 冰霜箭（寒冰箭）。数值全部读物编。
    /// </summary>
    private static void CheckTavernSkills()
    {
        GD.Print("==== 酒馆英雄技能自检 ====");

        CheckRainOfFire();
        CheckTornado();
        CheckForkedLightning();
        CheckColdArrows();
    }

    private static void CheckRainOfFire()
    {
        UnitType lordType = UnitFactory.Get("Nplh");
        AbilityType rainType = AbilityCatalog.Get("ANrf");
        UnitType footmanType = UnitFactory.Get("hfoo");
        if (lordType == null || rainType == null || footmanType == null) return;

        var world = new World(null, null);
        Unit lord = MakeHero(lordType, 0, 3, world);
        lord.Mana = 400f;
        lord.MaxMana = 400f;
        world.Units.Add(lord);

        var spot = new Vector3(300f, 0f, 0f);
        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = spot;
        footman.MaxHp = 2000f;
        footman.Hp = 2000f;
        world.Units.Add(footman);

        Ability rain = lord.GiveAbility("ANrf", 1);
        bool cast = world.TryCast(lord, rain, null, spot);
        Verify("火焰雨施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("火焰雨进入引导", 1f, world.ChannelCount, 0f);

        float before = footman.Hp;
        for (int tick = 0; tick < 22; tick++) world.Advance();
        float firstWave = before - footman.Hp;
        Verify("火焰雨第一波≈DataB", rainType.DataAt(1, 'B'), firstWave, 5f);

        bool burning = false;
        foreach (Buff buff in footman.Buffs)
        {
            if (buff is PeriodicDamageBuff burn && ReferenceEquals(burn.SourceAbility, rain)) burning = true;
        }
        Verify("火焰雨挂上灼烧", 1f, burning ? 1f : 0f, 0f);

        for (int tick = 0; tick < 130; tick++) world.Advance();
        Verify("火焰雨波数结束后停止引导", 0f, world.ChannelCount, 0f);
        GD.Print($"[自检] 火焰雨 {rainType.DataAt(1, 'A'):0} 波×{rainType.DataAt(1, 'B'):0}"
            + $"（建筑×{rainType.DataAt(1, 'D'):0.0}）灼烧 {rainType.DataAt(1, 'E'):0}/s"
            + $" 持续 {rainType.DurationAt(1):0}s，首波打掉 {firstWave:0}");
    }

    private static void CheckTornado()
    {
        UnitType lordType = UnitFactory.Get("Nplh");
        AbilityType tornadoType = AbilityCatalog.Get("ANto");
        if (lordType == null || tornadoType == null) return;
        if (UnitFactory.Get("ntor") == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) =>
        {
            Unit created = MakeUnit(UnitFactory.Get(id), caster.Player);
            created.Position = position;
            return created;
        };

        Unit lord = MakeHero(lordType, 0, 6, world);
        lord.Mana = 600f;
        lord.MaxMana = 600f;
        world.Units.Add(lord);

        Ability tornado = lord.GiveAbility("ANto", 1);
        var spot = new Vector3(420f, 0f, 0f);
        bool cast = world.TryCast(lord, tornado, null, spot);
        Verify("龙卷风施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("龙卷风进入引导", 1f, world.ChannelCount, 0f);

        Unit summoned = null;
        foreach (Unit unit in world.Units)
        {
            if (unit.Type != null && unit.Type.Id == "ntor") summoned = unit;
        }
        Verify("龙卷风已召出", 1f, summoned != null ? 1f : 0f, 0f);
        if (summoned == null) return;
        Verify("龙卷风存续=Dur", tornadoType.DurationAt(1), summoned.SummonRemaining, 1f);

        world.InterruptChannel(lord);
        Verify("断引导后龙卷风消散", 0f, summoned.Alive ? 1f : 0f, 0f);
        GD.Print($"[自检] 龙卷风 {tornadoType.DurationAt(1):0}s 引导，落点({spot.X:0},{spot.Z:0})，断引导即散");
    }

    private static void CheckForkedLightning()
    {
        UnitType lordType = UnitFactory.Get("Nplh");
        UnitType footmanType = UnitFactory.Get("hfoo");
        AbilityType forkType = AbilityCatalog.Get("ANfl");
        if (lordType == null || footmanType == null || forkType == null) return;

        var world = new World(null, null);
        Unit lord = MakeHero(lordType, 0, 3, world);
        lord.Mana = 400f;
        lord.MaxMana = 400f;
        world.Units.Add(lord);

        Unit main = MakeUnit(footmanType, 1);
        main.Position = new Vector3(300f, 0f, 0f);
        main.MaxHp = 1000f;
        main.Hp = 1000f;
        world.Units.Add(main);

        Unit left = MakeUnit(footmanType, 1);
        left.Position = new Vector3(360f, 0f, 60f);
        left.MaxHp = 1000f;
        left.Hp = 1000f;
        world.Units.Add(left);

        Unit right = MakeUnit(footmanType, 1);
        right.Position = new Vector3(360f, 0f, -60f);
        right.MaxHp = 1000f;
        right.Hp = 1000f;
        world.Units.Add(right);

        Unit outside = MakeUnit(footmanType, 1);
        outside.Position = new Vector3(360f, 0f, 420f);
        outside.MaxHp = 1000f;
        outside.Hp = 1000f;
        world.Units.Add(outside);

        Ability fork = lord.GiveAbility("ANfl", 1);
        bool cast = world.TryCast(lord, fork, main, main.Position);
        Verify("碎裂闪电施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("主目标吃 DataA", 1000f - forkType.DataAt(1, 'A'), main.Hp, 5f);
        Verify("扇形内两个目标也吃到", 1f, left.Hp < 1000f && right.Hp < 1000f ? 1f : 0f, 0f);
        Verify("扇形外的目标没吃到", 1f, Mathf.IsEqualApprox(outside.Hp, 1000f) ? 1f : 0f, 0f);
        GD.Print($"[自检] 碎裂闪电 最多 {forkType.DataAt(1, 'B'):0} 个 ×{forkType.DataAt(1, 'A'):0}"
            + $" 长度 {forkType.DataAt(1, 'C'):0} 末端宽 {forkType.DataAt(1, 'D'):0}"
            + $"（命中 {main.Hp:0}/{left.Hp:0}/{right.Hp:0}，扇形外 {outside.Hp:0}）");
    }

    private static void CheckColdArrows()
    {
        UnitType footmanType = UnitFactory.Get("hfoo");
        UnitType rangerType = UnitFactory.Get("Nbrn");
        AbilityType coldType = AbilityCatalog.Get("ANfa");
        if (footmanType == null || rangerType == null || coldType == null) return;

        var world = new World(null, null);
        Unit caster = MakeUnit(footmanType, 0);
        caster.Mana = 100f;
        caster.MaxMana = 100f;
        world.Units.Add(caster);

        Ability cold = caster.GiveAbility("ANfa", 1);
        Unit victim = MakeUnit(footmanType, 1);
        world.Units.Add(victim);

        string coldArt = world.PrepareArrowMissile(caster, victim);
        float modified = world.ApplyAttackModifiers(caster, victim, 50f);
        Verify("冰霜箭附加伤害=DataA", 50f + coldType.DataAt(1, 'A'), modified, 0.01f);
        Verify("冰霜箭换成自己的箭模型", 1f, coldArt != null ? 1f : 0f, 0f);
        Verify("冰霜箭扣蓝=Cost", 100f - coldType.CostAt(1), caster.Mana, 0.01f);
        Verify("冰霜箭移速减速=DataB", coldType.DataAt(1, 'B'), victim.SlowMovePct, 0.001f);
        Verify("冰霜箭攻速减速=DataC", coldType.DataAt(1, 'C'), victim.SlowAttackPct, 0.001f);
        Verify("普通单位减速时长=Dur", coldType.DurationAt(1), victim.SlowRemaining, 0.01f);

        Unit heroVictim = MakeHero(rangerType, 1, 1, world);
        world.Units.Add(heroVictim);
        caster.Mana = 100f;
        world.PrepareArrowMissile(caster, heroVictim);
        world.ApplyAttackModifiers(caster, heroVictim, 50f);
        Verify("英雄减速时长=HeroDur", coldType.HeroDurationAt(1), heroVictim.SlowRemaining, 0.01f);
        GD.Print($"[自检] 冰霜箭 +{coldType.DataAt(1, 'A'):0} 伤害 / {coldType.CostAt(1):0} 蓝，"
            + $"减速 {coldType.DataAt(1, 'B'):P0} 移速 / {coldType.DataAt(1, 'C'):P0} 攻速，"
            + $"普通 {coldType.DurationAt(1):0}s 英雄 {coldType.HeroDurationAt(1):0.0}s");
    }
}