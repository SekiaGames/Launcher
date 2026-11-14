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
        bool formation = _formationMovementEnabled();
        Dictionary<Unit, Vector3> slots = formation
            ? GroupFormation.Layout(movers, ground, ground - centroid) : null;
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots != null && slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.Move(spot), queued);
        }
        if (formation)
        {
            ApplyGroupSpeedCap(movers, slots, ground);
            if (!queued) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        }
        else ClearGroupSpeedCap(movers);
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
        bool formation = _formationMovementEnabled();
        Dictionary<Unit, Vector3> slots = formation
            ? GroupFormation.Layout(movers, ground, ground - centroid) : null;
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots != null && slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.AttackMove(spot), queued);
        }
        if (formation)
        {
            ApplyGroupSpeedCap(movers, slots, ground);
            if (!queued) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        }
        else ClearGroupSpeedCap(movers);
        _commandIssued?.Invoke(destination);
        OrderAck?.Invoke(true);
    }

    internal void Patrol(Vec2 destination, bool queued)
    {
        List<Unit> movers = _selection.MovableControlled().ToList();
        if (movers.Count == 0) return;

        Vector3 ground = ToWorld(destination);
        Vector3 centroid = Centroid(movers);
        bool formation = _formationMovementEnabled();
        Dictionary<Unit, Vector3> slots = formation
            ? GroupFormation.Layout(movers, ground, ground - centroid) : null;
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots != null && slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.Patrol(spot), queued);
        }
        if (formation)
        {
            ApplyGroupSpeedCap(movers, slots, ground);
            if (!queued) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        }
        else ClearGroupSpeedCap(movers);
        _commandIssued?.Invoke(destination);
        OrderAck?.Invoke(false);
    }

    /// <summary>
    /// 攻击者判据（单一出处）：智能右键要知道"谁真的被派去打了"，好让剩下的单位继续执行移动语义。
    /// </summary>
    internal Unit[] EligibleAttackers(Unit target, bool allowFriendly = false)
    {
        if (target == null) return Array.Empty<Unit>();
        return _selection.LiveControlled()
            .Where(unit => unit.CanAttack
                && !ReferenceEquals(unit, target)
                && (allowFriendly || _world.AreEnemies(unit, target)))
            .ToArray();
    }

    internal int Attack(Unit target, bool queued, bool allowFriendly = false)
    {
        Unit[] attackers = EligibleAttackers(target, allowFriendly);
        if (attackers.Length == 0) return 0;
        foreach (Unit attacker in attackers)
        {
            _world.IssueOrder(attacker, UnitOrder.Attack(target), queued);
            attacker.GroupSpeedCap = 0f;
        }
        _commandIssued?.Invoke(new Vec2(target.Position.X, target.Position.Z));
        OrderAck?.Invoke(true);
        return attackers.Length;
    }

    /// <summary>跟随：右键友军单位时走向它并持续跟住，不强制攻击。</summary>
    internal int Follow(Unit target, bool queued)
    {
        if (target == null) return 0;
        Unit[] movers = _selection.MovableControlled()
            .Where(unit => !ReferenceEquals(unit, target)).ToArray();
        if (movers.Length == 0) return 0;
        foreach (Unit unit in movers)
        {
            _world.IssueOrder(unit, UnitOrder.Move(target.Position), queued);
            unit.FollowTarget = target;
            unit.GroupSpeedCap = 0f;
        }
        _commandIssued?.Invoke(new Vec2(target.Position.X, target.Position.Z));
        OrderAck?.Invoke(false);
        return movers.Length;
    }

    /// <summary>载具装卸：选中的单位上别人，或者把点到的人装进自己（客户端 TransportSmart）。</summary>
    internal int TransportSmart(Unit target, bool queued)
    {
        if (target == null || _world == null) return 0;
        int loaded = 0;
        foreach (Unit unit in _selection.LiveControlled())
        {
            if (ReferenceEquals(unit, target)) continue;
            if (target.CargoCapacity > 0 && _world.Cargo.LoadCargo(target, unit))
            {
                loaded++;
                continue;
            }
            if (unit.CargoCapacity > 0 && _world.Cargo.LoadCargo(unit, target)) loaded++;
        }
        if (loaded == 0) return 0;
        _commandIssued?.Invoke(new Vec2(target.Position.X, target.Position.Z));
        OrderAck?.Invoke(false);
        return loaded;
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

    private static void ClearGroupSpeedCap(List<Unit> movers)
    {
        foreach (Unit unit in movers)
        {
            unit.GroupSpeedCap = 0f;
            unit.GroupMoveGoal = default;
        }
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