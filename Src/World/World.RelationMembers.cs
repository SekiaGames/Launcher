using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    private bool IsAllied(int a, int b)
    {
        if (a == b) return true;
        return Allied != null && Allied(a, b);
    }

    /// <summary>归属关系位：与客户端 TargetRules.RelationOf 同序（自己 → 同玩家 → 敌人 → 盟友）。</summary>
    private TargetRelation RelationOf(Unit source, Unit victim)
    {
        if (ReferenceEquals(source, victim)) return TargetRelation.Self;
        if (source.Player == victim.Player) return TargetRelation.Player;
        if (!IsAllied(source.Player, victim.Player)) return TargetRelation.Enemy;
        return TargetRelation.Ally;
    }
}
