using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed class UnitState
{
    public UnitType Type;
    public int Player;
    public Vector3 Position;
    public float Facing;
    public float Hp;
    public float MaxHp;
    public bool Alive = true;
    public bool IsBuilding;
    public UnitState Target;
    public Vector3? MoveTarget;
    public float AttackCooldown;
    public UnitView View;

    public UnitState GoldMine;
    public UnitState HomeHall;
    public TreeState Tree;
    public int Carry;
    public int CarryLumber;
    public float WorkTimer = 0f;
    public int Gold;

    public Vector3 Home;
    public bool AutoAcquire;
    public bool Returning;
    public float LeashRange = 1000f;

    public bool IsUnderConstruction;
    public float BuildRemaining;
    public float BuildTotal;

    public List<Vector3> Path;
    public int PathIndex;
    public float RepathTimer;

    public float Radius => IsBuilding ? 48f : 28f;
    public bool IsWorker => Type != null && Type.IsWorkerType;

    public float DistanceTo(UnitState other) =>
        new Vector2(Position.X - other.Position.X, Position.Z - other.Position.Z).Length();
}

public sealed class BattleSim
{
    public const float Tick = 0.05f;
    private const float GatherRange = 190f;
    private const float WorkSeconds = 2.2f;

    private readonly W3eTerrain _terrain;
    private readonly PathGrid _pathGrid;
    private readonly List<TreeState> _trees;
    private readonly Random _random = new(20261111);

    public List<UnitState> Units { get; } = new();
    public event Action<UnitState> Died;
    public event Action<int, int> GoldDeposited;
    public event Action<int, int> LumberDeposited;

    private readonly Dictionary<string, int> _research = new(StringComparer.OrdinalIgnoreCase);

    public int ResearchLevel(int player, string upgradeId)
    {
        return _research.TryGetValue(player + "|" + upgradeId, out int level) ? level : 0;
    }

    public void SetResearchLevel(int player, string upgradeId, int level)
    {
        _research[player + "|" + upgradeId] = Mathf.Max(0, level);
    }

