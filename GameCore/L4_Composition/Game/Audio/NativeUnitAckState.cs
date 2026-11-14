namespace OpenWC3.Game;

public sealed class NativeUnitAckState
{
    private int _lastUnit = -1;
    private readonly System.Collections.Generic.Dictionary<int, double> _responseEnd = new();
    private readonly System.Collections.Generic.Dictionary<int, double> _usernameEnd = new();

    public int SelectionCount { get; private set; }

    public void ResetSelection()
    {
        _lastUnit = -1;
        SelectionCount = 0;
    }

    public void BeginSelection(int identity, bool wasSelected)
    {
        if (!wasSelected || identity != _lastUnit) ResetSelection();
        _lastUnit = identity;
    }

    public int PissedIndex(int pissedCount)
    {
        if (SelectionCount < 3) return -1;
        int index = SelectionCount - 3;
        if (index < pissedCount) return index;
        SelectionCount = 0;
        return -1;
    }

    public bool CanRespond(int identity, double now)
        => !_responseEnd.TryGetValue(identity, out double end) || now >= end;

    public bool CanPlayUsername(int? username, double now)
        => !username.HasValue || !_usernameEnd.TryGetValue(username.Value, out double end) || now >= end;

    public void Played(int identity, double now, float duration, bool selection, int? username = null)
    {
        _responseEnd[identity] = now + duration + .250;
        if (username.HasValue) _usernameEnd[username.Value] = now + duration;
        if (selection) SelectionCount++;
    }
}
