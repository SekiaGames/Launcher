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
    }

    public bool Remove(Unit item)
    {
        bool removed = _items.Remove(item);
        if (removed && item != null && item.World == _world) item.Removed = true;
        return removed;
    }

    public void Insert(int index, Unit item)
    {
        if (item == null) return;
        item.World = _world;
        _items.Insert(index, item);
    }

    public Unit Find(Predicate<Unit> match) => _items.Find(match);

    public bool Exists(Predicate<Unit> match) => _items.Exists(match);

    public int RemoveAll(Predicate<Unit> match) => _items.RemoveAll(match);

    public Unit this[int index] { get => _items[index]; set => _items[index] = value; }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public void Clear() => _items.Clear();

    public bool Contains(Unit item) => _items.Contains(item);

    public void CopyTo(Unit[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<Unit> GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(Unit item) => _items.IndexOf(item);

    public void RemoveAt(int index) => _items.RemoveAt(index);

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
