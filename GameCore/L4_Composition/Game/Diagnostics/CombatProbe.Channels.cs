using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 引导（channel）与点目标技能的自检：暴风雪、死亡腐烂、闪电链、闪烁、沉默、霜冻护甲、闪避。
/// 全部按「读表数值 + 手算」对账，不依赖场景。
/// </summary>
public static partial class CombatProbe
{
    private static void CheckChannelsAndPointSkills()
    {
        CheckBlizzard();
        CheckDeathAndDecay();
        CheckChainLightning();
        CheckBlink();
        CheckSilence();
        CheckFrostArmor();
        CheckEvasion();
    }

    /// <summary>暴风雪：6 波 × 30 伤害；被眩晕/下令打断就停。</summary>
    private static void CheckBlizzard()
    {
        var type = AbilityCatalog.Get("AHbz");
        UnitType mageType = UnitFactory.Get("Hamg");
        UnitType footType = UnitFactory.Get("hfoo");
        if (type == null || mageType == null || footType == null) return;

        var world = new World(null, null);
        Unit mage = MakeHero(mageType, 0, 6, world);
        Unit victim = MakeUnit(footType, 1);
        victim.Position = new Vector3(120f, 0f, 0f);
        victim.MaxHp = 100000f;
        victim.Hp = 100000f;
        world.Units.Add(mage);
        world.Units.Add(victim);

        var storm = (RuntimeBlizzardAbility)mage.GiveAbility("AHbz", 1);
        storm.Level = 1;
        mage.Mana = mage.MaxMana;

        var point = new Vector3(120f, 0f, 0f);
        bool cast = world.TryCast(mage, storm, null, point);
        Verify("暴风雪施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("暴风雪进入引导", 1f, mage.IsChanneling ? 1f : 0f, 0f);
        Verify("暴风雪引导时长 = 波数×间隔", storm.Waves * storm.WaveInterval, mage.ChannelRemaining, 0.01f);

        // 单波伤害：拿一个不参战的同型单位按同样参数打一次做参照，避免手算护甲/矩阵
        var probe = new Unit { Type = footType, Player = 1, Position = point, MaxHp = 100000f, Hp = 100000f };
        world.DealDamage(World.MakeAbilityDamage(mage, probe, storm.WaveDamage, AttackType.Spells, DamageType.Magic));
        float reference = 100000f - probe.Hp;
        float before = victim.Hp;

        // 引导时长跟着物编 Cast 走（AHbz 是 1s/波），不能写死 tick 数。
        int stormTicks = (int)(storm.ChannelLength / World.Tick) + 5;
        for (int tick = 0; tick < stormTicks; tick++) world.TickChannels();
        float total = before - victim.Hp;

        float expected = reference * storm.Waves;
        Verify("暴风雪总伤害 = 波数×单波", expected, total, expected * 0.05f);
        Verify("暴风雪结束后不再引导", 0f, mage.IsChanneling ? 1f : 0f, 0f);
        GD.Print($"[自检] 暴风雪 {storm.Waves} 波 ×{storm.WaveDamage:0} 单波实结算 {reference:0.0}"
            + $" 合计 {total:0.0}（{storm.ChannelLength:0.0}s，半径 {storm.Radius:0}）");

        // 眩晕打断
        mage.Hp = mage.MaxHp;
        mage.Mana = mage.MaxMana;
        storm.CooldownRemaining = 0f;
        world.TryCast(mage, storm, null, point);
        for (int tick = 0; tick < 12; tick++) world.TickChannels();
        mage.State.Add(StateMod.Stun, 1f);
        world.TickChannels();
        Verify("眩晕打断引导", 0f, mage.IsChanneling ? 1f : 0f, 0f);
        mage.State.Clear();

        // 下令打断
        mage.Mana = mage.MaxMana;
        storm.CooldownRemaining = 0f;
        world.TryCast(mage, storm, null, point);
        world.ClearOrders(mage);
        Verify("下令（停/移动）打断引导", 0f, mage.IsChanneling ? 1f : 0f, 0f);
        Verify("引导中断后队列清空", 0f, world.ChannelCount, 0f);
    }

    /// <summary>死亡腐烂：每秒掉 4% 最大生命，35 秒引导。</summary>
    private static void CheckDeathAndDecay()
    {
        var type = AbilityCatalog.Get("AUdd");
        UnitType lichType = UnitFactory.Get("Udea");
        UnitType footType = UnitFactory.Get("hfoo");
        if (type == null || lichType == null || footType == null) return;

        var world = new World(null, null);
        Unit lich = MakeHero(lichType, 0, 6, world);
        Unit victim = MakeUnit(footType, 1);
        victim.Position = new Vector3(150f, 0f, 0f);
        victim.MaxHp = 1000f;
        victim.Hp = 1000f;
        world.Units.Add(lich);
        world.Units.Add(victim);

        var decay = (RuntimeDeathAndDecayAbility)lich.GiveAbility("AUdd", 1);
        decay.Level = 1;
        lich.Mana = lich.MaxMana;

        bool cast = world.TryCast(lich, decay, null, new Vector3(150f, 0f, 0f));
        Verify("死亡腐烂施放成功", 1f, cast ? 1f : 0f, 0f);

        float before = victim.Hp;
        for (int tick = 0; tick < 20; tick++) world.TickChannels();   // 1 秒
        float lost = before - victim.Hp;
        Verify("死亡腐烂 1 秒掉 4% 最大生命", 40f, lost, 4f);
        GD.Print($"[自检] 死亡腐烂 1s 掉 {lost:0.0}（{decay.UnitFraction * 100f:0}%/s，建筑 {decay.BuildingPercent:0}%/s，"
            + $"引导 {decay.Duration:0}s 半径 {decay.Radius:0}）");
    }

    /// <summary>闪电链：4 跳，每跳衰减 15%。</summary>
    private static void CheckChainLightning()
    {
        var type = AbilityCatalog.Get("AOcl");
        UnitType farType = UnitFactory.Get("Ofar");
        UnitType footType = UnitFactory.Get("hfoo");
        if (type == null || farType == null || footType == null) return;

        var world = new World(null, null);
        Unit seer = MakeHero(farType, 0, 6, world);
        world.Units.Add(seer);

        var victims = new List<Unit>();
        for (int index = 0; index < 4; index++)
        {
            Unit victim = MakeUnit(footType, 1);
            victim.Position = new Vector3(200f + index * 200f, 0f, 0f);
            victim.MaxHp = 100000f;
            victim.Hp = 100000f;
            world.Units.Add(victim);
            victims.Add(victim);
        }

        var chain = (ChainLightning)seer.GiveAbility("AOcl", 1);
        chain.Level = 1;
        seer.Mana = seer.MaxMana;

        bool cast = world.TryCast(seer, chain, victims[0], victims[0].Position);
        Verify("闪电链施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("闪电链跳满 4 个目标", 4f, chain.LastChain.Count, 0f);

        float first = 100000f - victims[0].Hp;
        float second = 100000f - victims[1].Hp;
        Verify("第二跳衰减 15%", first * (1f - chain.DamageReduction), second, 0.5f);
        GD.Print($"[自检] 闪电链 {chain.LastChain.Count} 跳：{first:0.0} → {second:0.0}"
            + $"（伤害 {chain.Damage:0}，每跳 -{chain.DamageReduction * 100f:0}%，跳跃 {chain.JumpRange:0}）");
    }

    /// <summary>闪烁：正常落点直接过去；超距钳到 0.8×上限。</summary>
    private static void CheckBlink()
    {
        var type = AbilityCatalog.Get("AEbl");
        UnitType wardenType = UnitFactory.Get("Ewar") ?? UnitFactory.Get("Ekee");
        if (type == null || wardenType == null) return;

        var world = new World(null, null);
        Unit warden = MakeHero(wardenType, 0, 6, world);
        world.Units.Add(warden);

        var blink = (Blink)warden.GiveAbility("AEbl", 1);
        blink.Level = 1;
        warden.Mana = warden.MaxMana;

        Vector3 origin = warden.Position;
        bool cast = world.TryCast(warden, blink, null, origin + new Vector3(400f, 0f, 0f));
        Verify("闪烁施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("闪烁落点 400", 400f, warden.Position.X - origin.X, 1f);

        blink.CooldownRemaining = 0f;
        Vector3 from = warden.Position;
        world.TryCast(warden, blink, null, from + new Vector3(3000f, 0f, 0f));
        float moved = warden.Position.X - from.X;
        Verify("超距闪烁钳到 0.8×上限", blink.MaximumRange * 0.8f, moved, 2f);
        GD.Print($"[自检] 闪烁 上限 {blink.MaximumRange:0} 最近 {blink.MinimumRange:0}，"
            + $"正常 400 → 超距钳 {moved:0}");
    }

    /// <summary>沉默：被沉默的单位放不出技能，状态到期后恢复。</summary>
    private static void CheckSilence()
    {
        var silenceType = AbilityCatalog.Get("ANsi");
        UnitType rangerType = UnitFactory.Get("Nbrn") ?? UnitFactory.Get("Hpal");
        UnitType mageType = UnitFactory.Get("Hamg");
        if (silenceType == null || rangerType == null || mageType == null) return;

        var world = new World(null, null);
        Unit ranger = MakeHero(rangerType, 0, 6, world);
        Unit mage = MakeHero(mageType, 1, 6, world);
        mage.Position = new Vector3(300f, 0f, 0f);
        world.Units.Add(ranger);
        world.Units.Add(mage);

        var silence = (Silence)ranger.GiveAbility("ANsi", 1);
        silence.Level = 1;
        ranger.Mana = ranger.MaxMana;
        world.TryCast(ranger, silence, null, mage.Position);

        Verify("沉默命中 1 人", 1f, silence.LastSilenced, 0f);
        Verify("目标处于沉默", 1f, mage.State.Silenced ? 1f : 0f, 0f);

        Ability bolt = mage.GiveAbility("AHtb", 1);
        bolt.Level = 1;
        mage.Mana = mage.MaxMana;
        bool blocked = world.TryCast(mage, bolt, ranger, ranger.Position);
        Verify("沉默中放不出技能", 0f, blocked ? 1f : 0f, 0f);

        mage.State.Clear();
        bolt.CooldownRemaining = 0f;
        bool ok = world.TryCast(mage, bolt, ranger, ranger.Position);
        Verify("解除沉默后能放", 1f, ok ? 1f : 0f, 0f);
        GD.Print($"[自检] 沉默 半径 {silence.Radius:0} 时长 {silence.Duration:0}s，"
            + $"沉默中施法={(blocked ? "成功（错）" : "被挡")}");
    }

    /// <summary>霜冻护甲：+DataB 护甲，到时撤销。</summary>
    private static void CheckFrostArmor()
    {
        var type = AbilityCatalog.Get("AUfa");
        UnitType lichType = UnitFactory.Get("Udea");
        UnitType footType = UnitFactory.Get("hfoo");
        if (type == null || lichType == null || footType == null) return;

        var world = new World(null, null);
        Unit lich = MakeHero(lichType, 0, 6, world);
        Unit soldier = MakeUnit(footType, 0);
        soldier.Position = new Vector3(80f, 0f, 0f);
        world.Units.Add(lich);
        world.Units.Add(soldier);

        var frost = (RuntimeFrostArmorAbility)lich.GiveAbility("AUfa", 1);
        frost.Level = 1;
        lich.Mana = lich.MaxMana;

        float baseArmor = world.ArmorOf(soldier);
        bool cast = world.TryCast(lich, frost, soldier, soldier.Position);
        Verify("霜冻护甲施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("护甲 +3", baseArmor + frost.ArmorBonus, world.ArmorOf(soldier), 0.01f);

        // 被普攻命中：攻击者被减速（Buff.OnAttacked → ApplySlow）
        Unit attacker = MakeUnit(footType, 1);
        attacker.Position = new Vector3(20f, 0f, 0f);
        world.Units.Add(attacker);
        world.DealDamage(world.MakeAttackDamage(attacker, soldier, 10f));
        Verify("打霜冻护甲会被减速", 1f, attacker.Slowed ? 1f : 0f, 0f);
        Verify("减速时长 5s", 5f, attacker.SlowRemaining, 0.2f);

        for (int tick = 0; tick < (int)(frost.BuffDuration / World.Tick) + 4; tick++) world.TickAbilities();
        Verify("到期护甲撤销", baseArmor, world.ArmorOf(soldier), 0.01f);
        GD.Print($"[自检] 霜冻护甲 +{frost.ArmorBonus:0} 甲 持续 {frost.BuffDuration:0}s，到期回到 {world.ArmorOf(soldier):0.0}");
    }

    /// <summary>闪避：普攻按 DataA 概率闪掉，法术伤害不受影响。</summary>
    private static void CheckEvasion()
    {
        var type = AbilityCatalog.Get("AEev");
        UnitType demonType = UnitFactory.Get("Edem") ?? UnitFactory.Get("Edmm");
        UnitType footType = UnitFactory.Get("hfoo");
        if (type == null || demonType == null || footType == null) return;

        var world = new World(null, null);
        Unit demon = MakeHero(demonType, 1, 6, world);
        demon.MaxHp = 1000000f;
        demon.Hp = 1000000f;
        Unit attacker = MakeUnit(footType, 0);
        attacker.Position = new Vector3(40f, 0f, 0f);
        world.Units.Add(demon);
        world.Units.Add(attacker);

        var evasion = (Evasion)demon.GiveAbility("AEev", 1);
        evasion.Level = 3;   // 30%

        const int swings = 2000;
        for (int index = 0; index < swings; index++)
        {
            world.DealDamage(world.MakeAttackDamage(attacker, demon, 1f));
        }

        float rate = evasion.Dodged * 100f / swings;
        Verify("闪避率接近 30%", 30f, rate, 4f);

        int dodgedBefore = evasion.Dodged;
        for (int index = 0; index < 200; index++)
        {
            world.DealDamage(World.MakeAbilityDamage(attacker, demon, 1f, AttackType.Spells, DamageType.Magic));
        }
        Verify("法术伤害不会被闪避", dodgedBefore, evasion.Dodged, 0f);
        GD.Print($"[自检] 闪避 {swings} 次普攻闪掉 {evasion.Dodged} 次（{rate:0.0}%，物编 {evasion.Chance * 100f:0}%）");
    }
}
