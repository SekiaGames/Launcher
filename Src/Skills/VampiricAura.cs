using Godot;

namespace Launcher;

/// <summary>
/// 吸血光环 AUav（恐惧魔王）。对标客户端 Src/Skills/VampiricAura：
/// 半径 900 内友军把普攻伤害的 DataA(0.15/0.3/0.45) 转成自己的生命值。
/// 结算挂在 World.SustainAfterHit（唯一伤害入口之后）。
/// </summary>
public sealed class VampiricAura : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.LifestealPct = Mathf.Max(bonus.LifestealPct, Data('A'));
    }
}
