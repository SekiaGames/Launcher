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
