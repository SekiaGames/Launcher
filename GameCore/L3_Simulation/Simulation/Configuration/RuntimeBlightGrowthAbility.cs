using Godot;

namespace Launcher;

/// <summary>
/// 腐地扩散（Abgl 大 / Abgs 小）。对标客户端 RuntimeBlightGrowthAbility：
/// 亡灵建筑把自己这圈腐地铺出去，半径就是物编 Area（960 / 768）。
/// </summary>
public sealed class RuntimeBlightGrowthAbility : Ability
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
