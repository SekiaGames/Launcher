using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 荆棘光环 AEah（丛林守护者）。对标客户端 RuntimeThornsAuraAbility：半径 900 内友军被**近战**普攻命中时，
/// 把实际伤害的 DataA(0.1/0.2/0.3) 反弹给攻击者（反弹自身不吃反伤，见 DamageData.Reflected）。
/// 物编：Area=900 DataA=0.1/0.2/0.3 DataB=1。
/// </summary>
public sealed class RuntimeThornsAuraAbility : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.ThornsPct = Mathf.Max(bonus.ThornsPct, Data('A'));
    }
}
