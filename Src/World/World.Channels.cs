using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 引导（channel）系统。对标客户端 Src/Skills/Bloodstorm.cs + BloodstormEffect：
/// 施法者挂一个引导状态，世界每 tick 推进；每次「波」由技能自己结算（伤害/治疗/减速）。
/// 任一条件成立就中断：施法者死亡、眩晕、沉默、被下令移动/攻击、施放别的技能。
/// </summary>
public sealed partial class World
{
    public event Action<Unit, Ability, Vector3> ChannelStarted;
    public event Action<Unit, Ability> ChannelWave;
    /// <summary>第二个参数为 true 表示被打断（false = 正常走完）。</summary>
    public event Action<Unit, Ability, bool> ChannelEnded;

    private sealed class ChannelState
    {
        public Unit Caster;
        public Ability Ability;
        public Vector3 Point;
        public float Duration;
        public float Remaining;
        public float Interval;
        public float Accumulator;
    }

    private readonly List<ChannelState> _channels = new();

    public int ChannelCount => _channels.Count;

    public bool IsChanneling(Unit unit) => ChannelOf(unit) != null;

    private ChannelState ChannelOf(Unit unit)
    {
        if (unit == null) return null;
        foreach (ChannelState state in _channels)
        {
            if (ReferenceEquals(state.Caster, unit)) return state;
        }
        return null;
    }

    /// <summary>
    /// 开始引导。duration 为总时长，interval 为每波间隔（&lt;=0 表示不结算波，只摆姿势）。
    /// 同一施法者已有引导时先静默中断旧的。
    /// </summary>
    public bool BeginChannel(Unit caster, Ability ability, Vector3 point, float duration, float interval)
    {
        if (caster == null || ability == null || !caster.Alive) return false;
        if (duration <= 0f) return false;

        InterruptChannel(caster);

        var state = new ChannelState
        {
            Caster = caster,
            Ability = ability,
            Point = point,
            Duration = duration,
            Remaining = duration,
            Interval = Mathf.Max(0f, interval),
            Accumulator = 0f,
        };
        _channels.Add(state);

        caster.ChannelAbility = ability;
        caster.ChannelRemaining = duration;
        caster.ChannelPoint = point;
        caster.CancelAttack();
        caster.Target = null;
        caster.MoveTarget = null;
        caster.Path = null;
        caster.View?.Play("Spell");

        ChannelStarted?.Invoke(caster, ability, point);
        return true;
    }

    /// <summary>推进所有引导：先判中断，再按间隔结算波，最后判到期。</summary>
    public void TickChannels()
    {
        for (int index = _channels.Count - 1; index >= 0; index--)
        {
            ChannelState state = _channels[index];
            Unit caster = state.Caster;

            if (caster == null || !caster.Alive || !ReferenceEquals(caster.ChannelAbility, state.Ability))
            {
                EndChannel(index, interrupted: true);
                continue;
            }

            // 眩晕 / 沉默 / 变羊这类状态一律打断（客户端 Unit.State.Channeling 同口径）
            if (caster.State.Stunned || caster.State.Silenced)
            {
                EndChannel(index, interrupted: true);
                continue;
            }

            state.Remaining -= Tick;
            if (state.Interval > 0f)
            {
                state.Accumulator += Tick;
                while (state.Accumulator >= state.Interval && state.Remaining > -state.Interval)
                {
                    state.Accumulator -= state.Interval;
                    state.Ability.OnChannelWave(this, state.Point);
                    ChannelWave?.Invoke(caster, state.Ability);
                }
            }

            caster.ChannelRemaining = Mathf.Max(0f, state.Remaining);
            if (state.Remaining <= 0f) EndChannel(index, interrupted: false);
        }
    }

    /// <summary>主动中断（下令移动/攻击、施放别的技能、死亡时调用）。</summary>
    public void InterruptChannel(Unit unit)
    {
        for (int index = _channels.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(_channels[index].Caster, unit)) continue;
            EndChannel(index, interrupted: true);
        }

