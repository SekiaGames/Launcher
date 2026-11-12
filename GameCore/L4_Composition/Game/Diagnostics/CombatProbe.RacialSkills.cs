using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    /// <summary>兽族 / 暗夜 / 人族新增技能：直线伤害、AoE、光环、开关型、持续伤害。</summary>
    private static void CheckRacialSkills()
    {
        GD.Print("==== 兽族/暗夜/人族技能自检 ====");

        UnitType footmanType = UnitFactory.Get("hfoo");
        UnitType ghoulType = UnitFactory.Get("ugho");
        UnitType chieftainType = UnitFactory.Get("Otch");
        if (footmanType == null || ghoulType == null || chieftainType == null) return;

        // —— 震荡波：直线打地面单位与建筑，偏出宽度不吃 ——
        var waveWorld = new World(null, null);
        Unit tauren = MakeHero(chieftainType, 0, 3, waveWorld);
        var waveVictims = new List<Unit>();
        for (int index = 0; index < 2; index++)
        {
            Unit victim = MakeUnit(footmanType, 1);
            victim.Position = new Vector3(200f + index * 160f, 0f, 0f);
            waveWorld.Units.Add(victim);
            waveVictims.Add(victim);
        }
        Unit offWave = MakeUnit(footmanType, 1);
        offWave.Position = new Vector3(300f, 0f, 260f);
        waveWorld.Units.Add(offWave);
        waveWorld.Units.Add(tauren);

        Ability wave = tauren.GiveAbility("AOsh", 1);
        int waveHit = 0;
        float offWaveHp = offWave.Hp;
        bool waveCast = waveWorld.TryCast(tauren, wave, null, new Vector3(700f, 0f, 0f));
        foreach (Unit victim in waveVictims)
        {
            if (victim.Hp < victim.MaxHp) waveHit++;
        }
        Verify("震荡波施放成功", 1f, waveCast ? 1f : 0f, 0f);
        Verify("震荡波命中 2 个", 2f, waveHit, 0f);
        Verify("震荡波不打偏出的单位", offWaveHp, offWave.Hp, 0.01f);
        GD.Print($"[自检] 震荡波 命中 {waveHit} 个，偏出单位生命 {offWave.Hp:0}/{offWave.MaxHp:0}");

        // —— 战争践踏：AoE 伤害 + 眩晕 ——
        var stompWorld = new World(null, null);
        Unit stomper = MakeHero(chieftainType, 0, 3, stompWorld);
        var stompVictims = new List<Unit>();
        for (int index = 0; index < 3; index++)
        {
            Unit victim = MakeUnit(footmanType, 1);
            victim.Position = new Vector3(90f + index * 50f, 0f, 0f);
            stompWorld.Units.Add(victim);
            stompVictims.Add(victim);
        }
        stompWorld.Units.Add(stomper);

        Ability stomp = stomper.GiveAbility("AOws", 1);
        bool stompCast = stompWorld.TryCast(stomper, stomp, null, stomper.Position);
        int stomped = 0;
        foreach (Unit victim in stompVictims)
        {
            if (victim.Hp < victim.MaxHp && victim.State.Has(StateMod.Stun)) stomped++;
        }
        Verify("战争践踏施放成功", 1f, stompCast ? 1f : 0f, 0f);
        Verify("战争践踏 3 个全部眩晕", 3f, stomped, 0f);
        Verify("战争践踏眩晕 3 秒", 3f, stompVictims[0].State.Remaining(StateMod.Stun), 0.01f);
        GD.Print($"[自检] 战争践踏 眩晕 {stomped} 个，伤害 {stompVictims[0].MaxHp - stompVictims[0].Hp:0}");

        // —— 耐久光环：+10% 移速、+5% 攻速 ——
        var enduranceWorld = new World(null, null);
        Unit auraChief = MakeHero(chieftainType, 0, 3, enduranceWorld);
        Unit auraFriend = MakeUnit(footmanType, 0);
        auraFriend.Position = new Vector3(160f, 0f, 0f);
        enduranceWorld.Units.Add(auraChief);
        enduranceWorld.Units.Add(auraFriend);

        float speedWithout = World.SpeedOf(auraFriend);
        float cooldownWithout = World.AttackCooldownOf(auraFriend);
        auraChief.GiveAbility("AOae", 1);
        enduranceWorld.TickAbilities();
        Verify("耐久光环 +10% 移速", speedWithout * 1.1f, World.SpeedOf(auraFriend), 0.5f);
        Verify("耐久光环 +5% 攻速", cooldownWithout / 1.05f, World.AttackCooldownOf(auraFriend), 0.01f);
        GD.Print($"[自检] 耐久光环 移速 {speedWithout:0}→{World.SpeedOf(auraFriend):0}，"
            + $"攻击间隔 {cooldownWithout:0.00}→{World.AttackCooldownOf(auraFriend):0.00}");

        // —— 医疗波：130 → 97.5 → 73.125（每跳 -25%） ——
        var healWorld = new World(null, null);
        Unit shaman = MakeHero(UnitFactory.Get("Oshd") ?? chieftainType, 0, 3, healWorld);
        shaman.Position = new Vector3(0f, 0f, -300f);
        var healTargets = new List<Unit>();
        for (int index = 0; index < 3; index++)
        {
            Unit ally = MakeUnit(footmanType, 0);
            ally.Position = new Vector3(120f + index * 130f, 0f, 0f);
            ally.Hp = 50f;
            healWorld.Units.Add(ally);
            healTargets.Add(ally);
        }
        healWorld.Units.Add(shaman);

        Ability healingWave = shaman.GiveAbility("AOhw", 1);
        bool waveHeal = healWorld.TryCast(shaman, healingWave, healTargets[0], healTargets[0].Position);
        Verify("医疗波施放成功", 1f, waveHeal ? 1f : 0f, 0f);
        Verify("医疗波首目标 +130", 180f, healTargets[0].Hp, 0.5f);
        Verify("医疗波第二跳 +97.5", 147.5f, healTargets[1].Hp, 0.6f);
        Verify("医疗波第三跳 +73.125", 123.125f, healTargets[2].Hp, 0.8f);
        GD.Print($"[自检] 医疗波 三跳治疗 {healTargets[0].Hp - 50f:0.0} / {healTargets[1].Hp - 50f:0.0} / {healTargets[2].Hp - 50f:0.0}");

        // —— 毒蛇守卫：落点召出 osp1，存在 40 秒 ——
        var wardWorld = new World(null, null);
        wardWorld.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit shadowHunter = MakeHero(UnitFactory.Get("Oshd") ?? chieftainType, 0, 3, wardWorld);
        wardWorld.Units.Add(shadowHunter);

        Ability ward = shadowHunter.GiveAbility("AOsw", 1);
        bool wardCast = wardWorld.TryCast(shadowHunter, ward, null, new Vector3(300f, 0f, 0f));
        Unit wardUnit = null;
        foreach (Unit unit in wardWorld.Units)
        {
            if (unit.Type != null && unit.Type.Id.StartsWith("osp", StringComparison.OrdinalIgnoreCase)) wardUnit = unit;
        }
        Verify("毒蛇守卫施放成功", 1f, wardCast ? 1f : 0f, 0f);
        Verify("毒蛇守卫召出 osp1", 1f, wardUnit != null ? 1f : 0f, 0f);
        if (wardUnit != null) Verify("毒蛇守卫存在 40 秒", 40f, wardUnit.SummonRemaining, 0.01f);
        GD.Print($"[自检] 毒蛇守卫 召出 {(wardUnit != null ? wardUnit.Type.Id : "无")}，"
            + $"{(wardUnit != null ? wardUnit.SummonRemaining : 0f):0}s 后消失");

        // —— 树人：丛林守护者 AEfn 按 UnitID(efon) 召 DataA 个 ——
        var treantWorld = new World(null, null);
        treantWorld.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit treantKeeper = MakeHero(UnitFactory.Get("Ekee") ?? chieftainType, 0, 3, treantWorld);
        treantWorld.Units.Add(treantKeeper);

        Ability forceOfNature = treantKeeper.GiveAbility("AEfn", 1);
        bool treantCast = treantWorld.TryCast(treantKeeper, forceOfNature, null, new Vector3(200f, 0f, 0f));
        int treants = 0;
        foreach (Unit unit in treantWorld.Units)
        {
            if (unit.Type != null && string.Equals(unit.Type.Id, "efon", StringComparison.OrdinalIgnoreCase)) treants++;
        }
        Verify("自然之力施放成功", 1f, treantCast ? 1f : 0f, 0f);
        Verify("自然之力召出 2 个树人", 2f, treants, 0f);
        GD.Print($"[自检] 自然之力 召出 {treants} 个树人");

        // —— 纠缠根须：定身 + 每秒 15 伤害 ——
        var rootWorld = new World(null, null);
        Unit keeper = MakeHero(UnitFactory.Get("Ekee") ?? chieftainType, 0, 3, rootWorld);
        Unit rooted = MakeUnit(ghoulType, 1);
        rooted.Position = new Vector3(300f, 0f, 0f);
        rootWorld.Units.Add(keeper);
        rootWorld.Units.Add(rooted);

        Ability roots = keeper.GiveAbility("AEer", 1);
        bool rootedCast = rootWorld.TryCast(keeper, roots, rooted, rooted.Position);
        Verify("纠缠根须施放成功", 1f, rootedCast ? 1f : 0f, 0f);
        Verify("纠缠根须定身", 0f, rooted.State.CanMove ? 1f : 0f, 0f);

        float rootedHp = rooted.Hp;
        for (int index = 0; index < 20; index++) rootWorld.TickAbilities();
        float ticked = rootedHp - rooted.Hp;
        Verify("纠缠根须 1 秒掉 15 血", 15f, ticked, 1f);
        GD.Print($"[自检] 纠缠根须 定身 {rooted.State.Remaining(StateMod.Snare):0}s，1 秒掉血 {ticked:0}");

        // —— 荆棘光环：近战攻击反弹 10%（远程不反） ——
        var thornsWorld = new World(null, null);
        Unit thornKeeper = MakeHero(UnitFactory.Get("Ekee") ?? chieftainType, 0, 3, thornsWorld);
        Unit thornVictim = MakeUnit(ghoulType, 0);
        thornVictim.Position = new Vector3(150f, 0f, 0f);
        Unit thornAttacker = MakeUnit(ghoulType, 1);
        thornAttacker.Position = new Vector3(200f, 0f, 0f);
        thornsWorld.Units.Add(thornKeeper);
        thornsWorld.Units.Add(thornVictim);
        thornsWorld.Units.Add(thornAttacker);

        thornKeeper.GiveAbility("AEah", 1);
        thornsWorld.RefreshAuras();
        Verify("荆棘光环 10%", 0.1f, thornVictim.AuraThornsPct, 0.001f);

        float attackerHp = thornAttacker.Hp;
        float victimHp = thornVictim.Hp;
        thornsWorld.DealDamage(thornsWorld.MakeAttackDamage(thornAttacker, thornVictim, 100f));
        float victimDealt = victimHp - thornVictim.Hp;
        float expectedThorns = War3GameplayConfig.Default.Resolve(100f * 0.1f, AttackType.Spells,
            thornAttacker.Type.DefenseType, DamageType.Universal, 0f, false, false);
        Verify("荆棘光环反弹 10%", attackerHp - expectedThorns, thornAttacker.Hp, 0.5f);

        float meleeReflect = attackerHp - thornAttacker.Hp;
        attackerHp = thornAttacker.Hp;
        Weapon ranged = new() { MissileScene = "res://fake.tscn", MissileSpeed = 900f };
        thornsWorld.DealDamage(thornsWorld.MakeAttackDamage(thornAttacker, thornVictim, 100f, ranged));
        Verify("荆棘光环不反远程", attackerHp, thornAttacker.Hp, 0.01f);
        GD.Print($"[自检] 荆棘光环 近战挨 {victimDealt:0} 反弹 {meleeReflect:0}，远程不反弹");

        // —— 法力燃烧：烧 50 蓝、造成等量伤害 ——
        var burnWorld = new World(null, null);
        Unit demon = MakeHero(UnitFactory.Get("Edem") ?? chieftainType, 0, 3, burnWorld);
        Unit burner = MakeUnit(UnitFactory.Get("Hpal") ?? footmanType, 1);
        burner.Position = new Vector3(200f, 0f, 0f);
        burnWorld.Units.Add(demon);
        burnWorld.Units.Add(burner);
        burnWorld.RefreshHeroAttributes(burner);
        burner.Mana = 200f;
        burner.MaxMana = 200f;

        Ability burn = demon.GiveAbility("AEmb", 1);
        float burnHp = burner.Hp;
        bool burned = burnWorld.TryCast(demon, burn, burner, burner.Position);
        Verify("法力燃烧施放成功", 1f, burned ? 1f : 0f, 0f);
        Verify("法力燃烧 DataB 延迟前不扣蓝", 200f, burner.Mana, 0.01f);
        for (int tick = 0; tick < 8; tick++) burnWorld.Advance();
        Verify("法力燃烧烧掉 50 蓝", 150f, burner.Mana, 0.5f);
        float burnExpected = War3GameplayConfig.Default.Resolve(50f, AttackType.Spells, burner.Type.DefenseType,
            DamageType.Magic, 0f, false, false);
        Verify("法力燃烧造成等量伤害", burnHp - burnExpected, burner.Hp, 0.5f);
        GD.Print($"[自检] 法力燃烧 蓝 200→{burner.Mana:0}，血 {burnHp:0}→{burner.Hp:0}");

        // —— 献祭：开→每秒 10 伤害 + 7 蓝；关不花蓝 ——
        var immolationWorld = new World(null, null);
        Unit immolator = MakeHero(UnitFactory.Get("Edem") ?? chieftainType, 0, 3, immolationWorld);
        Unit immolated = MakeUnit(footmanType, 1);
        immolated.Position = new Vector3(100f, 0f, 0f);
        immolationWorld.Units.Add(immolator);
        immolationWorld.Units.Add(immolated);

        Ability immolation = immolator.GiveAbility("AEim", 1);
        float manaBefore = immolator.Mana;
        bool turnedOn = immolationWorld.TryCast(immolator, immolation, null, immolator.Position);
        Verify("献祭开启", 1f, turnedOn && immolation.ToggledOn ? 1f : 0f, 0f);
        Verify("献祭开启扣 25 蓝", manaBefore - 25f, immolator.Mana, 0.5f);

        float immolatedHp = immolated.Hp;
        for (int index = 0; index < 20; index++) immolationWorld.TickAbilities();
        float burnedHp = immolatedHp - immolated.Hp;
        Verify("献祭 1 秒 10 伤害", 10f, burnedHp, 1f);
        Verify("献祭 1 秒烧 7 蓝", manaBefore - 32f, immolator.Mana, 0.6f);

        float manaAtOff = immolator.Mana;
        bool turnedOff = immolationWorld.TryCast(immolator, immolation, null, immolator.Position);
        Verify("献祭关闭", 1f, turnedOff && !immolation.ToggledOn ? 1f : 0f, 0f);
        Verify("关闭不花法力", manaAtOff, immolator.Mana, 0.01f);
        GD.Print($"[自检] 献祭 1 秒 {burnedHp:0} 伤害、法力 {manaBefore:0}→{immolator.Mana:0}");

        // —— 刀阵旋风：周围 AoE ——
        var fanWorld = new World(null, null);
        Unit warden = MakeHero(UnitFactory.Get("Ewar") ?? chieftainType, 0, 3, fanWorld);
        var fanVictims = new List<Unit>();
        for (int index = 0; index < 3; index++)
        {
            Unit victim = MakeUnit(footmanType, 1);
            victim.Position = new Vector3(60f + index * 80f, 0f, 0f);
            fanWorld.Units.Add(victim);
            fanVictims.Add(victim);
        }
        fanWorld.Units.Add(warden);

        Ability fan = warden.GiveAbility("AEfk", 1);
        bool fanCast = fanWorld.TryCast(warden, fan, null, warden.Position);
        int fanned = 0;
        foreach (Unit victim in fanVictims)
        {
            if (victim.Hp < victim.MaxHp) fanned++;
        }
        Verify("刀阵旋风施放成功", 1f, fanCast ? 1f : 0f, 0f);
        Verify("刀阵旋风打到 3 个", 3f, fanned, 0f);
        GD.Print($"[自检] 刀阵旋风 命中 {fanned} 个，单体伤害 {fanVictims[0].MaxHp - fanVictims[0].Hp:0}");

        // —— 暗影突袭：先 75，之后每 3 秒 10 点，并减速 ——
        var strikeWorld = new World(null, null);
        Unit strikerHero = MakeHero(UnitFactory.Get("Ewar") ?? chieftainType, 0, 3, strikeWorld);
        Unit struck = MakeUnit(ghoulType, 1);
        struck.Position = new Vector3(200f, 0f, 0f);
        strikeWorld.Units.Add(strikerHero);
        strikeWorld.Units.Add(struck);

        Ability strike = strikerHero.GiveAbility("AEsh", 1);
        float struckHp = struck.Hp;
        bool struckCast = strikeWorld.TryCast(strikerHero, strike, struck, struck.Position);
        float firstHit = struckHp - struck.Hp;
        Verify("暗影突袭施放成功", 1f, struckCast ? 1f : 0f, 0f);
        Verify("暗影突袭初始伤害 > 0", 1f, firstHit > 0f ? 1f : 0f, 0f);
        Verify("暗影突袭减速 50%", 0.5f, struck.SlowMovePct, 0.001f);

        float dotBase = struck.Hp;
        for (int index = 0; index < 65; index++) strikeWorld.TickAbilities();
        float dot = dotBase - struck.Hp;
        Verify("暗影突袭 3 秒后跳一次 10 点", 10f, dot, 1.5f);
        GD.Print($"[自检] 暗影突袭 初始 {firstHit:0}，3 秒后持续伤害 {dot:0}，减速 {struck.SlowMovePct * 100f:0}%");

        // —— 辉煌光环：每秒多回 0.75 法力 ——
        var brillianceWorld = new World(null, null);
        Unit archMage = MakeHero(UnitFactory.Get("Hamg") ?? chieftainType, 0, 3, brillianceWorld);
        Unit casterFriend = MakeHero(UnitFactory.Get("Hpal") ?? chieftainType, 0, 3, brillianceWorld);
        casterFriend.Position = new Vector3(150f, 0f, 0f);
        brillianceWorld.Units.Add(archMage);
        brillianceWorld.Units.Add(casterFriend);

        archMage.GiveAbility("AHab", 1);
        brillianceWorld.TickAbilities();
        Verify("辉煌光环每秒 0.75 法力", 0.75f,
            casterFriend.ManaRegen - (casterFriend.Type?.ManaRegen ?? 0f), 0.001f);

        casterFriend.Mana = 10f;
        for (int index = 0; index < 20; index++)
        {
            brillianceWorld.TickAbilities();
            brillianceWorld.TickRegeneration();
        }
        Verify("辉煌光环 1 秒回蓝 > 0.75", 1f, casterFriend.Mana > 10.7f ? 1f : 0f, 0f);
        GD.Print($"[自检] 辉煌光环 法力 10→{casterFriend.Mana:0.00}");

        // —— 强击光环：只加远程攻击伤害（同样的随机种子，两套世界对比） ——
        UnitType archerType = UnitFactory.Get("earc");
        UnitType towerType = UnitFactory.Get("hbla");
        if (archerType != null && towerType != null)
        {
            float plain = MeasureArrowDamage(null, archerType, towerType);
            float buffed = MeasureArrowDamage(UnitFactory.Get("Ewar") ?? chieftainType, archerType, towerType);
            Verify("强击光环把远程伤害抬高 10%", plain * 1.1f, buffed, plain * 0.02f);
            GD.Print($"[自检] 强击光环 远程单箭 {plain:0.00}→{buffed:0.00}");
        }
    }

    /// <summary>建一套「弓箭手打建筑」的世界跑一次出手，返回造成的伤害；auraHero 非空时挂强击光环。</summary>
    private static float MeasureArrowDamage(UnitType auraHero, UnitType archerType, UnitType buildingType)
    {
        var world = new World(null, null);
        Unit archer = MakeUnit(archerType, 0);
        Unit building = MakeUnit(buildingType, 1);
        building.Position = new Vector3(200f, 0f, 0f);
        world.Units.Add(archer);
        world.Units.Add(building);

        if (auraHero != null)
        {
            Unit hero = MakeHero(auraHero, 0, 3, world);
            hero.Position = new Vector3(60f, 0f, 0f);
            world.Units.Add(hero);
            hero.GiveAbility("AEar", 1);
            world.RefreshAuras();
        }

        world.BeginAttack(archer, building);
        float before = building.Hp;
        for (int index = 0; index < 60; index++) world.TickWeapons();
        return before - building.Hp;
    }
}
