using Godot;

namespace OpenWC3;

public sealed class DetectPassive : Ability
{
    public override bool IsPassive => true;
    public override float PassiveDetectRadius => CastRange > 0f ? CastRange : 900f;
}
