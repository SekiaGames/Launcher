using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

public sealed partial class World
{
    private bool Advance(Unit unit, Vector3 goal, float stopDistance)
    {
        Vector2 from = new(unit.Position.X, unit.Position.Z);
        Vector2 to = new(goal.X, goal.Z);
        if (from.DistanceTo(to) <= stopDistance) return true;
        if (!unit.State.CanMove) return false;

        EnsurePath(unit, goal, stopDistance);
        if (unit.PathFailed) return false;
        Vector3 waypoint = CurrentWaypoint(unit, goal);
        MoveToward(unit, waypoint);
        return false;
    }

    private void EnsurePath(Unit unit, Vector3 goal, float stopDistance)
    {
        unit.RepathTimer -= Tick;
        bool exhausted = unit.Path == null || unit.PathIndex >= unit.Path.Count;
        if (exhausted && unit.RepathTimer > 0f) return;

        bool chasing = unit.Target != null;
        if (!exhausted && !chasing) return;
        if (!exhausted && unit.RepathTimer > 0f) return;

        unit.RepathTimer = 0.8f;
        if (_pathGrid == null)
        {
            unit.Path = null;
            unit.PathIndex = 0;
            return;
        }
        Vector3 pathGoal = goal;
        PathLayer layer = unit.Type?.PathingLayer ?? PathLayer.Ground;
        List<Vector3> path = _pathGrid.FindPath(unit.Position, pathGoal, unit.Radius, layer);
        if (path == null || path.Count == 0)
        {
            unit.Path = null;
            unit.PathIndex = 0;
            unit.PathFailed = true;
            return;
        }

        unit.PathFailed = false;
        unit.Path = path;
        unit.PathIndex = 0;
        unit.GroupMoveId = 0;
        if (_pathLogBudget > 0 && path.Count > 1)
        {
            _pathLogBudget--;
            GD.Print($"[PATH] {unit.Type.Id} 航点 {path.Count} 从 {unit.Position.X:0},{unit.Position.Z:0} 到 {goal.X:0},{goal.Z:0}");
        }
    }

    private static int _pathLogBudget = 4;

    private Vector3 CurrentWaypoint(Unit unit, Vector3 goal)
    {
        if (unit.Path == null || unit.PathIndex >= unit.Path.Count) return goal;

        Vector3 waypoint = unit.Path[unit.PathIndex];
        Vector2 flat = new(unit.Position.X - waypoint.X, unit.Position.Z - waypoint.Z);
        if (flat.Length() < 24f)
        {
            int floor = GroupWaypointFloor(unit);
            if (floor < 0 || unit.PathIndex <= floor) unit.PathIndex++;
            return unit.PathIndex < unit.Path.Count ? unit.Path[unit.PathIndex] : goal;
        }
        return waypoint;
    }

    private void MoveToward(Unit unit, Vector3 goal)
    {
        Vector2 from = new(unit.Position.X, unit.Position.Z);
        Vector2 to = new(goal.X, goal.Z);
        Vector2 delta = to - from;
        float distance = delta.Length();
        if (distance < 0.001f) return;

        Vector2 direction = delta / distance;
        float step = Mathf.Min(SpeedOf(unit) * Tick, distance);
        Vector2 next = from + direction * step;

        float groundY = _terrain != null ? SurfaceY(unit.Type, next.X, next.Y) : unit.Position.Y;
        unit.Position = new Vector3(next.X, groundY, next.Y);
        unit.TargetFacing = Mathf.Pi + Mathf.Atan2(-direction.X, direction.Y);

        if (unit.View != null)
        {
            unit.View.Root.Position = unit.Position;
            ApplyFacing(unit);
            unit.View.Play("Walk");
        }
    }

    public float SurfaceY(UnitType type, float worldX, float worldZ)
    {
        if (_terrain == null || type == null) return 0f;
        PathLayer layer = type.PathingLayer;
        if (layer == PathLayer.Water)
        {
            return MapWorldBuilder.WaterHeightAt(_terrain, worldX, worldZ);
        }
        if (layer == PathLayer.Amphibious && _pathGrid != null
            && !_pathGrid.IsBlockedWorld(worldX, worldZ, PathLayer.Water))
        {
            return MapWorldBuilder.WaterHeightAt(_terrain, worldX, worldZ);
        }
        return MapWorldBuilder.HeightAt(_terrain, worldX, worldZ);
    }

    /// <summary>瞬移/落点定位：改模拟坐标的同时把节点也挪过去（对标客户端 World.SetUnitPosition）。</summary>
    public void MoveUnitTo(Unit unit, Vector3 position)
    {
        if (unit == null) return;

        unit.Position = position;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.PathFailed = false;
        unit.GroupMoveId = 0;
        unit.RepathTimer = 0f;
        if (unit.View != null) unit.View.Root.Position = position;
    }

    /// <summary>按 UnitData.turnRate 折算的角速度转向目标朝向（建筑不动）。</summary>
    private static void ApplyFacing(Unit unit)
    {
        if (unit.IsBuilding || unit.View == null) return;

        float diff = Mathf.Wrap(unit.TargetFacing - unit.Facing, -Mathf.Pi, Mathf.Pi);
        float maxStep = Mathf.Max(1.5f, unit.Type.TurnSpeed) * Tick;
        if (Mathf.Abs(diff) <= maxStep) unit.Facing = unit.TargetFacing;
        else unit.Facing += Mathf.Sign(diff) * maxStep;
        unit.View.Root.Rotation = new Vector3(0f, unit.Facing, 0f);
    }

    private static void FaceTarget(Unit unit, Unit target)
    {
        Vector2 delta = new(target.Position.X - unit.Position.X, target.Position.Z - unit.Position.Z);
        unit.TargetFacing = Mathf.Pi + Mathf.Atan2(-delta.X, delta.Y);
        ApplyFacing(unit);
    }
}
