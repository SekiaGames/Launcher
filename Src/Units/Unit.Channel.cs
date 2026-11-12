using Godot;

namespace Launcher;

/// <summary>
/// 引导状态。对标客户端 UnitFlags.Channeling + Abilities.ChannelingAbility：
/// 引导期间不能移动、不能攻击，被打断（眩晕/沉默/下令/死亡/再施法）就停。
/// </summary>
public sealed partial class Unit
{
    /// <summary>正在引导的技能；null 表示没在引导。</summary>
    public Ability ChannelAbility;
    public float ChannelRemaining;
    /// <summary>引导落点（暴风雪/死亡腐烂这种点目标技能用得到）。</summary>
    public Vector3 ChannelPoint;

    public bool IsChanneling => ChannelAbility != null && ChannelRemaining > 0f;
}
