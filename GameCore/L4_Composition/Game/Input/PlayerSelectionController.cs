using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenWC3.Game;

internal sealed class PlayerSelectionController
{
    private readonly World _world;
    private readonly Func<int> _localPlayerId;
    private readonly Action _changed;
    private readonly List<Unit> _items;
    private readonly List<Unit>[] _controlGroups =
        [new(), new(), new(), new(), new(), new(), new(), new(), new(), new()];

    internal PlayerSelectionController(World world, Func<int> localPlayerId, Action changed,
        List<Unit> items = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
        _changed = changed;
        _items = items ?? new List<Unit>();
    }

    internal Action<Unit, bool> SelectedAck { get; set; }

    internal const int MaxSelection = 12;

    internal IReadOnlyList<Unit> Items => _items;
    internal Unit Primary { get; private set; }

    internal static bool SameCommandSubgroup(Unit primary, Unit candidate)
        => primary != null && candidate != null && (ReferenceEquals(primary, candidate)
            || (!CommandHeroLike(primary) && !CommandHeroLike(candidate)
                && primary.TypeId == candidate.TypeId
                && (primary.IsIllusion) == (candidate.IsIllusion)));

    internal static bool CommandHeroLike(Unit unit)
        => unit != null && unit.IsHero && !unit.IsIllusion;

    internal void SelectInitial(Unit unit)
    {
        _items.Clear();
        Primary = CanSelect(unit) ? unit : null;
        if (Primary != null)
        {
            _items.Add(Primary);
            Fire(GameEvent.UnitSelected, Primary);
        }
        _changed?.Invoke();
    }

    internal void ApplyScriptSnapshot(IEnumerable<Unit> units)
    {
        _items.Clear();
        _items.AddRange((units ?? Array.Empty<Unit>()).Where(CanSelect)
            .Distinct().Take(MaxSelection));
        Primary = _items.FirstOrDefault();
        _changed?.Invoke();
    }

    internal void SelectFromGroup(Unit unit)
    {
        if (unit == null || !_items.Contains(unit)) return;
        Primary = unit;
        _changed?.Invoke();
    }

    internal void Remove(Unit unit)
    {
        if (unit == null || !_items.Remove(unit)) return;
        Fire(GameEvent.UnitDeselected, unit);
        if (ReferenceEquals(Primary, unit))
            Primary = _items.FirstOrDefault(candidate => candidate?.Alive == true);
        _changed?.Invoke();
    }

    internal void SelectClicked(Unit unit, bool additive, bool mouseChannel = false)
    {
        if (!CanSelect(unit)) return;

        Unit[] previous = _items.ToArray();
        bool repeatedSelection = !additive && previous.Contains(unit);
        if (!additive) _items.Clear();
        if (additive && _items.Contains(unit))
            _items.Remove(unit);
        else if (!_items.Contains(unit) && _items.Count < MaxSelection)
            _items.Add(unit);
        if (_items.Count == 0 && !(mouseChannel && additive))
            _items.Add(unit);

        foreach (Unit removed in previous.Where(value => !_items.Contains(value)))
            Fire(GameEvent.UnitDeselected, removed);
        foreach (Unit added in _items.Where(value => !previous.Contains(value)))
            Fire(GameEvent.UnitSelected, added);
        if (repeatedSelection && _items.Contains(unit))
            Fire(GameEvent.UnitSelected, unit);

        Primary = _items.Contains(unit) ? unit : _items.FirstOrDefault();
        _changed?.Invoke();
        if (Primary != null) SelectedAck?.Invoke(Primary, previous.Contains(Primary));
    }

    internal void SelectRect(IEnumerable<Unit> units, bool additive)
    {
        Unit[] previous = _items.ToArray();
        if (!additive) _items.Clear();
        foreach (Unit unit in units)
        {
            if (_items.Count >= MaxSelection) break;
            if (CanSelect(unit) && !_items.Contains(unit)) _items.Add(unit);
        }
        foreach (Unit removed in previous.Where(value => !_items.Contains(value)))
            Fire(GameEvent.UnitDeselected, removed);
        foreach (Unit added in _items.Where(value => !previous.Contains(value)))
            Fire(GameEvent.UnitSelected, added);
        Primary = _items.FirstOrDefault();
        _changed?.Invoke();
        if (Primary != null) SelectedAck?.Invoke(Primary, previous.Contains(Primary));
    }

    internal Unit[] LiveControlled() =>
        _items.Where(unit => unit != null && unit.Alive
            && !unit.Hidden && !unit.State.Paused
            && _world.CanControl(unit, _localPlayerId())).ToArray();

