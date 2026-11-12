using Godot;
using System.Collections.Generic;

namespace OpenWC3;

public sealed class SlowSource
{
    public float MovePct;
    public float AttackPct;
    public float Remaining;
    public float MoveStep;
    public float AttackStep;
    public float MovePeriod;
    public float AttackPeriod;
    public float MoveClock;
    public float AttackClock;
    public int MoveStages;
    public int AttackStages;
}

public sealed partial class Unit
{
    public readonly List<SlowSource> Slows = new();

    public void ApplySlow(float movePct, float attackPct, float seconds)
    {
        AddSlow(movePct, attackPct, seconds, 0, 0);
    }

    public SlowSource AddSlow(float movePct, float attackPct, float seconds,
        int moveStages, int attackStages)
    {
        float lifetime = Mathf.Max(0.05f, seconds);
        var source = new SlowSource
        {
            MovePct = Mathf.Clamp(movePct, 0f, 0.95f),
            AttackPct = Mathf.Clamp(attackPct, 0f, 0.95f),
            Remaining = lifetime,
        };

        if (moveStages > 0 && source.MovePct > 0f)
        {
            source.MoveStages = moveStages;
            source.MoveStep = source.MovePct / moveStages;
            source.MovePeriod = lifetime / moveStages;
        }
        if (attackStages > 0 && source.AttackPct > 0f)
        {
            source.AttackStages = attackStages;
            source.AttackStep = source.AttackPct / attackStages;
            source.AttackPeriod = lifetime / attackStages;
        }

        Slows.Add(source);
        RecomputeSlow();
        return source;
    }

    public void ClearSlows()
    {
        Slows.Clear();
        SlowMovePct = 0f;
        SlowAttackPct = 0f;
        SlowRemaining = 0f;
    }

    public void TickSlows(float delta)
    {
        if (Slows.Count == 0)
        {
            if (SlowRemaining != 0f || SlowMovePct != 0f || SlowAttackPct != 0f) RecomputeSlow();
            return;
        }

        for (int index = Slows.Count - 1; index >= 0; index--)
        {
            SlowSource source = Slows[index];
            source.Remaining -= delta;

            if (source.MoveStages > 0 && source.MovePeriod > 0f)
            {
                source.MoveClock += delta;
                while (source.MoveClock >= source.MovePeriod - 0.0001f && source.MovePct > 0f)
                {
                    source.MoveClock -= source.MovePeriod;
                    source.MovePct = Mathf.Max(0f, source.MovePct - source.MoveStep);
                }
            }
            if (source.AttackStages > 0 && source.AttackPeriod > 0f)
            {
                source.AttackClock += delta;
                while (source.AttackClock >= source.AttackPeriod - 0.0001f && source.AttackPct > 0f)
                {
                    source.AttackClock -= source.AttackPeriod;
                    source.AttackPct = Mathf.Max(0f, source.AttackPct - source.AttackStep);
                }
            }

            if (source.Remaining <= 0f) Slows.RemoveAt(index);
        }

        RecomputeSlow();
    }

    private void RecomputeSlow()
    {
        float move = 0f;
        float attack = 0f;
        float remaining = 0f;
        foreach (SlowSource source in Slows)
        {
            move = Mathf.Max(move, source.MovePct);
            attack = Mathf.Max(attack, source.AttackPct);
            remaining = Mathf.Max(remaining, source.Remaining);
        }
        SlowMovePct = move;
        SlowAttackPct = attack;
        SlowRemaining = remaining;
    }
}