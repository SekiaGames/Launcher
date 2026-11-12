using Godot;

namespace Launcher;

/// <summary>
/// 耐久光环 AOae（牛头人酋长）。对标客户端 Src/Skills/EnduranceAura：
/// 半径 900 内友军 +DataA 移速、+DataB 攻速。
/// 物编：DataA=0.1/0.2/0.3（移速）DataB=0.05/0.1/0.15（攻速）。
/// 凯恩·血蹄的 AOr2 是同一基类码，走同一实现。
/// </summary>
public sealed class EnduranceAura : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.MoveSpeedPct = Mathf.Max(bonus.MoveSpeedPct, Data('A'));
        bonus.AttackSpeedPct = Mathf.Max(bonus.AttackSpeedPct, Data('B'));
    }
}
