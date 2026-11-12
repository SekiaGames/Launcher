namespace Launcher;

/// <summary>技能的目标形态。对标客户端 Src/Abilities/AbilityTargetMode.cs。</summary>
public enum AbilityTargetMode
{
    /// <summary>无目标，按一下立刻施放（天神下凡、雷霆一击、神圣护甲）。</summary>
    None,
    /// <summary>需要单位目标（圣光术、风暴之锤）。</summary>
    Unit,
    /// <summary>需要地面点（暴风雪、闪烁）。</summary>
    Point,
}