    internal Unit[] MovableControlled() =>
        LiveControlled().Where(unit => unit.CanMove).ToArray();

    internal void Clear()
    {
        foreach (Unit unit in _items.ToArray()) Fire(GameEvent.UnitDeselected, unit);
        _items.Clear();
        Primary = null;
        _changed?.Invoke();
    }

    internal void SetControlGroup(int index)
    {
        if ((uint)index >= (uint)_controlGroups.Length) return;
        List<Unit> group = _controlGroups[index];
        group.Clear();
        group.AddRange(_items.Where(unit => unit?.Alive == true && !unit.Removed
            && _world.CanControl(unit, _localPlayerId())).Take(MaxSelection));
    }

    internal void AddToControlGroup(int index)
    {
        if ((uint)index >= (uint)_controlGroups.Length) return;
        List<Unit> group = _controlGroups[index];
        foreach (Unit unit in _items)
        {
            if (group.Count >= MaxSelection) break;
            if (unit?.Alive != true || unit.Removed) continue;
            if (!_world.CanControl(unit, _localPlayerId())) continue;
            if (!group.Contains(unit)) group.Add(unit);
        }
    }

    internal void RecallControlGroup(int index)
    {
        if ((uint)index >= (uint)_controlGroups.Length) return;
        Unit[] recalled = _controlGroups[index].Where(unit => CanSelect(unit)
            && _world.CanControl(unit, _localPlayerId())).Take(MaxSelection).ToArray();
        _controlGroups[index].Clear();
        _controlGroups[index].AddRange(recalled);
        if (recalled.Length == 0) return;
        Unit[] previous = _items.ToArray();
        _items.Clear();
        _items.AddRange(recalled);
        foreach (Unit removed in previous.Where(unit => !_items.Contains(unit)))
            Fire(GameEvent.UnitDeselected, removed);
        foreach (Unit added in _items.Where(unit => !previous.Contains(unit)))
            Fire(GameEvent.UnitSelected, added);
        Primary = _items[0];
        _changed?.Invoke();
        SelectedAck?.Invoke(Primary, previous.Contains(Primary));
    }

    internal void RecallSnapshot(IReadOnlyList<Unit> units)
    {
        Unit[] recalled = (units ?? Array.Empty<Unit>()).Where(CanSelect)
            .Distinct().Take(MaxSelection).ToArray();
        if (recalled.Length == 0) return;
        Unit[] previous = _items.ToArray();
        _items.Clear();
        _items.AddRange(recalled);
        foreach (Unit removed in previous.Where(unit => !_items.Contains(unit)))
            Fire(GameEvent.UnitDeselected, removed);
        foreach (Unit added in _items.Where(unit => !previous.Contains(unit)))
            Fire(GameEvent.UnitSelected, added);
        Primary = _items[0];
        _changed?.Invoke();
        SelectedAck?.Invoke(Primary, previous.Contains(Primary));
    }

    internal void SelectSingle(Unit unit)
    {
        if (!CanSelect(unit)) return;
        Unit[] previous = _items.ToArray();
        _items.Clear();
        _items.Add(unit);
        foreach (Unit removed in previous.Where(value => !ReferenceEquals(value, unit)))
            Fire(GameEvent.UnitDeselected, removed);
        if (!previous.Contains(unit)) Fire(GameEvent.UnitSelected, unit);
        Primary = unit;
        _changed?.Invoke();
        SelectedAck?.Invoke(unit, previous.Contains(unit));
    }

    internal void CycleSubgroup()
    {
        if (_items.Count < 2) return;
        Unit primary = Primary;
        int current = -1;
        for (int i = 0; i < _items.Count; i++)
            if (ReferenceEquals(_items[i], primary)) { current = i; break; }
        int count = _items.Count;
        for (int step = 1; step <= count; step++)
        {
            int index = (current + step + count) % count;
            Unit candidate = _items[index];
            if (candidate == null) continue;
            if (primary != null && SameCommandSubgroup(primary, candidate)) continue;
            SelectFromGroup(candidate);
            return;
        }
    }

    private bool CanSelect(Unit unit)
    {
        if (unit?.Alive != true || unit.Removed || unit.Hidden) return false;
        if (unit.Player == _localPlayerId()) return true;
        return _world.FogVisible == null
            || _world.FogVisible(unit.Pos.X, unit.Pos.Y);
    }

    private void Fire(GameEvent gameEvent, Unit unit)
    {
        _world.Events.Fire(new EventContext
        {
            Event = gameEvent,
            Source = unit,
            Target = unit,
            TriggerUnit = unit,
            TriggerPlayerId = _localPlayerId()
        });
    }
}