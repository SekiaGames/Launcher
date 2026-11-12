using System;

namespace Launcher;

// 目标类别位（对标客户端 CTargetType：武器 targs1 与单位 targType 用同一套位）
[Flags]
public enum TargetKind
{
    None = 0,
    Ground = 1 << 0,
    Air = 1 << 1,
    Structure = 1 << 2,
    Ward = 1 << 3,
    Item = 1 << 4,
    Tree = 1 << 5,
    Wall = 1 << 6,
    Debris = 1 << 7,
    All = Ground | Air | Structure | Ward | Item | Tree | Wall | Debris,
}

// 归属关系位（对标客户端 TargetRules.RelationOf；掩码里没写关系时按客户端 68c7b0 默认成全部）
[Flags]
public enum TargetRelation
{
    None = 0,
    Self = 1 << 0,
    Player = 1 << 1,
    Ally = 1 << 2,
    Neutral = 1 << 3,
    Enemy = 1 << 4,
    All = Self | Player | Ally | Neutral | Enemy,
}
