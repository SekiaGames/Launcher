namespace OpenWC3;

public sealed class AbilityContainer : IList<Ability>
{
    readonly List<Ability> _items = new();

    Hero _heroView;

    public Unit Owner { get; set; }

    public IReadOnlyList<Ability> Items => _items;

    public Ability Get(string rawcode)
    {
        if (string.IsNullOrEmpty(rawcode)) return null;
        for (int i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i].Id, rawcode, StringComparison.OrdinalIgnoreCase)) return _items[i];
        }
        return null;
    }

    public bool Contains(string rawcode) => Get(rawcode) != null;

    public void CancelChannel()
    {
        Unit owner = Owner;
        if (owner == null)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Owner == null) continue;
                owner = _items[i].Owner;
                break;
            }
        }
        if (owner?.World != null) owner.World.CancelChannel(owner);
    }

    public T Get<T>() where T : Ability
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] is T typed) return typed;
        }
        // 客户端的 Hero 是挂在技能表里的能力对象；Launcher 的英雄状态在 Unit 上，
        // 这里给英雄单位现场造一个同名三围视图，客户端技能按 Get<Hero>() 取值即可。
        if (typeof(T) == typeof(Hero) && Owner is { IsHero: true } heroUnit)
        {
            _heroView ??= new Hero(heroUnit);
            return (T)(Ability)_heroView;
        }
        return null;
    }

    public T Get<T>(string rawcode) where T : Ability
    {
        Ability found = Get(rawcode);
        return found as T;
    }

    public List<Ability> Snapshot() => new(_items);

    public bool Remove(string rawcode)
    {
        Ability found = Get(rawcode);
        if (found == null) return false;
        _items.Remove(found);
        return true;
    }

    public int RemoveAll(Predicate<Ability> match) => _items.RemoveAll(match);

    public bool Exists(Predicate<Ability> match) => _items.Exists(match);

    public Ability Find(Predicate<Ability> match) => _items.Find(match);

    public List<TOut> ConvertAll<TOut>(Converter<Ability, TOut> converter) => _items.ConvertAll(converter);

    public Ability this[int index] { get => _items[index]; set => _items[index] = value; }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public void Add(Ability item) => _items.Add(item);

    public void Clear() => _items.Clear();

    public bool Contains(Ability item) => _items.Contains(item);

    public void CopyTo(Ability[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<Ability> GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(Ability item) => _items.IndexOf(item);

    public void Insert(int index, Ability item) => _items.Insert(index, item);

    public bool Remove(Ability item) => _items.Remove(item);

    public void RemoveAt(int index) => _items.RemoveAt(index);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
