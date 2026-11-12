using Godot;

namespace Launcher;

/// <summary>
/// 腐地扩散（Abgl 大 / Abgs 小）。对标客户端 RuntimeBlightGrowthAbility：
/// 亡灵建筑把自己这圈腐地铺出去，半径就是物编 Area（960 / 768）。
/// </summary>
public sealed class BlightGrowth : Ability
{
    public override bool IsPassive => true;

    public float Radius => Area > 0f ? Area : MiscRulesBlightRadius;

    private static float MiscRulesBlightRadius => 800f;

    public override void OnLearned()
    {
        if (Owner == null) return;
        Owner.BlightRadius = Mathf.Max(Owner.BlightRadius, Radius);
    }
}

/// <summary>
/// 腐地净化（Abdl 大 / Abds 小）。对标客户端 RuntimeBlightDispelAbility：
/// 这类建筑周围一圈不算腐地（暗夜/人族基地压掉亡灵腐地）。
/// </summary>
public sealed class BlightDispel : Ability
{
    public override bool IsPassive => true;

    public float Radius => Area > 0f ? Area : 800f;

    public override void OnLearned()
    {
        if (Owner == null) return;
        Owner.BlightSuppressRadius = Mathf.Max(Owner.BlightSuppressRadius, Radius);
    }
}

/// <summary>被腐化的金矿（Abgm）：只是标记这口矿自带腐地，采集逻辑不需要额外处理。</summary>
public sealed class BlightedMine : Ability
{
    public override bool IsPassive => true;
}