using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenWC3.Game;

internal sealed class PlayerCommandProcessor
{
    private readonly World _world;
    private readonly PlayerSelectionController _selection;
    private readonly Func<bool> _formationMovementEnabled;
    private readonly Action<Vec2> _commandIssued;

    internal Action<bool> OrderAck { get; set; }

    internal PlayerCommandProcessor(World world, PlayerSelectionController selection,
        Func<bool> formationMovementEnabled, Action<Vec2> commandIssued)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _formationMovementEnabled = formationMovementEnabled
            ?? throw new ArgumentNullException(nameof(formationMovementEnabled));
        _commandIssued = commandIssued;
    }

    internal void Execute(PlayerCommand command)
    {
        switch (command)
        {
            case SelectUnitCommand select:
                if (select.Additive) _selection.SelectClicked(select.Unit, true, true);
                else _selection.SelectSingle(select.Unit);
                break;
            case DeselectCommand deselect:
                _selection.Remove(deselect.Unit);
                break;
            case MoveCommand move:
                Move(move.Destination, move.Queued);
                break;
            case AttackCommand attack:
                Attack(attack.Target, attack.Queued);
                break;
            case AttackMoveCommand attackMove:
                AttackMove(attackMove.Destination, attackMove.Queued);
                break;
            case PatrolCommand patrol:
                Patrol(patrol.Destination, patrol.Queued);
                break;
            case StopCommand:
                Stop();
                break;
            case HoldPositionCommand:
                HoldPosition();
                break;
        }
    }

    internal void Stop()
    {
        foreach (Unit unit in _selection.LiveControlled())
            _world.StopUnit(unit);
    }

    internal void HoldPosition()
    {
        foreach (Unit unit in _selection.LiveControlled())
            _world.HoldPosition(unit);
    }

    internal void Move(Vec2 destination, bool queued, bool silent = false,
        Unit[] explicitMovers = null)
    {
        List<Unit> movers = (explicitMovers ?? _selection.MovableControlled()).ToList();
        if (movers.Count == 0) return;

        Vector3 ground = ToWorld(destination);
        Vector3 centroid = Centroid(movers);
        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(movers, ground, ground - centroid);
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.Move(spot), queued);
        }
        ApplyGroupSpeedCap(movers, slots, ground);
        if (!queued) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        if (silent) return;
        _commandIssued?.Invoke(destination);
        OrderAck?.Invoke(false);
    }

    internal void AttackMove(Vec2 destination, bool queued)
    {
        List<Unit> movers = _selection.LiveControlled()
            .Where(unit => unit.CanMove && unit.CanAttack).ToList();
        if (movers.Count == 0) return;

        Vector3 ground = ToWorld(destination);
        Vector3 centroid = Centroid(movers);
        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(movers, ground, ground - centroid);
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.AttackMove(spot), queued);
        }
        ApplyGroupSpeedCap(movers, slots, ground);
        if (!queued) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        _commandIssued?.Invoke(destination);
        OrderAck?.Invoke(true);
    }

    internal void Patrol(Vec2 destination, bool queued)
    {
        List<Unit> movers = _selection.MovableControlled().ToList();
        if (movers.Count == 0) return;

        Vector3 ground = ToWorld(destination);
        Vector3 centroid = Centroid(movers);
        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(movers, ground, ground - centroid);
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.Patrol(spot), queued);
        }
        ApplyGroupSpeedCap(movers, slots, ground);
        if (!queued) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        _commandIssued?.Invoke(destination);
        OrderAck?.Invoke(false);
    }

    internal int Attack(Unit target, bool queued)
    {
        if (target == null) return 0;
        List<Unit> attackers = _selection.LiveControlled()
            .Where(unit => unit.CanAttack && !ReferenceEquals(unit, target)).ToList();
        if (attackers.Count == 0) return 0;
        foreach (Unit attacker in attackers)
        {
            _world.IssueOrder(attacker, UnitOrder.Attack(target), queued);
            attacker.GroupSpeedCap = 0f;
        }
        _commandIssued?.Invoke(new Vec2(target.Position.X, target.Position.Z));
        OrderAck?.Invoke(true);
        return attackers.Count;
    }

    internal void NotifyExternalOrder(Vec2 point, bool attack)
    {
        _commandIssued?.Invoke(point);
        OrderAck?.Invoke(attack);
    }

    private static Vector3 ToWorld(Vec2 point) => new(point.X, 0f, point.Y);

    private static Vector3 Centroid(IReadOnlyList<Unit> units)
    {
        Vector3 sum = Vector3.Zero;
        foreach (Unit unit in units) sum += unit.Position;
        return sum / Mathf.Max(1, units.Count);
    }

    private static void ApplyGroupSpeedCap(List<Unit> movers, Dictionary<Unit, Vector3> slots,
        Vector3 fallback)
    {
        foreach (Unit unit in movers)
        {
            unit.GroupSpeedCap = 0f;
            unit.GroupMoveGoal = default;
        }
        if (movers.Count <= 1) return;

        float cap = float.MaxValue;
        foreach (Unit unit in movers) cap = Mathf.Min(cap, World.SpeedOf(unit));
        foreach (Unit unit in movers)
        {
            unit.GroupSpeedCap = cap;
            unit.GroupMoveGoal = slots.TryGetValue(unit, out Vector3 spot) ? spot : fallback;
        }
    }
}