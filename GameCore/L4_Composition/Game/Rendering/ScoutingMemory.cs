using Godot;

namespace OpenWC3.Game;

internal sealed class ScoutingMemory<T>
{
    internal readonly record struct Entry(float X, float Y, T State);

    private readonly Dictionary<int, Entry> _entries = new();
    private readonly List<int> _forgotten = new();

    internal IEnumerable<KeyValuePair<int, Entry>> Entries => _entries;

    internal void Begin(FogOfWar fog, Dictionary<int, T> retired = null)
    {
        retired?.Clear();
        _forgotten.Clear();
        foreach (var pair in _entries)
            if (fog == null || fog.IsVisible(new Vector3(pair.Value.X, 0f, pair.Value.Y)))
                _forgotten.Add(pair.Key);
        foreach (int id in _forgotten)
        {
            if (retired != null) retired[id] = _entries[id].State;
            _entries.Remove(id);
        }
    }

    internal void Observe(int id, float x, float y, T state) => _entries[id] = new(x, y, state);
}