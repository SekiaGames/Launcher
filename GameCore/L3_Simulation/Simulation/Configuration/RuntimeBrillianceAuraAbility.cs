using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 辉煌光环 AHab（大魔法师）。半径 900 内友军每秒多回 DataA(0.75/1.5/2.25) 法力。
/// 回蓝在 World.TickRegeneration 里按 AuraManaRegenPerSecond 加到英雄身上。
/// </summary>
public sealed class RuntimeBrillianceAuraAbility : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        bonus.ManaRegenPerSecond = Mathf.Max(bonus.ManaRegenPerSecond, Data('A'));
    }
}
