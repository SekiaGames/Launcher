using System;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>对标客户端 CUnit 的 stateModBuff：计数式叠加，多来源互不覆盖。</summary>
public enum StateMod
{
    Stun,
    Snare,
    Sleep,
    Disarm,
    Silence,
    Ethereal,
    Invisible,
    Invulnerable,
    MagicImmune,
    Resistant,
    Detect,
    Pause,
    RepairDisabled,
    PhysicalImmune,
    /// <summary>变形（妖术变羊）：禁攻击禁施法，但仍能移动。</summary>
    Morph,
    MorphImmune,
    Detector,
    DisableAutoAttack,
    DisableAutoCast,
    DisableUnitCollision,
    DisableBuildingCollision,
    InternalPause,
    PauseTimedLife,
    Phased,
}

public sealed class State
{
    private readonly Dictionary<StateMod, int> _counts = new();
    private readonly Dictionary<StateMod, float> _remaining = new();

    /// <summary>负面状态的时长缩放（抗性皮肤 = 0.5）。</summary>
    public float DebuffScale = 1f;

    private static bool IsDebuff(StateMod mod) => mod is StateMod.Stun or StateMod.Snare or StateMod.Sleep
        or StateMod.Disarm or StateMod.Silence or StateMod.Ethereal or StateMod.Morph or StateMod.Pause;

    public bool Has(StateMod mod) => _counts.TryGetValue(mod, out int count) && count > 0;

    public int Count(StateMod mod) => _counts.TryGetValue(mod, out int count) ? count : 0;

    public float Remaining(StateMod mod) => _remaining.TryGetValue(mod, out float seconds) ? seconds : 0f;

    public void Add(StateMod mod, float seconds = 0f)
    {
        _counts[mod] = Count(mod) + 1;
        if (seconds <= 0f) return;
        if (IsDebuff(mod)) seconds *= Math.Max(0.1f, DebuffScale);
        float current = Remaining(mod);
        _remaining[mod] = Math.Max(current, seconds);
    }

    public void Remove(StateMod mod)
    {
        int count = Count(mod);
        if (count <= 0) return;
        if (count == 1)
        {
            _counts.Remove(mod);
            _remaining.Remove(mod);
        }
        else
        {
            _counts[mod] = count - 1;
        }
    }

    public void RemoveAll(StateMod mod)
    {
        _counts.Remove(mod);
        _remaining.Remove(mod);
    }

    public void FrameSeconds(float delta)
    {
        if (_remaining.Count == 0) return;

        var expired = new List<StateMod>();
        foreach (KeyValuePair<StateMod, float> pair in _remaining)
        {
            float left = pair.Value - delta;
            if (left <= 0f) expired.Add(pair.Key);
            else _remaining[pair.Key] = left;
        }

        foreach (StateMod mod in expired)
        {
            RemoveAll(mod);
            Expired?.Invoke(mod);
        }
    }

    public event Action<StateMod> Expired;

    public bool Stunned => Has(StateMod.Stun) || Has(StateMod.Sleep) || Has(StateMod.Pause);
    public bool Snared => Has(StateMod.Snare);
    public bool Disarmed => Has(StateMod.Disarm) || Has(StateMod.Morph);
    public bool Silenced => Has(StateMod.Silence) || Has(StateMod.Stun) || Has(StateMod.Morph);
    public bool Ethereal => Has(StateMod.Ethereal);
    public bool Invisible => Has(StateMod.Invisible);
    public bool Invulnerable => Has(StateMod.Invulnerable);
    public bool MagicImmune => Has(StateMod.MagicImmune);
    public bool PhysicalImmune => Has(StateMod.PhysicalImmune);

    public bool CanMove => !Stunned && !Snared;
    public bool CanAttack => !Stunned && !Disarmed && !Ethereal;
    public bool CanCast => !Stunned && !Silenced;

    public void Clear()
    {
        _counts.Clear();
        _remaining.Clear();
    }

    public string Describe()
    {
        if (_counts.Count == 0) return "-";

        var parts = new List<string>();
        foreach (KeyValuePair<StateMod, int> pair in _counts)
        {
            if (pair.Value <= 0) continue;
            float left = Remaining(pair.Key);
            parts.Add(left > 0f ? $"{pair.Key}×{pair.Value}({left:0.0}s)" : $"{pair.Key}×{pair.Value}");
        }
        return parts.Count == 0 ? "-" : string.Join(",", parts);
    }
}
