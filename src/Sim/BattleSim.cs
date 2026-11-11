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
    public float ReturnDelay;
    public float LeashRange = 1000f;

    public bool IsUnderConstruction;
    public float BuildRemaining;
    public float BuildTotal;
    public UnitState BuildSite;
    public bool BuildingNow;
    public bool NoBuilderLogged;
    public float NoBuilderTimer;

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
    public event Action<UnitState, UnitState> MissileFired;

    /// <summary>同盟判定由对局层注入（w3i 队伍），模拟层不假设玩家号顺序。</summary>
    public Func<int, int, bool> Allied;

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

        if (unit.BuildSite != null)
        {
            StepBuild(unit);
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

    private void TickConstruction(UnitState site)
    {
        int builders = 0;
        int assigned = 0;
        foreach (UnitState unit in Units)
        {
            if (!unit.Alive || unit.BuildSite != site) continue;
            assigned++;
            if (unit.BuildingNow) builders++;
        }

        if (assigned == 0)
        {
            site.NoBuilderTimer += Tick;
            if (!site.NoBuilderLogged && site.NoBuilderTimer >= 3f)
            {
                site.NoBuilderLogged = true;
                GD.Print($"[BUILD] {site.Type.Name} 没有指派工人，施工暂停");
            }
        }
        else
        {
            site.NoBuilderTimer = 0f;
            site.NoBuilderLogged = false;
        }

        if (builders > 0)
        {
            float rate = Mathf.Min(builders, 3);
            site.BuildRemaining -= Tick * rate;
            if (_buildLogBudget > 0 && site.BuildRemaining > 0f && builders > 1)
            {
                _buildLogBudget--;
                GD.Print($"[BUILD] {site.Type.Name} 合建 {builders} 人，速度 x{rate:0}");
            }
        }
        float progress = site.BuildTotal <= 0f ? 1f : 1f - site.BuildRemaining / site.BuildTotal;
        site.Hp = Mathf.Max(1f, site.MaxHp * Mathf.Clamp(progress, 0f, 1f));

        if (site.BuildRemaining > 0f) return;

        site.IsUnderConstruction = false;
        site.BuildRemaining = 0f;
        site.Hp = site.MaxHp;
        foreach (UnitState unit in Units)
        {
            if (ReferenceEquals(unit.BuildSite, site))
            {
                unit.BuildSite = null;
                unit.BuildingNow = false;
                unit.Path = null;
            }
        }
        site.View?.Play("Stand");
        site.View?.SetHp(1f, true);
    }

    private static int _buildLogBudget = 6;

    public void SetBuild(UnitState worker, UnitState site)
    {
        if (worker == null || site == null) return;
        worker.BuildSite = site;
        worker.BuildingNow = false;
        worker.Tree = null;
        worker.GoldMine = null;
        worker.Target = null;
        worker.MoveTarget = null;
        worker.Path = null;
        worker.PathIndex = 0;
        worker.WorkTimer = 0f;
    }

    private void StepBuild(UnitState worker)
    {
        UnitState site = worker.BuildSite;
        if (site == null || !site.Alive || !site.IsUnderConstruction)
        {
            worker.BuildSite = null;
            worker.BuildingNow = false;
            worker.View?.Play("Stand");
            return;
        }

        float reach = site.Type.FootprintRadius + 80f;
        if (Advance(worker, site.Position, reach))
        {
            worker.BuildingNow = true;
            worker.View?.Play("Stand Work");
            FaceTarget(worker, site);
        }
        else
        {
            worker.BuildingNow = false;
        }
    }

    public void SetChop(UnitState worker, TreeState tree)
    {
        if (worker == null || tree == null || !worker.Type.CanGatherLumber) return;
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
                unit.ReturnDelay = 0f;
                unit.Path = null;
                unit.View?.Play("Stand");
            }
            return;
        }

        if (unit.ReturnDelay > 0f)
        {
            unit.ReturnDelay -= Tick;
            unit.Target = null;
            unit.MoveTarget = null;
            unit.Path = null;
            unit.View?.Play("Stand");
            if (unit.ReturnDelay <= 0f) unit.Returning = true;
            return;
        }

        if (unit.Target != null)
        {
            if (!unit.Target.Alive)
            {
                unit.Target = null;
                unit.MoveTarget = null;
                unit.Path = null;
                return;
            }
            if (GuardDistanceExceeded(unit))
            {
                GiveUpChase(unit);
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
            EngageTarget(unit, victim);
            if (unit.Player == 12 && _creepLogBudget > 0)
            {
                _creepLogBudget--;
                GD.Print($"[CREEP] 野怪 {unit.Type.Id} 警戒锁定 {victim.Type.Id} 距离 {unit.DistanceTo(victim):0} 距营地 {DistanceFromGuard(unit):0}");
            }
            return;
        }

        unit.View?.Play("Stand");
    }

    private static int _creepLogBudget = 8;

    private UnitState FindAcquireTarget(UnitState unit)
    {
        UnitState best = null;
        float bestDistance = Mathf.Min(unit.Type.AcquireRange, MiscRules.GuardDistance);
        foreach (UnitState other in Units)
        {
            if (!other.Alive) continue;
            if (other.Player == 15) continue;
            if (IsAllied(unit.Player, other.Player)) continue;
            float distance = unit.DistanceTo(other);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = other;
            }
        }
        return best;
    }

    private bool IsAllied(int a, int b)
    {
        if (a == b) return true;
        return Allied != null && Allied(a, b);
    }

    private static float DistanceFromGuard(UnitState unit)
    {
        return new Vector2(unit.Position.X - unit.Home.X, unit.Position.Z - unit.Home.Z).Length();
    }

    private static bool GuardDistanceExceeded(UnitState unit)
    {
        if (unit.Player != 12) return DistanceFromGuard(unit) > unit.LeashRange;
        return DistanceFromGuard(unit) >= MiscRules.MaxGuardDistance;
    }

    private void GiveUpChase(UnitState unit)
    {
        unit.Target = null;
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.ReturnDelay = MiscRules.GuardReturnTime;
        unit.Returning = false;
        unit.View?.Play("Stand");
        if (_creepLogBudget > 0)
        {
            _creepLogBudget--;
            GD.Print($"[CREEP] 野怪 {unit.Type.Id} 超出最大追击 {DistanceFromGuard(unit):0} >= {MiscRules.MaxGuardDistance:0}，原地等待 {MiscRules.GuardReturnTime:0}s 后回营");
        }
    }

    private void EngageTarget(UnitState unit, UnitState victim)
    {
        if (victim == null || !victim.Alive) return;
        unit.Target = victim;
        unit.Returning = false;
        unit.ReturnDelay = 0f;
        CallForHelp(unit, victim);
    }

    private void CallForHelp(UnitState caller, UnitState victim)
    {
        if (caller.Player != 12) return;
        foreach (UnitState other in Units)
        {
            if (!other.Alive || other == caller || other.Player != 12) continue;
            if (other.Target != null || other.Returning || other.ReturnDelay > 0f) continue;
            if (caller.DistanceTo(other) > MiscRules.CreepCallForHelp) continue;
            other.Target = victim;
        }
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
                if (unit.Type.EntersMine)
                {
                    GoldDeposited?.Invoke(unit.Player, take);
                }
                else
                {
                    unit.Carry = take;
                    unit.HomeHall = NearestHall(unit);
                }
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
        if (unit.Type.MissileScene != null) MissileFired?.Invoke(unit, target);
        target.Hp -= Mathf.Max(1f, damage);
        target.View?.SetHp(target.Hp / Mathf.Max(1f, target.MaxHp), target.Alive);

        if (target.AutoAcquire && target.Alive && target.Target == null)
        {
            EngageTarget(target, unit);
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
        if (worker == null || mine == null || !worker.Type.CanGatherGold) return;
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
        unit.BuildSite = null;
        unit.BuildingNow = false;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.View?.Play("Stand");
    }
}