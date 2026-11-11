using Godot;

namespace Launcher;

public sealed class Ultravision : Ability
{
    public override bool IsPassive => true;
    public override bool NightSight => true;
    public override float PassiveSightBonus => Data('A');
}

public sealed class ResistantSkin : Ability
{
    public override bool IsPassive => true;
    public override float DebuffDurationScale => 0.5f;
}

public sealed class ItemAura : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public override void CollectAura(ref AuraBonus bonus)
    {
        switch ((Type?.Id ?? "").ToUpperInvariant())
        {
            case "AIBA":
                bonus.ManaRegenPerSecond = Mathf.Max(bonus.ManaRegenPerSecond, Data('A'));
                break;
            case "AIAV":
                bonus.LifestealPct = Mathf.Max(bonus.LifestealPct, Data('A'));
                break;
            case "AIAR":
                bonus.RangedDamagePct = Mathf.Max(bonus.RangedDamagePct, Data('A'));
                break;
            case "AIAE":
                bonus.MoveSpeedPct = Mathf.Max(bonus.MoveSpeedPct, Data('A'));
                bonus.AttackSpeedPct = Mathf.Max(bonus.AttackSpeedPct, Data('B'));
                break;
            case "AIAU":
                bonus.MoveSpeedPct = Mathf.Max(bonus.MoveSpeedPct, Data('A'));
                bonus.HpRegenPerSecond = Mathf.Max(bonus.HpRegenPerSecond, Data('B'));
                break;
            case "AICD":
                bonus.DamagePct = Mathf.Max(bonus.DamagePct, Data('A'));
                break;
        }
    }
}