    public float DamageBonus(UnitState unit)
    {
        float bonus = 0f;
        foreach (string upgradeId in unit.Type.UpgradesApplied)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;
            bonus += upgrade.DamageAt(ResearchLevel(unit.Player, upgradeId));
        }
        return bonus;
    }

    public float ArmorOf(UnitState unit)
    {
        float armor = unit.Type.Armor;
        foreach (string upgradeId in unit.Type.UpgradesApplied)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;
            armor += upgrade.ArmorAt(ResearchLevel(unit.Player, upgradeId));
        }
        return armor;
    }

    public float HpMultiplier(UnitState unit, int player)
    {
        float multiplier = 1f;
        foreach (string upgradeId in unit.Type.UpgradesApplied)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;
            multiplier += upgrade.HpPercentAt(ResearchLevel(player, upgradeId));
        }
        return multiplier;
    }

    public BattleSim(W3eTerrain terrain, PathGrid pathGrid, List<TreeState> trees = null)
    {
        _terrain = terrain;
        _pathGrid = pathGrid;
        _trees = trees ?? new List<TreeState>();
    }

    public void Advance()
    {
        foreach (UnitState unit in Units)
        {
            if (!unit.Alive) continue;
            Step(unit);
        }
    }

    private void Step(UnitState unit)
    {
        if (unit.IsUnderConstruction)
        {
            TickConstruction(unit);
            return;
        }

        if (unit.Tree != null)
        {
            StepChop(unit);
            return;
        }

        if (unit.GoldMine != null)
        {
            StepGather(unit);
            return;
        }

        if (unit.AutoAcquire)
        {
            StepCreep(unit);
            return;
        }

        if (unit.Target != null && !unit.Target.Alive) unit.Target = null;

        if (unit.Target != null && unit.Type.CanAttack)
        {
            float distance = unit.DistanceTo(unit.Target);
            if (distance <= unit.Type.AttackRange + unit.Target.Radius)
            {
                FaceTarget(unit, unit.Target);
                Attack(unit, unit.Target);
                return;
            }
            if (unit.IsBuilding)
            {
                unit.Target = null;
                return;
            }
            Advance(unit, unit.Target.Position, unit.Type.AttackRange + unit.Target.Radius);
            return;
        }

        if (unit.MoveTarget.HasValue)
        {
            if (unit.IsBuilding)
            {
                unit.MoveTarget = null;
                return;
            }
            if (Advance(unit, unit.MoveTarget.Value, 12f))
            {
                unit.MoveTarget = null;
                unit.View?.Play("Stand");
            }
            return;
        }

        unit.View?.Play("Stand");
    }

    private void StepChop(UnitState unit)
    {
        TreeState tree = unit.Tree;
        if (tree == null || !tree.Alive)
        {
            tree = NearestTree(unit, 900f);
            unit.Tree = tree;
        }

        if (unit.CarryLumber > 0)
        {
            if (unit.HomeHall == null || !unit.HomeHall.Alive)
            {
                unit.HomeHall = NearestHall(unit);
            }
            if (unit.HomeHall == null)
            {
                unit.CarryLumber = 0;
                return;
            }

            if (Advance(unit, unit.HomeHall.Position, GatherRange))
            {
                LumberDeposited?.Invoke(unit.Player, unit.CarryLumber);
                unit.CarryLumber = 0;
                unit.WorkTimer = 0f;
            }
            return;
        }

        if (tree == null)
        {
            unit.View?.Play("Stand");
            return;
        }

        if (Advance(unit, tree.Position, 140f))
        {
            unit.View?.Play("Stand Work");
            unit.WorkTimer += Tick;
            if (unit.WorkTimer >= WorkSeconds)
            {
                unit.WorkTimer = 0f;
                unit.CarryLumber = 10;
                tree.Chop(10f);
                unit.HomeHall = NearestHall(unit);
            }
        }
    }

    private TreeState NearestTree(UnitState worker, float maxDistance)
    {
        TreeState best = null;
        float bestDistance = maxDistance;
        foreach (TreeState tree in _trees)
        {
            if (!tree.Alive) continue;
            float distance = new Vector2(worker.Position.X - tree.Position.X, worker.Position.Z - tree.Position.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tree;
            }
        }
        return best;
    }

    private void TickConstruction(UnitState unit)
    {
        unit.BuildRemaining -= Tick;
        float progress = unit.BuildTotal <= 0f ? 1f : 1f - unit.BuildRemaining / unit.BuildTotal;
        unit.Hp = Mathf.Max(1f, unit.MaxHp * Mathf.Clamp(progress, 0f, 1f));

        if (unit.BuildRemaining > 0f) return;

        unit.IsUnderConstruction = false;
        unit.BuildRemaining = 0f;
        unit.Hp = unit.MaxHp;
        unit.View?.Play("Stand");
        unit.View?.SetHp(1f, true);
    }

    public void SetChop(UnitState worker, TreeState tree)
    {
        if (worker == null || tree == null) return;
        worker.Tree = tree;
        worker.GoldMine = null;
        worker.Target = null;
        worker.MoveTarget = null;
        worker.HomeHall = null;
        worker.WorkTimer = 0f;
        worker.Path = null;
        worker.PathIndex = 0;
    }

    private void StepCreep(UnitState unit)
    {
        if (unit.Returning)
        {
            if (Advance(unit, unit.Home, 60f))
            {
                unit.Returning = false;
                unit.View?.Play("Stand");
            }
            return;
        }

        if (unit.Target != null)
        {
            if (!unit.Target.Alive || LeashExceeded(unit))
            {
                unit.Target = null;
                unit.MoveTarget = null;
                unit.Path = null;
                if (LeashExceeded(unit)) unit.Returning = true;
                return;
            }

            float distance = unit.DistanceTo(unit.Target);
            if (distance <= unit.Type.AttackRange + unit.Target.Radius)
            {
                FaceTarget(unit, unit.Target);
                Attack(unit, unit.Target);
            }
            else
            {
                Advance(unit, unit.Target.Position, unit.Type.AttackRange + unit.Target.Radius);
            }
            return;
        }

        UnitState victim = FindAcquireTarget(unit);
        if (victim != null)
        {
            unit.Target = victim;
            if (_creepLogBudget > 0)
            {
                _creepLogBudget--;
                GD.Print($"[AI] 野怪 {unit.Type.Id} p{unit.Player} 锁定 {victim.Type.Id} 距离 {unit.DistanceTo(victim):0} 自身 {unit.Position.X:0},{unit.Position.Z:0}");
            }
            return;
        }

        unit.View?.Play("Stand");
    }

    private static int _creepLogBudget = 8;

    private UnitState FindAcquireTarget(UnitState unit)
    {
        UnitState best = null;
        float bestDistance = Mathf.Min(unit.Type.AcquireRange, 100f);
        foreach (UnitState other in Units)
        {
            if (!other.Alive || other.Player != 0 || other.IsBuilding) continue;
            float distance = unit.DistanceTo(other);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = other;
            }
        }
        return best;
    }

    private bool LeashExceeded(UnitState unit)
    {
        return new Vector2(unit.Position.X - unit.Home.X, unit.Position.Z - unit.Home.Z).Length() > unit.LeashRange;
    }

    private void StepGather(UnitState unit)
    {
        UnitState mine = unit.GoldMine;
        if (mine == null || !mine.Alive || mine.Gold <= 0)
        {
            unit.GoldMine = null;
            unit.HomeHall = null;
            unit.View?.Play("Stand");
            return;
        }

        if (unit.Carry > 0)
        {
            if (unit.HomeHall == null || !unit.HomeHall.Alive)
            {
                unit.HomeHall = NearestHall(unit);
            }
            if (unit.HomeHall == null)
            {
                unit.Carry = 0;
                return;
            }

            if (Advance(unit, unit.HomeHall.Position, GatherRange))
            {
                GoldDeposited?.Invoke(unit.Player, unit.Carry);
                unit.Carry = 0;
                unit.WorkTimer = 0f;
            }
            return;
        }

        if (Advance(unit, mine.Position, GatherRange))
        {
            unit.View?.Play("Stand Work");
            unit.WorkTimer += Tick;
            if (unit.WorkTimer >= WorkSeconds)
            {
                unit.WorkTimer = 0f;
                int take = Mathf.Min(10, mine.Gold);
                mine.Gold -= take;
                unit.Carry = take;
                unit.HomeHall = NearestHall(unit);
            }
        }
    }

    private UnitState NearestHall(UnitState worker)
    {
        UnitState best = null;
        float bestDistance = float.MaxValue;
        foreach (UnitState unit in Units)
        {
            if (!unit.Alive || unit.Player != worker.Player || !unit.IsBuilding) continue;
            if (unit.Type.Id is not ("htow" or "ogre" or "unpl" or "etol")) continue;

            float distance = worker.DistanceTo(unit);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    private bool Advance(UnitState unit, Vector3 goal, float stopDistance)
    {
        Vector2 from = new(unit.Position.X, unit.Position.Z);
        Vector2 to = new(goal.X, goal.Z);
        if (from.DistanceTo(to) <= stopDistance) return true;

        EnsurePath(unit, goal, stopDistance);
        Vector3 waypoint = CurrentWaypoint(unit, goal);
        MoveToward(unit, waypoint);
        return false;
    }

    private void EnsurePath(UnitState unit, Vector3 goal, float stopDistance)
    {
        unit.RepathTimer -= Tick;
        bool exhausted = unit.Path == null || unit.PathIndex >= unit.Path.Count;
        bool chasing = unit.Target != null && unit.RepathTimer <= 0f;
        if (!exhausted && !chasing) return;

        unit.RepathTimer = 0.8f;
        if (_pathGrid == null)
        {
            unit.Path = null;
            unit.PathIndex = 0;
            return;
        }
        Vector3 pathGoal = goal;
        List<Vector3> path = _pathGrid.FindPath(unit.Position, pathGoal, unit.Radius);
        if (path == null || path.Count == 0)
        {
            unit.Path = null;
            unit.PathIndex = 0;
            return;
        }

        unit.Path = path;
        unit.PathIndex = 0;
        if (_pathLogBudget > 0 && path.Count > 1)
        {
            _pathLogBudget--;
            GD.Print($"[PATH] {unit.Type.Id} 航点 {path.Count} 从 {unit.Position.X:0},{unit.Position.Z:0} 到 {goal.X:0},{goal.Z:0}");
        }
    }

    private static int _pathLogBudget = 4;

    private static Vector3 CurrentWaypoint(UnitState unit, Vector3 goal)
    {
        if (unit.Path == null || unit.PathIndex >= unit.Path.Count) return goal;

        Vector3 waypoint = unit.Path[unit.PathIndex];
        Vector2 flat = new(unit.Position.X - waypoint.X, unit.Position.Z - waypoint.Z);
        if (flat.Length() < 24f)
        {
            unit.PathIndex++;
            return unit.PathIndex < unit.Path.Count ? unit.Path[unit.PathIndex] : goal;
        }
        return waypoint;
    }

    private void MoveToward(UnitState unit, Vector3 goal)
    {
        Vector2 from = new(unit.Position.X, unit.Position.Z);
        Vector2 to = new(goal.X, goal.Z);
        Vector2 delta = to - from;
        float distance = delta.Length();
        if (distance < 0.001f) return;

        Vector2 direction = delta / distance;
        float step = Mathf.Min(unit.Type.Speed * Tick, distance);
        Vector2 next = from + direction * step;

        unit.Position = new Vector3(next.X, MapWorldBuilder.HeightAt(_terrain, next.X, next.Y), next.Y);
        unit.Facing = Mathf.Pi + Mathf.Atan2(-direction.X, direction.Y);

        if (unit.View != null)
        {
            unit.View.Root.Position = unit.Position;
            unit.View.Root.Rotation = new Vector3(0f, unit.Facing, 0f);
            unit.View.Play("Walk");
        }
    }

    private static void FaceTarget(UnitState unit, UnitState target)
    {
        Vector2 delta = new(target.Position.X - unit.Position.X, target.Position.Z - unit.Position.Z);
        unit.Facing = Mathf.Pi + Mathf.Atan2(-delta.X, delta.Y);
        if (unit.View != null)
        {
            unit.View.Root.Rotation = new Vector3(0f, unit.Facing, 0f);
        }
    }

    private void Attack(UnitState unit, UnitState target)
    {
        unit.AttackCooldown -= Tick;
        if (unit.AttackCooldown > 0f) return;

        unit.AttackCooldown = Mathf.Max(0.2f, unit.Type.AttackCooldown);
        unit.View?.Play("Attack");

        float damage = Mathf.Lerp(unit.Type.MinDamage, unit.Type.MaxDamage, (float)_random.NextDouble()) + DamageBonus(unit);
        target.Hp -= Mathf.Max(1f, damage);
        target.View?.SetHp(target.Hp / Mathf.Max(1f, target.MaxHp), target.Alive);

        if (target.AutoAcquire && target.Alive && target.Target == null)
        {
            target.Target = unit;
        }

        if (target.Hp <= 0f) Kill(target);
    }

    private void Kill(UnitState unit)
    {
        if (!unit.Alive) return;
        unit.Alive = false;
        unit.Hp = 0f;
        unit.Target = null;
        unit.MoveTarget = null;
        unit.GoldMine = null;
        unit.Tree = null;
        unit.Carry = 0;
        unit.CarryLumber = 0;
        unit.Path = null;
        unit.View?.Play("Death");
        unit.View?.SetHp(0f, false);
        unit.View?.SetSelected(false);
        Died?.Invoke(unit);
    }

    public void SetGather(UnitState worker, UnitState mine)
    {
        if (worker == null || mine == null) return;
        worker.GoldMine = mine;
        worker.Tree = null;
        worker.HomeHall = null;
        worker.Carry = 0;
        worker.WorkTimer = 0f;
        worker.Target = null;
        worker.MoveTarget = null;
        worker.Path = null;
        worker.PathIndex = 0;
    }

    public void ClearOrders(UnitState unit)
    {
        unit.Target = null;
        unit.MoveTarget = null;
        unit.GoldMine = null;
        unit.Tree = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.View?.Play("Stand");
    }
}