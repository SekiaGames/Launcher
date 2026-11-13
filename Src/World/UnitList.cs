using System.Collections;

namespace OpenWC3;

/// <summary>
/// 世界单位表。入表时把世界引用打到单位上，客户端技能里的 Owner.World 才有值。
/// </summary>
public sealed class UnitList : IList<Unit>
{
    readonly World _world;
    readonly List<Unit> _items = new();

    public UnitList(World world) => _world = world;

    public IReadOnlyList<Unit> Items => _items;

    public void Add(Unit item)
    {
        if (item == null) return;
        item.World = _world;
        item.Removed = false;
        _items.Add(item);
        _world.BumpUnitCollectionVersion();
        _world.OnUiCandidateAdded(item);
    }

    public bool Remove(Unit item)
    {
        bool removed = _items.Remove(item);
        if (removed && item != null)
        {
            if (item.World == _world) item.Removed = true;
            _world.BumpUnitCollectionVersion();
            _world.OnUiCandidateRemoved(item);
        }
        return removed;
    }

    public void Insert(int index, Unit item)
    {
        if (item == null) return;
        item.World = _world;
        item.Removed = false;
        _items.Insert(index, item);
        _world.BumpUnitCollectionVersion();
        _world.OnUiCandidateAdded(item);
    }

    public Unit Find(Predicate<Unit> match) => _items.Find(match);

    public bool Exists(Predicate<Unit> match) => _items.Exists(match);

    public int RemoveAll(Predicate<Unit> match)
    {
        int removed = 0;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            Unit item = _items[i];
            if (!match(item)) continue;
            _items.RemoveAt(i);
            _world.OnUiCandidateRemoved(item);
            removed++;
        }
        if (removed > 0) _world.BumpUnitCollectionVersion();
        return removed;
    }

    public Unit this[int index]
    {
        get => _items[index];
        set
        {
            if (ReferenceEquals(_items[index], value)) return;
            Unit previous = _items[index];
            if (previous != null) _world.OnUiCandidateRemoved(previous);
            if (value != null)
            {
                value.World = _world;
                value.Removed = false;
                _world.OnUiCandidateAdded(value);
            }
            _items[index] = value;
            _world.BumpUnitCollectionVersion();
        }
    }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public void Clear()
    {
        if (_items.Count == 0) return;
        foreach (Unit item in _items)
            if (item != null) _world.OnUiCandidateRemoved(item);
        _items.Clear();
        _world.BumpUnitCollectionVersion();
    }

    public bool Contains(Unit item) => _items.Contains(item);

    public void CopyTo(Unit[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<Unit> GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(Unit item) => _items.IndexOf(item);

    public void RemoveAt(int index)
    {
        Unit item = _items[index];
        _items.RemoveAt(index);
        if (item != null)
        {
            if (item.World == _world) item.Removed = true;
            _world.OnUiCandidateRemoved(item);
        }
        _world.BumpUnitCollectionVersion();
    }

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
