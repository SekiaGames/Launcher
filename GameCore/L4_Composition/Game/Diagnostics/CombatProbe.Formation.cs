using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    /// <summary>
    /// 编队阵型：物编 formation 分层 + 排布几何（对标客户端 MovementFormationOffsets）。
    /// </summary>
    private static void CheckFormation()
    {
        GD.Print("==== 编队阵型自检 ====");

        UnitType footmanType = UnitFactory.Get("hfoo");
        UnitType rifleType = UnitFactory.Get("hrif");
        if (footmanType == null || rifleType == null) return;

        Verify("步兵 formation=0（近战前排）", 0f, footmanType.FormationRank, 0f);
        Verify("火枪 formation=2（远程后排）", 2f, rifleType.FormationRank, 0f);

        var world = new World(null, null);
        Unit meleeLeft = MakeUnit(footmanType, 0);
        meleeLeft.Position = new Vector3(0f, 0f, -60f);
        Unit meleeRight = MakeUnit(footmanType, 0);
        meleeRight.Position = new Vector3(0f, 0f, 60f);
        Unit rangeLeft = MakeUnit(rifleType, 0);
        rangeLeft.Position = new Vector3(0f, 0f, -40f);
        Unit rangeRight = MakeUnit(rifleType, 0);
        rangeRight.Position = new Vector3(0f, 0f, 40f);

        var members = new List<Unit> { meleeLeft, meleeRight, rangeLeft, rangeRight };
        var destination = new Vector3(1000f, 0f, 0f);
        var forward = new Vector3(1f, 0f, 0f);
        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(members, destination, forward);

        Verify("每个单位都分到位置", members.Count, slots.Count, 0f);

        float meleeDepth = 1000f - slots[meleeLeft].X;
        float rangeDepth = 1000f - slots[rangeLeft].X;
        Verify("近战前排贴在落点", 0f, meleeDepth, 0.5f);
        Verify("远程后排退到至少一层之后", 1f, rangeDepth >= GroupFormation.RowGap + GroupFormation.RankGap - 1f ? 1f : 0f, 0f);
        Verify("近战左右顺序保留", 1f, slots[meleeLeft].Z < slots[meleeRight].Z ? 1f : 0f, 0f);
        Verify("远程左右顺序保留", 1f, slots[rangeLeft].Z < slots[rangeRight].Z ? 1f : 0f, 0f);
        Verify("同排横向间距=成员间距", GroupFormation.MemberGap, Mathf.Abs(slots[meleeRight].Z - slots[meleeLeft].Z), 0.5f);

        var unique = new HashSet<string>();
        foreach (KeyValuePair<Unit, Vector3> pair in slots)
        {
            unique.Add($"{pair.Value.X:0.0}|{pair.Value.Z:0.0}");
        }
        Verify("没有两个单位抢占同一位置", members.Count, unique.Count, 0f);

        GD.Print($"[自检] 阵型 近战深度 {meleeDepth:0} 远程深度 {rangeDepth:0}，"
            + $"横向 {slots[meleeLeft].Z:0}/{slots[meleeRight].Z:0}，"
            + $"间距 成员{GroupFormation.MemberGap:0} 行{GroupFormation.RowGap:0} 层{GroupFormation.RankGap:0}");
    }

    /// <summary>AI 战术：威胁分（英雄 > 后排 > 攻城 > 普通）与切入点选敌人少的一侧。</summary>
    private static void CheckAiTactics()
    {
        UnitType footmanType = UnitFactory.Get("hfoo");
        UnitType rifleType = UnitFactory.Get("hrif");
        UnitType mortarType = UnitFactory.Get("hmtm");
        UnitType heroType = UnitFactory.Get("Hpal");
        if (footmanType == null || rifleType == null || mortarType == null || heroType == null) return;

        Unit footman = MakeUnit(footmanType, 1);
        Unit rifle = MakeUnit(rifleType, 1);
        Unit mortar = MakeUnit(mortarType, 1);
        Unit hero = MakeHero(heroType, 1, 3, null);
        Unit illusionHero = MakeHero(heroType, 1, 3, null);
        illusionHero.IsIllusion = true;

        float footScore = AiTactics.ThreatScore(footman);
        float rifleScore = AiTactics.ThreatScore(rifle);
        float mortarScore = AiTactics.ThreatScore(mortar);
        float heroScore = AiTactics.ThreatScore(hero);
        float illusionScore = AiTactics.ThreatScore(illusionHero);

        Verify("英雄威胁分高于远程", 1f, heroScore > rifleScore ? 1f : 0f, 0f);
        Verify("英雄威胁分高于攻城", 1f, heroScore > mortarScore ? 1f : 0f, 0f);
        Verify("攻城威胁分高于远程", 1f, mortarScore > rifleScore ? 1f : 0f, 0f);
        Verify("远程威胁分高于近战", 1f, rifleScore > footScore ? 1f : 0f, 0f);
        Verify("幻象不算英雄", 1f, illusionScore < heroScore ? 1f : 0f, 0f);

        var units = new List<Unit> { footman, rifle };
        for (int index = 0; index < 2; index++)
        {
            Unit guard = MakeUnit(footmanType, 1);
            guard.Position = new Vector3(1000f, 0f, 420f);
            units.Add(guard);
        }

        var focus = MakeUnit(rifleType, 1);
        focus.Position = new Vector3(1000f, 0f, 0f);
        Vector3 flank = AiTactics.FlankPoint(focus, Vector3.Zero, units, null, 0);
        Verify("切入侧选敌人少的那边", -420f, flank.Z, 1f);

        GD.Print($"[自检] 威胁分 英雄 {heroScore:0.00}（幻象 {illusionScore:0.00}）"
            + $" 远程 {rifleScore:0.00} 攻城 {mortarScore:0.00} 近战 {footScore:0.00}；"
            + $"侧翼 ({flank.X:0},{flank.Z:0})");
    }
}