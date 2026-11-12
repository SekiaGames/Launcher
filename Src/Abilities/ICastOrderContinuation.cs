namespace OpenWC3;

/// <summary>A completed effect may keep its order while another unit finishes a paired action.</summary>
public interface ICastOrderContinuation
{
    bool WaitForOrderCompletion { get; }
    void OnCastOrderEnded(bool interrupted);
}