        if (unit != null && unit.ChannelAbility != null)
        {
            unit.ChannelAbility = null;
            unit.ChannelRemaining = 0f;
        }
    }

    private void EndChannel(int index, bool interrupted)
    {
        ChannelState state = _channels[index];
        _channels.RemoveAt(index);

        Unit caster = state.Caster;
        if (caster != null && ReferenceEquals(caster.ChannelAbility, state.Ability))
        {
            caster.ChannelAbility = null;
            caster.ChannelRemaining = 0f;
            if (!interrupted) caster.View?.Play("Stand");
        }

        ChannelEnded?.Invoke(caster, state.Ability, interrupted);
        state.Ability?.OnChannelEnded(this, interrupted);
    }

    /// <summary>该点能不能站人（没有寻路网格时一律放行，方便探针在空世界里跑）。</summary>
    public bool CanStandAt(Vector3 point, float radius)
    {
        return _pathGrid == null || _pathGrid.IsAreaFree(point, radius);
    }

    /// <summary>范围内的敌方单位（技能 AoE 统一走这里，避开每处各写一遍过滤）。</summary>
    public List<Unit> EnemiesInRange(Unit source, Vector3 point, float radius, bool hitBuildings = true)
    {
        var result = new List<Unit>();
        if (source == null) return result;

        foreach (Unit other in Units)
        {
            if (other == null || !other.Alive || ReferenceEquals(other, source)) continue;
            if (IsAllied(source.Player, other.Player)) continue;
            if (!hitBuildings && other.IsBuilding) continue;
            if (other.State.Invulnerable) continue;
            if (other.State.MagicImmune) continue;

            float dx = other.Position.X - point.X;
            float dz = other.Position.Z - point.Z;
            if (Mathf.Sqrt(dx * dx + dz * dz) > radius + other.Radius) continue;
            result.Add(other);
        }
        return result;
    }

    /// <summary>
    /// 施法者到落点这条线段上的敌方单位（震荡波 / 腐臭蜂群 / 穿刺共用）。
    /// 判定用「到线段的垂直距离 ≤ width + 半径」，所以贴边走的目标也算命中。
    /// </summary>
    public List<Unit> EnemiesAlongLine(Unit source, Vector3 from, Vector3 to, float width,
        bool groundOnly = false)
    {
        var result = new List<Unit>();
        if (source == null) return result;

        Vector3 direction = new(to.X - from.X, 0f, to.Z - from.Z);
        float length = direction.Length();
        if (length < 1f) return result;
        direction /= length;

        foreach (Unit other in Units)
        {
            if (other == null || !other.Alive || ReferenceEquals(other, source)) continue;
            if (IsAllied(source.Player, other.Player)) continue;
            if (groundOnly && other.Type != null && other.Type.Flying) continue;
            if (other.State.Invulnerable) continue;
            if (other.State.MagicImmune) continue;

            Vector3 offset = new(other.Position.X - from.X, 0f, other.Position.Z - from.Z);
            float along = offset.X * direction.X + offset.Z * direction.Z;
            if (along < -other.Radius || along > length + other.Radius) continue;

            float perpendicular = Mathf.Abs(offset.X * direction.Z - offset.Z * direction.X);
            if (perpendicular > width + other.Radius) continue;
            result.Add(other);
        }
        return result;
    }

    /// <summary>范围内的友军（医疗波这类跳跃治疗用）。</summary>
    public List<Unit> AlliesInRange(Unit source, Vector3 point, float radius)
    {
        var result = new List<Unit>();
        if (source == null) return result;

        foreach (Unit other in Units)
        {
            if (other == null || !other.Alive) continue;
            if (!IsAllied(source.Player, other.Player)) continue;

            float dx = other.Position.X - point.X;
            float dz = other.Position.Z - point.Z;
            if (Mathf.Sqrt(dx * dx + dz * dz) > radius + other.Radius) continue;
            result.Add(other);
        }
        return result;
    }
}
