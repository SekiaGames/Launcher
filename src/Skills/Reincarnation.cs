using Godot;

namespace Launcher;

/// <summary>
/// 重生 AOre（牛头人酋长终极）。等价客户端的死亡前钩子：阵亡时若已学且不在冷却，
/// DataA（7s）后原地满血复活，不走祭坛、不扣金，技能自身进 Cool（240s）冷却。
/// 物编 Cast=3 是施法前摇，死亡触发不走施法流程。
/// </summary>
public sealed class Reincarnation : Ability
{
    public override bool IsPassive => true;

    public float DelaySeconds => Mathf.Max(1f, Data('A'));
}