namespace OpenWC3;

/// <summary>Abilities whose cast uses another action's clock (for example arrow attacks).</summary>
public interface ICastOrderTiming
{
    bool CanInterruptCastPoint => true;
    bool IsCastReady(Unit target);
    (float Point,float Recovery) PrepareCastTiming(Unit target);
}
