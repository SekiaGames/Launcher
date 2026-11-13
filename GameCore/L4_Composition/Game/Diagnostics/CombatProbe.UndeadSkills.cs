using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    /// <summary>亡灵四英雄（死亡骑士/巫妖/恐惧魔王/地穴领主）与光环、反伤自检。</summary>
    private static void CheckUndeadSkills()
    {
        GD.Print("==== 亡灵英雄技能自检 ====");

        UnitType deathKnightType = UnitFactory.Get("Udea");
        UnitType ghoulType = UnitFactory.Get("ugho");
        UnitType footmanType = UnitFactory.Get("hfoo");
        if (deathKnightType == null || ghoulType == null || footmanType == null) return;

        // —— 死亡缠绕：亡灵友军回血、活物敌人掉血，都要等弹道飞到 ——
        var world = new World(null, null);
        Unit knight = MakeHero(deathKnightType, 0, 3, world);
        Unit ghoul = MakeUnit(ghoulType, 0);
        ghoul.Position = new Vector3(200f, 0f, 0f);
        ghoul.Hp = 100f;
        Unit enemy = MakeUnit(footmanType, 1);
        enemy.Position = new Vector3(-200f, 0f, 0f);
        world.Units.Add(knight);
        world.Units.Add(ghoul);
        world.Units.Add(enemy);

        Ability coil = knight.GiveAbility("AUdc", 1);
        float ghoulHp = ghoul.Hp;
        Verify("死亡缠绕对亡灵友军施放", 1f, world.TryCast(knight, coil, ghoul, ghoul.Position) ? 1f : 0f, 0f);
        Verify("死亡缠绕飞行中还没回血", ghoulHp, ghoul.Hp, 0.01f);
        for (int index = 0; index < 20; index++) world.TickAbilityMissiles();
        Verify("死亡缠绕命中后回 200", ghoulHp + 200f, ghoul.Hp, 0.5f);

        coil.CooldownRemaining = 0f;
        knight.Mana = knight.MaxMana;
        float enemyHp = enemy.Hp;
        Verify("死亡缠绕对活物敌人施放", 1f, world.TryCast(knight, coil, enemy, enemy.Position) ? 1f : 0f, 0f);
        for (int index = 0; index < 20; index++) world.TickAbilityMissiles();
        Verify("死亡缠绕命中敌人扣血", 1f, enemy.Hp < enemyHp ? 1f : 0f, 0f);
        GD.Print($"[自检] 死亡缠绕 亡灵 {ghoulHp:0}→{ghoul.Hp:0}，敌人 {enemyHp:0}→{enemy.Hp:0}");

        // —— 死亡契约：献祭友军，按它最大生命的 100% 回血 ——
        var pactWorld = new World(null, null);
        Unit pactKnight = MakeHero(deathKnightType, 0, 3, pactWorld);
        Unit offering = MakeUnit(ghoulType, 0);
        offering.Position = new Vector3(150f, 0f, 0f);
        pactWorld.Units.Add(pactKnight);
        pactWorld.Units.Add(offering);
        pactKnight.Hp = 200f;

        Ability pact = pactKnight.GiveAbility("AUdp", 1);
        float expectedHeal = offering.MaxHp * 1f;
        bool pacted = pactWorld.TryCast(pactKnight, pact, offering, offering.Position);
        Verify("死亡契约施放成功", 1f, pacted ? 1f : 0f, 0f);
        Verify("死亡契约献祭掉友军", 0f, offering.Alive ? 1f : 0f, 0f);
        Verify("死亡契约按最大生命回血", Mathf.Min(pactKnight.MaxHp, 200f + expectedHeal), pactKnight.Hp, 0.5f);
        GD.Print($"[自检] 死亡契约 献祭 {offering.MaxHp:0} 血的食尸鬼，死亡骑士 200→{pactKnight.Hp:0}");

        // —— 黑暗仪式：按最大生命的 33% 换法力 ——
        var ritualWorld = new World(null, null);
        Unit lich = MakeHero(UnitFactory.Get("Ulic") ?? deathKnightType, 0, 3, ritualWorld);
        Unit fodder = MakeUnit(ghoulType, 0);
        fodder.Position = new Vector3(120f, 0f, 0f);
        ritualWorld.Units.Add(lich);
        ritualWorld.Units.Add(fodder);
        lich.Mana = 100f;

        Ability ritual = lich.GiveAbility("AUdr", 1);
        float expectedMana = 100f - ritual.ManaCost + fodder.MaxHp * 0.33f;
        bool didRitual = ritualWorld.TryCast(lich, ritual, fodder, fodder.Position);
        Verify("黑暗仪式施放成功", 1f, didRitual ? 1f : 0f, 0f);
        Verify("黑暗仪式换到 33% 法力", expectedMana, lich.Mana, 0.5f);
        GD.Print($"[自检] 黑暗仪式 法力 100→{lich.Mana:0}（祭品 {fodder.MaxHp:0} 血）");

        // —— 邪恶光环：+10% 移速、每秒 0.5 回血 ——
        var auraWorld = new World(null, null);
        Unit auraKnight = MakeHero(deathKnightType, 0, 3, auraWorld);
        Unit auraTarget = MakeUnit(ghoulType, 0);
        auraTarget.Position = new Vector3(160f, 0f, 0f);
        auraTarget.Hp = 100f;
        auraWorld.Units.Add(auraKnight);
        auraWorld.Units.Add(auraTarget);

        float baseSpeed = World.SpeedOf(auraTarget);
        auraKnight.GiveAbility("AUau", 1);
        auraWorld.TickAbilities();
        Verify("邪恶光环 +10% 移速", baseSpeed * 1.1f, World.SpeedOf(auraTarget), 0.5f);
        Verify("邪恶光环每秒回 0.5 血", 0.5f, auraTarget.AlwaysLifeRegenBonus, 0.001f);

        float regenHp = auraTarget.Hp;
        float baseRegenPerSecond = string.Equals(auraTarget.Type.RegenType, "none", StringComparison.OrdinalIgnoreCase)
            ? 0f
            : auraTarget.Type.RegenHp;
        for (int index = 0; index < 20; index++)
        {
            auraWorld.RefreshAuras();
            auraWorld.TickRegeneration();
        }
        float expectedRegen = baseRegenPerSecond + 0.5f;
        Verify("邪恶光环 1 秒回 0.5 血（叠加自然恢复）", regenHp + expectedRegen, auraTarget.Hp, 0.1f);
        GD.Print($"[自检] 邪恶光环 移速 {baseSpeed:0}→{World.SpeedOf(auraTarget):0}，1 秒回血 +{auraTarget.Hp - regenHp:0.00}");

        // —— 操纵死尸：抢尸体、临时无敌、到期散掉 ——
        var animateWorld = new World(null, null);
        Unit animator = MakeHero(deathKnightType, 0, 6, animateWorld);
        Unit corpse = MakeUnit(ghoulType, 1);
        corpse.Position = new Vector3(200f, 0f, 0f);
        corpse.Alive = false;
        corpse.Hp = 0f;
        corpse.IsCorpse = true;
        corpse.CorpseRemaining = 88f;
        animateWorld.Units.Add(animator);
        animateWorld.Units.Add(corpse);

        Ability animate = animator.GiveAbility("AUan", 1);
        int raised = 0;
        if (animateWorld.TryCast(animator, animate, null, animator.Position))
        {
            foreach (Unit unit in animateWorld.Units)
            {
                if (unit.Alive && ReferenceEquals(unit, corpse)) raised++;
            }
        }
        Verify("操纵死尸拉起 1 具尸体", 1f, raised, 0f);
        Verify("操纵死尸归施法者", 0f, corpse.Player, 0f);
        Verify("操纵死尸期间无敌", 1f, corpse.State.Invulnerable ? 1f : 0f, 0f);
        Verify("操纵死尸存在 40 秒", 40f, corpse.SummonRemaining, 0.01f);

        for (int index = 0; index < 900; index++) animateWorld.TickAbilities();
        Verify("40 秒后散掉", 0f, corpse.Alive ? 1f : 0f, 0f);
        GD.Print($"[自检] 操纵死尸 尸体起身 → 40s 后 {corpse.Alive}");

        // —— 霜冻新星：主目标 100、旁边 50、全体减速 ——
        var novaWorld = new World(null, null);
        Unit novaCaster = MakeHero(UnitFactory.Get("Ulic") ?? deathKnightType, 0, 3, novaWorld);
        Unit primary = MakeUnit(ghoulType, 1);
        primary.Position = new Vector3(300f, 0f, 0f);
        Unit splashA = MakeUnit(ghoulType, 1);
        splashA.Position = new Vector3(380f, 0f, 0f);
        Unit splashB = MakeUnit(ghoulType, 1);
        splashB.Position = new Vector3(300f, 0f, 120f);
        novaWorld.Units.Add(novaCaster);
        novaWorld.Units.Add(primary);
        novaWorld.Units.Add(splashA);
        novaWorld.Units.Add(splashB);

        Ability nova = novaCaster.GiveAbility("AUfn", 1);
        float primaryHp = primary.Hp;
        float splashHp = splashA.Hp;
        bool novaCast = novaWorld.TryCast(novaCaster, nova, primary, primary.Position);
        Verify("霜冻新星施放成功", 1f, novaCast ? 1f : 0f, 0f);
        Verify("霜冻新星主目标掉血", 1f, primary.Hp < primaryHp ? 1f : 0f, 0f);
        Verify("霜冻新星范围内也掉血", 1f, splashA.Hp < splashHp ? 1f : 0f, 0f);
        Verify("霜冻新星移速减速 50%", 0.5f, splashA.SlowMovePct, 0.001f);
        Verify("霜冻新星攻速减速 25%", 0.25f, splashA.SlowAttackPct, 0.001f);
        Verify("霜冻新星减速 4 秒", 4f, primary.SlowRemaining, 0.01f);
        GD.Print($"[自检] 霜冻新星 主目标 -{primaryHp - primary.Hp:0}，旁边 -{splashHp - splashA.Hp:0}，减速 {splashA.SlowRemaining:0.0}s");

        // —— 腐臭蜂群：直线上的吃伤害，偏出去的不吃 ——
        var swarmWorld = new World(null, null);
        Unit dread = MakeHero(UnitFactory.Get("Uear") ?? deathKnightType, 0, 3, swarmWorld);
        var onLine = new List<Unit>();
        for (int index = 0; index < 3; index++)
        {
            Unit victim = MakeUnit(footmanType, 1);
            victim.Position = new Vector3(150f + index * 150f, 0f, 0f);
            swarmWorld.Units.Add(victim);
            onLine.Add(victim);
        }
        Unit offLine = MakeUnit(footmanType, 1);
        offLine.Position = new Vector3(300f, 0f, 320f);
        swarmWorld.Units.Add(offLine);
        swarmWorld.Units.Add(dread);

        Ability swarm = dread.GiveAbility("AUcs", 1);
        float[] before = new float[onLine.Count];
        for (int index = 0; index < onLine.Count; index++) before[index] = onLine[index].Hp;
        float offBefore = offLine.Hp;
        bool swarmCast = swarmWorld.TryCast(dread, swarm, null, new Vector3(600f, 0f, 0f));

        // 客户端腐尸群是线型导弹，命中在推进后结算
        for (int tick = 0; tick < 30; tick++) swarmWorld.Advance();

        int hit = 0;
        for (int index = 0; index < onLine.Count; index++)
        {
            if (onLine[index].Hp < before[index]) hit++;
        }
        Verify("腐臭蜂群施放成功", 1f, swarmCast ? 1f : 0f, 0f);
        Verify("腐臭蜂群打中直线上的 3 个", 3f, hit, 0f);
        Verify("腐臭蜂群不打偏出去的单位", offBefore, offLine.Hp, 0.01f);
        GD.Print($"[自检] 腐臭蜂群 命中 {hit} 个，偏出 320 距离的单位生命 {offLine.Hp:0}/{offLine.MaxHp:0}");

        // —— 睡眠：睡着不能动，挨打就醒 ——
        var sleepWorld = new World(null, null);
        Unit sleeperHero = MakeHero(UnitFactory.Get("Uear") ?? deathKnightType, 0, 3, sleepWorld);
        Unit sleeper = MakeUnit(footmanType, 1);
        sleeper.Position = new Vector3(200f, 0f, 0f);
        sleepWorld.Units.Add(sleeperHero);
        sleepWorld.Units.Add(sleeper);

        Ability sleep = sleeperHero.GiveAbility("AUsl", 1);
        bool slept = sleepWorld.TryCast(sleeperHero, sleep, sleeper, sleeper.Position);
        Verify("睡眠施放成功", 1f, slept ? 1f : 0f, 0f);
        Verify("睡眠禁止移动", 0f, sleeper.State.CanMove ? 1f : 0f, 0f);
        var sl = sleeper.FindBuff<RuntimeSleepBuff>();
        var pr = sleeper.FindBuff<RuntimeSleepProtectionBuff>();
        Verify("睡眠 20 秒", 20f, sl?.Remaining ?? 0f, 0.5f);
        Verify("睡眠保护期=DataA", sleeperHero.FindAbility("AUsl").Type.DataAt(1, 'A'), pr?.Remaining ?? 0f, 0.1f);

        sleepWorld.DealDamage(World.MakeAbilityDamage(sleeperHero, sleeper, 10f, AttackType.Spells, DamageType.Magic));
        Verify("保护期内挨打不醒", 1f, sleeper.FindBuff<RuntimeSleepBuff>() != null ? 1f : 0f, 0f);

        for (int tick = 0; tick < 60 && sleeper.FindBuff<RuntimeSleepProtectionBuff>() != null; tick++) sleepWorld.Advance();
        sleepWorld.DealDamage(World.MakeAbilityDamage(sleeperHero, sleeper, 10f, AttackType.Spells, DamageType.Magic));
        Verify("保护期过后挨打立刻醒", 0f, sleeper.FindBuff<RuntimeSleepBuff>() != null ? 1f : 0f, 0f);
GD.Print($"[自检] 睡眠 20s → 吃 10 点伤害后 {sleeper.State.Describe()}");

        // —— 吸血光环：普攻伤害的 15% 回血 ——
        var drainWorld = new World(null, null);
        Unit vampSource = MakeHero(UnitFactory.Get("Uear") ?? deathKnightType, 0, 3, drainWorld);
        Unit vampAttacker = MakeUnit(footmanType, 0);
        vampAttacker.Position = new Vector3(150f, 0f, 0f);
        Unit vampVictim = MakeUnit(ghoulType, 1);
        vampVictim.Position = new Vector3(260f, 0f, 0f);
        drainWorld.Units.Add(vampSource);
        drainWorld.Units.Add(vampAttacker);
        drainWorld.Units.Add(vampVictim);
        vampAttacker.Hp = 100f;

        vampSource.GiveAbility("AUav", 1);
        drainWorld.RefreshAuras();
        Verify("吸血光环 15%", 0.15f, vampAttacker.AuraLifestealPct, 0.001f);

        float vampBefore = vampAttacker.Hp;
        float victimBefore = vampVictim.Hp;
        drainWorld.DealDamage(drainWorld.MakeAttackDamage(vampAttacker, vampVictim, 100f));
        float dealt = victimBefore - vampVictim.Hp;
        Verify("吸血光环按实际伤害回血", vampBefore + dealt * 0.15f, vampAttacker.Hp, 0.5f);
        GD.Print($"[自检] 吸血光环 打出 {dealt:0} 伤害，攻击者 {vampBefore:0}→{vampAttacker.Hp:0}");

        // —— 地狱火：落点伤害 + 眩晕 + 召唤 ——
        var infernoWorld = new World(null, null);
        infernoWorld.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit dreadTwo = MakeHero(UnitFactory.Get("Uear") ?? deathKnightType, 0, 6, infernoWorld);
        Unit infernoVictim = MakeUnit(footmanType, 1);
        infernoVictim.Position = new Vector3(400f, 0f, 0f);
        infernoWorld.Units.Add(dreadTwo);
        infernoWorld.Units.Add(infernoVictim);

        Ability inferno = dreadTwo.GiveAbility("AUin", 1);
        float infernoBefore = infernoVictim.Hp;
        bool infernoCast = infernoWorld.TryCast(dreadTwo, inferno, null, new Vector3(400f, 0f, 0f));
        Verify("地狱火施放成功", 1f, infernoCast ? 1f : 0f, 0f);
        Verify("地狱火落点伤害", 1f, infernoVictim.Hp < infernoBefore ? 1f : 0f, 0f);
        Verify("地狱火眩晕 4 秒", 4f, infernoVictim.State.Remaining(StateMod.Stun), 0.01f);

        Unit summonedInfernal = null;
        foreach (Unit unit in infernoWorld.Units)
        {
            if (unit.Type != null && unit.Type.Id == "ninf") summonedInfernal = unit;
        }
        Verify("地狱火召出 ninf", 1f, summonedInfernal != null ? 1f : 0f, 0f);
        if (summonedInfernal != null)
        {
            Verify("地狱火存在 180 秒", 180f, summonedInfernal.SummonRemaining, 0.01f);
        }
        GD.Print($"[自检] 地狱火 落点伤害 -{infernoBefore - infernoVictim.Hp:0}，召出 {(summonedInfernal != null ? "ninf" : "无")}");

        // —— 穿刺：直线地面伤害 + 眩晕 ——
        var impaleWorld = new World(null, null);
        Unit cryptLord = MakeHero(UnitFactory.Get("Ucrl") ?? deathKnightType, 0, 3, impaleWorld);
        var impaleVictims = new List<Unit>();
        for (int index = 0; index < 2; index++)
        {
            Unit victim = MakeUnit(footmanType, 1);
            victim.Position = new Vector3(200f + index * 150f, 0f, 0f);
            impaleWorld.Units.Add(victim);
            impaleVictims.Add(victim);
        }
        impaleWorld.Units.Add(cryptLord);

        Ability impale = cryptLord.GiveAbility("AUim", 1);
        int impaledCount = 0;
        bool impaleCast = impaleWorld.TryCast(cryptLord, impale, null, new Vector3(600f, 0f, 0f));
        foreach (Unit victim in impaleVictims)
        {
            if (victim.Hp < victim.MaxHp && victim.State.Has(StateMod.Stun)) impaledCount++;
        }
        Verify("穿刺施放成功", 1f, impaleCast ? 1f : 0f, 0f);
        Verify("穿刺击中 2 个并眩晕", 2f, impaledCount, 0f);
        GD.Print($"[自检] 穿刺 命中 {impaledCount} 个，眩晕 {impaleVictims[0].State.Remaining(StateMod.Stun):0.0}s");

        // —— 尖刺甲壳：+3 护甲，近战反伤 15% ——
        var carapaceWorld = new World(null, null);
        Unit beetle = MakeHero(UnitFactory.Get("Ucrl") ?? deathKnightType, 0, 3, carapaceWorld);
        Unit striker = MakeUnit(footmanType, 1);
        striker.Position = new Vector3(80f, 0f, 0f);
        carapaceWorld.Units.Add(beetle);
        carapaceWorld.Units.Add(striker);

        float armorWithout = carapaceWorld.ArmorOf(beetle);
        beetle.GiveAbility("AUts", 1);
        Verify("尖刺甲壳 +3 护甲", armorWithout + 3f, carapaceWorld.ArmorOf(beetle), 0.01f);

        float strikerHp = striker.Hp;
        float beetleHp = beetle.Hp;
        carapaceWorld.DealDamage(carapaceWorld.MakeAttackDamage(striker, beetle, 100f));
        float dealtToBeetle = beetleHp - beetle.Hp;
        float reflected = War3GameplayConfig.Default.Resolve(100f * 0.15f, AttackType.Spells,
            striker.Type.DefenseType, DamageType.Universal, 0f, false, false);
        Verify("尖刺甲壳反弹 15%", strikerHp - reflected, striker.Hp, 0.5f);
        GD.Print($"[自检] 尖刺甲壳 挨 {dealtToBeetle:0} 伤害，反弹 {strikerHp - striker.Hp:0}");

        // —— 腐尸甲虫：从尸体里召甲虫 ——
        var scarabWorld = new World(null, null);
        scarabWorld.SummonHandler = (caster, id, position) => MakeUnit(UnitFactory.Get(id), caster.Player);
        Unit scarabLord = MakeHero(UnitFactory.Get("Ucrl") ?? deathKnightType, 0, 3, scarabWorld);
        Unit scarabCorpse = MakeUnit(footmanType, 1);
        scarabCorpse.Position = new Vector3(200f, 0f, 0f);
        scarabCorpse.Alive = false;
        scarabCorpse.Hp = 0f;
        scarabCorpse.IsCorpse = true;
        scarabWorld.Units.Add(scarabLord);
        scarabWorld.Units.Add(scarabCorpse);

        Ability scarabs = scarabLord.GiveAbility("AUcb", 1);
        bool scarabCast = scarabWorld.TryCast(scarabLord, scarabs, scarabCorpse, scarabCorpse.Position);
        int beetles = 0;
        foreach (Unit unit in scarabWorld.Units)
        {
            if (unit.Alive && unit.Type != null && unit.Type.Id.StartsWith("ucs", StringComparison.OrdinalIgnoreCase)) beetles++;
        }
        Verify("腐尸甲虫施放成功", 1f, scarabCast ? 1f : 0f, 0f);
        Verify("腐尸甲虫召出 1 只", 1f, beetles, 0f);
        Verify("腐尸甲虫吃掉尸体", 0f, scarabCorpse.IsCorpse ? 1f : 0f, 0f);

        int capped = beetles;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Unit extra = MakeUnit(footmanType, 1);
            extra.Position = new Vector3(200f, 0f, 0f);
            extra.Alive = false;
            extra.Hp = 0f;
            extra.IsCorpse = true;
            scarabWorld.Units.Add(extra);
            scarabs.CooldownRemaining = 0f;
            scarabLord.Mana = scarabLord.MaxMana;
            scarabWorld.TryCast(scarabLord, scarabs, extra, extra.Position);
        }
        capped = 0;
        foreach (Unit unit in scarabWorld.Units)
        {
            if (unit.Alive && unit.Type != null && unit.Type.Id.StartsWith("ucs", StringComparison.OrdinalIgnoreCase)) capped++;
        }
        Verify("腐尸甲虫上限 DataE=5", 5f, capped, 0f);
        GD.Print($"[自检] 腐尸甲虫 连放 9 次后存活 {capped} 只（DataE 上限）");
    }
}
