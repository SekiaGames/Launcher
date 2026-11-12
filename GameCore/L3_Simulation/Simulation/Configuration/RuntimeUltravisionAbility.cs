namespace OpenWC3.Game;

/// <summary>
/// Ault is the passive Ultravision research marker. The upgrade refresh
/// updates night sight in RefreshUnitTechEffects; this ability never casts or
/// detects invisible units.
/// </summary>
public sealed class RuntimeUltravisionAbility : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
}
