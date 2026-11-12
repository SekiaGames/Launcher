using Godot;

namespace Launcher;

/// <summary>
/// 强击光环 AEar（月之女祭司）。半径 900 内友军**远程**攻击伤害 +DataA(10%/20%/30%)。
/// 只作用于带弹道的武器（FireWeapon 里按 MissileScene 判定），近战武器不受影响。
/// 物编：Area=900 DataA=0.1/0.2/0.3 DataC=1。
/// </summary>
public sealed class TrueshotAura : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.RangedDamagePct = Mathf.Max(bonus.RangedDamagePct, Data('A'));
    }
}
