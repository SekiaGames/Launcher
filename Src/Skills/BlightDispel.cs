using Godot;

namespace Launcher;

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
