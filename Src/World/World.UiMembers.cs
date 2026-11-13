using System;
using System.Collections.Generic;

namespace OpenWC3;

public sealed partial class World
{
    readonly List<Unit> _uiHeroes = new();
    readonly List<Unit> _uiWorkers = new();
    long _uiMemberCollectionVersion = -1;
    bool _uiClassificationDirty = true;
    Action<Unit> _uiClassificationChanged;
    internal int UiMemberIndexRebuildCount { get; private set; }

    internal void OnUiCandidateAdded(Unit unit)
        => unit.Identity.UiClassificationMembershipChanged +=
            _uiClassificationChanged ??= UiClassificationChanged;

    internal void OnUiCandidateRemoved(Unit unit)
        => unit.Identity.UiClassificationMembershipChanged -= _uiClassificationChanged;

    void UiClassificationChanged(Unit _) => _uiClassificationDirty = true;

    void EnsureUiMembers()
    {
        if (!_uiClassificationDirty && _uiMemberCollectionVersion == UnitCollectionVersion) return;
        _uiHeroes.Clear();
        _uiWorkers.Clear();
        for (int i = 0; i < Units.Count; i++)
        {
            Unit unit = Units[i];
            if (unit.IsHero) _uiHeroes.Add(unit);
            if (unit.Is(Classification.Peon)) _uiWorkers.Add(unit);
        }
        _uiMemberCollectionVersion = UnitCollectionVersion;
        _uiClassificationDirty = false;
        UiMemberIndexRebuildCount++;
    }

    public IReadOnlyList<Unit> HeroUiCandidates()
    {
        EnsureUiMembers();
        return _uiHeroes;
    }

    public IReadOnlyList<Unit> WorkerUiCandidates()
    {
        EnsureUiMembers();
        return _uiWorkers;
    }
}