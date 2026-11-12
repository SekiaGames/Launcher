using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed partial class World
{
    private readonly Dictionary<int, List<Unit>> _moveGroups = new();
    private int _moveGroupSerial;

    public void ApplyGroupMovePath(List<Unit> movers, Vector3 centroid, Vector3 ground,
        IReadOnlyDictionary<Unit, Vector3> slots)
    {
        if (movers == null || movers.Count < 2 || _pathGrid == null) return;

        PathLayer layer = movers[0].Type?.PathingLayer ?? PathLayer.Ground;
        float radius = 0f;
        foreach (Unit unit in movers)
        {
            if (unit == null || !unit.Alive || unit.IsBuilding) return;
            if ((unit.Type?.PathingLayer ?? PathLayer.Ground) != layer) return;
            radius = Mathf.Max(radius, unit.Radius);
        }

        List<Vector3> route = _pathGrid.FindPath(centroid, ground, radius, layer);
        if (route == null || route.Count < 2) return;

        int groupId = ++_moveGroupSerial;
        _moveGroups[groupId] = new List<Unit>(movers);
        foreach (Unit unit in movers)
        {
            Vector3 offset = Vector3.Zero;
            if (slots != null && slots.TryGetValue(unit, out Vector3 slot))
            {
                offset = slot - ground;
                offset.Y = 0f;
            }

            var path = new List<Vector3>(route.Count);
            for (int index = 0; index < route.Count; index++)
            {
                Vector3 point = index == 0 ? route[index] : route[index] + offset;
                if (!_pathGrid.IsAreaFree(point, unit.Radius, layer)) point = route[index];
                path.Add(point);
            }

            unit.Path = path;
            unit.PathIndex = 0;
            unit.PathFailed = false;
            unit.RepathTimer = 0.8f;
            unit.GroupMoveId = groupId;
        }
    }

    private int GroupWaypointFloor(Unit unit)
    {
        if (unit.GroupMoveId == 0 || !_moveGroups.TryGetValue(unit.GroupMoveId, out List<Unit> members))
            return -1;

        int floor = int.MaxValue;
        foreach (Unit member in members)
        {
            if (member == null || !member.Alive || member.PathFailed) continue;
            if (!member.MoveTarget.HasValue) continue;
            floor = Mathf.Min(floor, member.PathIndex);
        }

        if (floor == int.MaxValue)
        {
            _moveGroups.Remove(unit.GroupMoveId);
            return -1;
        }
        return floor;
    }
}