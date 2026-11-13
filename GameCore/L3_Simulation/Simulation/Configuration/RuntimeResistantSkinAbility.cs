namespace OpenWC3.Game;

/// <summary>CAbilityResistantSkin +1a0 advertises hero spell rules, not a shield.</summary>
public sealed class RuntimeResistantSkinAbility : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool ProvidesResistantSkin=>!IsDisabled&&Level>=1;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
}
