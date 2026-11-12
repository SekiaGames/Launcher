using Godot;

namespace Launcher;

/// <summary>
/// 邪恶光环 AUau（死亡骑士）。对标客户端 Src/Skills/AuraAbility：
/// 半径 Area(900) 内友军 +DataA 移速、每秒回 DataB 点生命。
/// 物编：DataA=0.1/0.2/0.3（移速百分比）DataB=0.5/1/1.5（每秒回血）。
/// </summary>
public sealed class RuntimeUnholyAuraAbility : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.MoveSpeedPct = Mathf.Max(bonus.MoveSpeedPct, Data('A'));
        bonus.HpRegenPerSecond = Mathf.Max(bonus.HpRegenPerSecond, Data('B'));
    }
}
