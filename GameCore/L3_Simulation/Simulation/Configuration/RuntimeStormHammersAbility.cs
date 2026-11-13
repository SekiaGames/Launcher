namespace OpenWC3;

// Rhhb's rasd effect changes the authored mline weapon's spill distance.
public sealed class RuntimeStormHammersAbility : Ability
{
    public override string Name=>"风暴战锤";
    public override bool IsPassive=>true;
    public override AbilityTargetMode TargetMode=>AbilityTargetMode.None;
}
