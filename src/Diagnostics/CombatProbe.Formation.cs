using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>
    /// 编队阵型：物编 formation 分层 + 排布几何（对标客户端 MovementFormationOffsets）。
    /// </summary>
    private static void CheckFormation()
    {
        GD.Print("==== 编队阵型自检 ====");

        UnitType footmanType = UnitCatalog.Get("hfoo");
        UnitType rifleType = UnitCatalog.Get("hrif");
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
}