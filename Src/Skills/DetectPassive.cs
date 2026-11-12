using Godot;

namespace Launcher;

public sealed class DetectPassive : Ability
{
    public override bool IsPassive => true;
    public override float PassiveDetectRadius => CastRange > 0f ? CastRange : 900f;
}
