using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public enum UnitOrderKind
{
    Move,
    Attack,
    Patrol,
}

public sealed class UnitOrder
{
    public UnitOrderKind Kind;
    public Vector3 Point;
    public Unit Target;

    public static UnitOrder Move(Vector3 point) => new() { Kind = UnitOrderKind.Move, Point = point };
    public static UnitOrder Attack(Unit target) => new() { Kind = UnitOrderKind.Attack, Target = target };
    public static UnitOrder Patrol(Vector3 point) => new() { Kind = UnitOrderKind.Patrol, Point = point };
}

public sealed partial class World
{
    public const int MaxQueuedOrders = 12;

    public void IssueOrder(Unit unit, UnitOrder order, bool queue)
    {
        if (unit == null || order == null || !unit.Alive || unit.IsBuilding) return;

        if (queue && HasActiveOrder(unit))
        {
            if (unit.PendingOrders.Count >= MaxQueuedOrders) return;
            unit.PendingOrders.Add(order);
            LogOrder(unit, $"排队 {DescribeOrder(order)} 队列 {unit.PendingOrders.Count}");
            return;
        }

        unit.PendingOrders.Clear();
        ApplyOrder(unit, order);
        LogOrder(unit, $"执行 {DescribeOrder(order)}");
    }

    public bool HasActiveOrder(Unit unit)
    {
        if (unit == null) return false;
        return unit.MoveTarget.HasValue || unit.Target != null || unit.Patrolling || unit.HoldingPosition;
    }

    public void ClearQueuedOrders(Unit unit)
    {
        if (unit == null) return;
        unit.PendingOrders.Clear();
        unit.Patrolling = false;
        unit.HoldingPosition = false;
    }

    public void StopUnit(Unit unit)
    {
        if (unit == null || !unit.Alive) return;
        unit.CancelAttack();
        ClearOrders(unit);
        unit.View?.Play("Stand");
        LogOrder(unit, "停止");
    }

    public void HoldPosition(Unit unit)
    {
        if (unit == null || !unit.Alive || unit.IsBuilding) return;
        unit.CancelAttack();
        unit.PendingOrders.Clear();
        unit.Target = null;
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.Patrolling = false;
        unit.HoldingPosition = true;
        unit.Target = FindHeldTarget(unit);
        unit.View?.Play("Stand");
        LogOrder(unit, unit.Target != null ? $"原地据守，锁定 {unit.Target.Type?.Name}" : "原地据守");
    }

    public void AdvanceOrder(Unit unit)
    {
        if (unit == null || unit.PendingOrders.Count == 0) return;
        UnitOrder next = unit.PendingOrders[0];
        unit.PendingOrders.RemoveAt(0);
        ApplyOrder(unit, next);
        LogOrder(unit, $"接单 {DescribeOrder(next)} 剩余队列 {unit.PendingOrders.Count}");
    }

    private void ApplyOrder(Unit unit, UnitOrder order)
    {
        unit.GoldMine = null;
        unit.Tree = null;
        unit.BuildSite = null;
        unit.RepairTarget = null;
        unit.Repairing = false;
        unit.HoldingPosition = false;
        unit.Patrolling = false;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.GroupSpeedCap = 0f;
        unit.GroupMoveGoal = default;
        unit.GroupMoveId = 0;
        unit.Returning = false;
        unit.ReturnDelay = 0f;

        switch (order.Kind)
        {
            case UnitOrderKind.Move:
                unit.Target = null;
                unit.MoveTarget = order.Point;
                break;
            case UnitOrderKind.Attack:
                unit.MoveTarget = null;
                unit.Target = order.Target;
                break;
            case UnitOrderKind.Patrol:
                unit.Target = null;
                unit.PatrolFrom = unit.Position;
                unit.PatrolTo = order.Point;
                unit.MoveTarget = order.Point;
                unit.Patrolling = true;
                break;
        }
    }

    private void StepHoldPosition(Unit unit)
    {
        unit.Target = FindHeldTarget(unit);
        if (unit.Target == null)
        {
            unit.View?.Play("Stand");
            return;
        }

        FaceTarget(unit, unit.Target);
        if (unit.DistanceTo(unit.Target) > unit.AttackReach + unit.Target.Radius) return;
        BeginAttack(unit, unit.Target);
    }

    private void TickPatrolEndpoint(Unit unit)
    {
        Vector3 from = unit.PatrolFrom;
        unit.PatrolFrom = unit.PatrolTo;
        unit.PatrolTo = from;
        unit.MoveTarget = unit.PatrolTo;
        unit.Path = null;
        unit.PathIndex = 0;
        LogOrder(unit, $"巡逻折返 → ({unit.PatrolTo.X:0},{unit.PatrolTo.Z:0})");
    }

    private Unit FindHeldTarget(Unit unit)
    {
        if (unit.Type == null || !unit.Type.CanAttack || !unit.State.CanAttack) return null;

        float reach = unit.Type.AcquireRange + unit.Radius;
        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit other in Units)
        {
            if (!other.Alive || other == unit) continue;
            if (other.Player == 15) continue;
            if (IsAllied(unit.Player, other.Player)) continue;
            if (!CanStrike(unit, other)) continue;
            if (other.State.Invisible && !unit.State.Has(StateMod.Detect)) continue;

            float distance = unit.DistanceTo(other);
            if (distance > reach + other.Radius) continue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = other;
            }
        }
        return best;
    }

    private static string DescribeOrder(UnitOrder order)
    {
        return order.Kind switch
        {
            UnitOrderKind.Attack => $"攻击 {order.Target?.Type?.Name ?? "?"}",
            UnitOrderKind.Patrol => $"巡逻 ({order.Point.X:0},{order.Point.Z:0})",
            _ => $"移动 ({order.Point.X:0},{order.Point.Z:0})",
        };
    }

    private static void LogOrder(Unit unit, string text)
    {
        if (unit.OrderLogBudget <= 0) return;
        unit.OrderLogBudget--;
        GD.Print($"[ORDER] {unit.Type?.Name}(P{unit.Player}) {text}");
    }
}