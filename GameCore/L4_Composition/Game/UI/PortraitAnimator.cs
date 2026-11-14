using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

internal sealed class PortraitAnimator
{
    const int SeedBase = 0x504F52;
    static readonly string[] TalkTags = { "TALK" };

    Random _random = new(SeedBase);
    UnitSceneComponent _view;
    int _identity = -1;
    string _typeId;
    string[] _properties = Array.Empty<string>();
    string _sequence;
    float _sequenceStart;
    bool _talking;
    float _talkEnd;

    public void Reset(UnitSceneComponent view, Unit unit, float nowSeconds)
    {
        _view = view;
        _identity = unit?.SimulationId ?? -1;
        _typeId = unit?.TypeId;
        _properties = Snapshot(unit);
        _sequence = null;
        _talking = false;
        _sequenceStart = nowSeconds;
        _talkEnd = 0f;
        _random = new Random(SeedBase ^ (_identity * 397));
    }

    public void Talk(Unit unit, float durationSeconds, float nowSeconds)
    {
        if (unit == null || _view == null) return;
        if (unit.SimulationId != _identity
            || !string.Equals(unit.TypeId, _typeId, StringComparison.Ordinal))
        {
            Reset(_view, unit, nowSeconds);
        }
        _talking = true;
        _sequence = null;
        _sequenceStart = nowSeconds;
        _talkEnd = durationSeconds > 0f ? nowSeconds + durationSeconds : 0f;
    }

    public void Frame(UnitSceneComponent view, Unit unit, float nowSeconds)
    {
        if (view == null || unit == null) return;
        if (!ReferenceEquals(view, _view) || unit.SimulationId != _identity
            || !string.Equals(unit.TypeId, _typeId, StringComparison.Ordinal)
            || !SameProperties(_properties, unit.AnimationProperties))
        {
            Reset(view, unit, nowSeconds);
        }
        if (_sequence == null || view.SequenceLength(_sequence) <= 0f)
        {
            _sequence = Pick(view, _talking, null);
            _sequenceStart = nowSeconds;
        }
        if (_sequence == null) return;
        float duration = Math.Max(0.001f, view.SequenceLength(_sequence));
        float elapsed = nowSeconds - _sequenceStart;
        if (_talking)
        {
            bool stillTalking = _talkEnd > 0f ? nowSeconds < _talkEnd : elapsed < duration;
            if (!stillTalking)
            {
                string previous = _sequence;
                _talking = false;
                _sequence = Pick(view, talking: false, previous);
                _sequenceStart = nowSeconds;
                if (_sequence == null) return;
                duration = Math.Max(0.001f, view.SequenceLength(_sequence));
                elapsed = 0f;
            }
        }
        else if (elapsed >= duration)
        {
            string previous = _sequence;
            _sequence = Pick(view, talking: false, previous);
            _sequenceStart = nowSeconds;
            if (_sequence == null) return;
            duration = Math.Max(0.001f, view.SequenceLength(_sequence));
            elapsed = 0f;
        }
        float frame = _talking ? elapsed % duration : Math.Min(elapsed, duration);
        view.PlayPortraitSequence(_sequence, frame);
    }

    string Pick(UnitSceneComponent view, bool talking, string avoid)
    {
        IReadOnlyCollection<string> tags = talking ? TalkTags : Array.Empty<string>();
        string sequence = view.SelectPortraitSequence("Portrait", tags, avoid, _random);
        if (sequence == null)
            sequence = view.SelectPortraitSequence("Stand", tags, avoid, _random);
        if (sequence != null) return sequence;
        IReadOnlyList<string> names = view.AnimationNames;
        return names.Count > 0 ? names[0] : null;
    }

    static string[] Snapshot(Unit unit) => unit?.AnimationProperties?.Count > 0
        ? unit.AnimationProperties.ToArray() : Array.Empty<string>();

    static bool SameProperties(string[] snapshot, List<string> current)
    {
        int count = current?.Count ?? 0;
        if (snapshot.Length != count) return false;
        if (count == 0) return true;
        foreach (string property in current)
        {
            bool found = false;
            foreach (string previous in snapshot)
                if (string.Equals(previous, property, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            if (!found) return false;
        }
        return true;
    }
}