namespace OpenWC3.Game;

/// <summary>Aimp marker; Repb's rasd/renw effects configure the line weapon.</summary>
public sealed class RuntimeImpalingBoltAbility : Ability
{
    public override string Name=>ObjectData?.GetString("Name",Rawcode)??Rawcode;
    public override bool IsPassive=>true;
    public override bool CheckCanUse(Unit target,Vec2 point)=>false;
}
