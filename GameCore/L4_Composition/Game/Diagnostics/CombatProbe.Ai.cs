using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>
    /// AI 组合出招与换兵种：控制技能先于爆发出手；对手阵容统计与候选兵种打分。
    /// 场景层的 Ai.cs 用同一套（AiCounters / Ability.AiCastPriority），这里验的是模拟层行为。
    /// </summary>
    private static void CheckAiCombat()
    {
        GD.Print("==== AI 组合出招自检 ====");

        UnitType chieftainType = UnitFactory.Get("Otch");
        UnitType gruntType = UnitFactory.Get("ogru");
        if (chieftainType != null && gruntType != null)
        {
            var world = new World(null, null);
            Unit chief = MakeHero(chieftainType, 1, 6, world);
            chief.AutoCast = true;
            chief.Mana = chief.MaxMana;
            world.Units.Add(chief);

            Unit first = MakeUnit(gruntType, 0);
            first.Position = new Vector3(90f, 0f, 0f);
            Unit second = MakeUnit(gruntType, 0);
            second.Position = new Vector3(130f, 0f, 90f);
            world.Units.Add(first);
            world.Units.Add(second);
            chief.Target = first;

            Ability stomp = chief.GiveAbility("AOws", 1);
            Ability wave = chief.GiveAbility("AOsh", 1);
            if (stomp != null && wave != null)
            {
                var order = new List<string>();
                world.AbilityCast += (caster, ability, target) => order.Add(ability.Id);

                for (int index = 0; index < 60; index++) world.Advance();

                Verify("AI 首招是控制（战争践踏）", 1f, order.Count > 0 && order[0] == stomp.Id ? 1f : 0f, 0f);
                Verify("AI 控制之后接爆发（震荡波）", 1f, order.Contains(wave.Id) ? 1f : 0f, 0f);
                GD.Print($"[自检] AI 出手次序 {string.Join(" → ", order)}"
                    + $"（战争践踏优先级 {(int)stomp.AiCastPriority}，震荡波 {(int)wave.AiCastPriority}）");
            }
        }

        UnitType footmanType = UnitFactory.Get("hfoo");
        UnitType riflemanType = UnitFactory.Get("hrif");
        UnitType paladinType = UnitFactory.Get("Hpal");
        UnitType flyerType = UnitFactory.Get("hgyr");
        if (footmanType == null || riflemanType == null || paladinType == null || flyerType == null) return;

        var heroComp = new EnemyComposition();
        for (int index = 0; index < 4; index++)
        {
            heroComp.ArmorWeight[(int)DefenseType.Hero] += 1f;
            heroComp.Total += 1f;
            heroComp.Ground++;
        }
        heroComp.Heroes = 4;

        float footVsHero = AiCounters.Score(footmanType, heroComp);
        float rifleVsHero = AiCounters.Score(riflemanType, heroComp);
        Verify("对手英雄甲：普通伤害优于穿刺", 1f, footVsHero > rifleVsHero ? 1f : 0f, 0f);

        var airComp = new EnemyComposition();
        airComp.Air = 3;
        airComp.Total = 3f;
        airComp.ArmorWeight[(int)DefenseType.Large] = 3f;

        float footVsAir = AiCounters.Score(footmanType, airComp);
        float rifleVsAir = AiCounters.Score(riflemanType, airComp);
        Verify("对手有空中：打不到空的兵种被压低", 1f, rifleVsAir > footVsAir + 0.5f ? 1f : 0f, 0f);
        GD.Print($"[自检] 换兵种打分 英雄甲→步兵{footVsHero:0.00}/火枪{rifleVsHero:0.00}"
            + $"  对空→步兵{footVsAir:0.00}/火枪{rifleVsAir:0.00}");

        var units = new List<Unit>
        {
            MakeHero(paladinType, 1, 3, null),
            MakeHero(paladinType, 1, 3, null),
            MakeUnit(flyerType, 1),
            MakeUnit(footmanType, 0),
        };
        EnemyComposition analyzed = AiCounters.Analyze(units, 0, null);
        Verify("阵容统计只算对手", 3f, analyzed.Total, 0f);
        Verify("阵容统计识别空中", 1f, analyzed.Air, 0f);
        Verify("阵容统计识别英雄甲", 2f, analyzed.ArmorWeight[(int)DefenseType.Hero], 0f);
        GD.Print($"[自检] 阵容统计 {AiCounters.Describe(analyzed)}");
    }
}