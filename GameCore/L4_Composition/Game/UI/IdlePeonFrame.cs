using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

internal sealed class IdlePeonFrame : ButtonFrame
{
    internal const float ButtonSize = 48.64f;
    internal const float OffsetX = 4.8f;
    internal const float OffsetYUp = 217.6f;
    const float ClickIntervalSeconds = 1f;
    const string IconFallback = @"ReplaceableTextures\CommandButtons\BTNPeon.blp";
    const string DisabledFallback = @"ReplaceableTextures\CommandButtonsDisabled\DISBTNPeon.blp";

    readonly List<Unit> _slots = new();
    readonly List<Unit> _candidates = new();
    readonly HashSet<Unit> _present = new();
    readonly HashSet<Unit> _slotted = new();
    int _index;
    float _lastClickTime;
    int _cachedCount = -1;

    public Action<Unit> OnIdlePeonClick;

    public IdlePeonFrame(IUiTextureResolver textures, War3UiSkin skin) : base("IdlePeon")
    {
        string path = skin?.Get("IdlePeon", IconFallback) ?? IconFallback;
        Icon = textures.Resolve(path) ?? textures.Resolve(IconFallback);
        DisabledIcon = textures.Resolve(NativeCommandButtonBinding.DisabledArt(path,
            @"ReplaceableTextures\CommandButtonsDisabled\"));
        ActiveHighlight = textures.Resolve(@"UI\Widgets\Console\Human\CommandButton\human-activebutton.blp");
        TooltipTitle = "闲置的农民";
        TooltipBody = "一个或几个农民没有参与工作。";
        Disabled = false;
        Visible = false;
        Clicked += _ => TriggerIdlePeon();
    }

    public void Layout(float viewportWidth, float viewportHeight)
    {
        float top = viewportHeight - OffsetYUp - ButtonSize;
        RenderBounds = new UiRect(OffsetX, top, ButtonSize, ButtonSize);
    }

    public void Update(IReadOnlyList<Unit> units, int localPlayerId, float now)
    {
        _candidates.Clear();
        _present.Clear();
        if (units != null)
        {
            foreach (Unit unit in units)
            {
                if (unit == null || unit.Player != localPlayerId || !IsIdleWorkerCandidate(unit))
                    continue;
                _candidates.Add(unit);
                _present.Add(unit);
            }
        }

        _slotted.Clear();
        for (int i = 0; i < _slots.Count; i++)
        {
            Unit unit = _slots[i];
            if (unit != null && !_present.Contains(unit)) _slots[i] = null;
            else if (unit != null) _slotted.Add(unit);
        }

        int nextEmpty = 0;
        foreach (Unit unit in _candidates)
        {
            if (!_slotted.Add(unit)) continue;
            while (nextEmpty < _slots.Count && _slots[nextEmpty] != null) nextEmpty++;
            if (nextEmpty < _slots.Count) _slots[nextEmpty++] = unit;
            else _slots.Add(unit);
        }

        int count = CountIdle();
        if (count == _cachedCount) return;
        _cachedCount = count;
        OverlayText = count > 0 ? count.ToString() : null;
        Visible = count > 0;
        GD.Print($"[IDLE] 空闲农民 {count}");
    }

    public void TriggerIdlePeon()
    {
        if (_slots.Count == 0 || !Visible) return;
        int start = _lastClickTime <= 0f || Time.GetTicksMsec() / 1000f >= _lastClickTime + ClickIntervalSeconds
            ? 0 : (_index + 1) % _slots.Count;
        for (int step = 0; step < _slots.Count; step++)
        {
            int index = (start + step) % _slots.Count;
            Unit unit = _slots[index];
            if (unit == null || !IsIdleWorker(unit)) continue;
            _index = index;
            _lastClickTime = Time.GetTicksMsec() / 1000f;
            OnIdlePeonClick?.Invoke(unit);
            return;
        }
    }

    int CountIdle()
    {
        int count = 0;
        for (int i = _slots.Count - 1; i >= 0; i--)
            if (IsIdleWorker(_slots[i])) count++;
        return count;
    }

    internal static bool IsIdleWorkerCandidate(Unit unit) =>
        unit != null && unit.Is(Classification.Peon) && unit.Alive
        && !unit.Hidden && !unit.IsCargoed;

    internal static bool IsIdleWorker(Unit unit) =>
        IsIdleWorkerCandidate(unit)
        && unit.GoldMine == null && unit.Tree == null && unit.BuildSite == null
        && unit.RepairTarget == null && !unit.MoveTarget.HasValue
        && unit.Target == null && !unit.BuildingNow;
}