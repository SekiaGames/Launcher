using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

public sealed partial class World
{
    private static readonly string[] CargoWorkVariants =
    {
        "Stand Work First", "Stand Work Second", "Stand Work Third",
        "Stand Work Fourth", "Stand Work Fifth",
    };
    private const int CargoWorkVariantLimit = 5;

    private bool StepCargoWork(Unit mine)
    {
        if (mine.Type?.Id == null || !mine.Type.Id.Equals("egol", StringComparison.OrdinalIgnoreCase)) return false;
        int occupants = EntangledMineOccupants(mine);
        if (occupants <= 0) return false;
        mine.View?.Play(CargoWorkVariants[Math.Min(occupants, CargoWorkVariantLimit) - 1]);
        return true;
    }

    public int EntangledMineOccupants(Unit mine)
    {
        int count = 0;
        for (int index = 0; index < Units.Count; index++)
        {
            Unit worker = Units[index];
            if (worker == null || !worker.Alive || worker.IsBuilding) continue;
            if (!ReferenceEquals(worker.GoldMine, mine)) continue;
            count++;
        }
        return count;
    }

    /// <summary>手上工地完工后接上 Shift 连造排队的那一批。</summary>
    public void AdvanceBuildQueue(Unit worker)
    {
        if (worker == null || worker.BuildSite != null) return;
        while (worker.BuildQueue.Count > 0)
        {
            Unit next = worker.BuildQueue[0];
            worker.BuildQueue.RemoveAt(0);
            if (next == null || !next.Alive || !next.IsUnderConstruction) continue;
            ApplyBuild(worker, next);
            return;
        }
    }

    /// <summary>右键点主城交付：携带的资源送到指定建筑，这一趟送完清掉标记，接着回矿/树。</summary>
    private void StepReturnResources(Unit unit)
    {
        Unit hall = unit.ReturnHall;
        if (hall == null || !hall.Alive || (unit.Carry <= 0 && unit.CarryLumber <= 0))
        {
            unit.ReturnHall = null;
            return;
        }

        if (unit.Carry > 0 && unit.GoldMine != null) return;
        if (unit.CarryLumber > 0 && unit.Tree != null) return;

        float reach = hall.Type != null
            ? hall.Type.FootprintRadius + Mathf.Max(8f, unit.Radius) + 8f
            : 160f;
        if (!Advance(unit, hall.Position, reach)) return;

        if (unit.Carry > 0)
        {
            GoldDeposited?.Invoke(unit.Player, unit.Carry);
            unit.Carry = 0;
        }
        if (unit.CarryLumber > 0)
        {
            LumberDeposited?.Invoke(unit.Player, unit.CarryLumber);
            unit.CarryLumber = 0;
        }
        unit.WorkTimer = 0f;
        unit.ReturnHall = null;
        unit.HomeHall = hall;
    }

    private void StepChop(Unit unit)
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
            unit.WorkTimer += FrameSeconds;
            if (unit.WorkTimer >= WorkSeconds)
            {
                unit.WorkTimer = 0f;
                unit.CarryLumber = 10 + Mathf.RoundToInt(unit.TechLumberBonus);
                tree.Chop(10f);
                TreeChopped?.Invoke(unit);
                if (!tree.Alive) _pathGrid?.ClearArea(tree.Position, 24f);
                unit.HomeHall = NearestHall(unit);
            }
        }
    }

    private void TickConstruction(Unit site)
    {
        int builders = 0;
        int assigned = 0;
        foreach (Unit unit in Units)
        {
            if (!unit.Alive || unit.BuildSite != site) continue;
            assigned++;
            if (unit.BuildingNow) builders++;
        }

        if (assigned == 0)
        {
            site.NoBuilderTimer += FrameSeconds;
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
            site.BuildRemaining -= FrameSeconds * rate;
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
        foreach (Unit unit in Units)
        {
            if (ReferenceEquals(unit.BuildSite, site))
            {
                unit.BuildSite = null;
                unit.BuildingNow = false;
                unit.Path = null;
            }
        }
        site.View?.Play("Stand");
        ConstructionFinished?.Invoke(site);
    }

    private static int _buildLogBudget = 6;

    private void StepBuild(Unit worker)
    {
        Unit site = worker.BuildSite;
        if (site == null || !site.Alive || !site.IsUnderConstruction)
        {
            worker.BuildSite = null;
            worker.BuildingNow = false;
            worker.View?.Play("Stand");
            return;
        }

        float reach = site.Type.FootprintRadius + Mathf.Max(8f, worker.Radius) + 8f;
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

    private void StepGather(Unit unit)
    {
        Unit mine = unit.GoldMine;
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
            unit.WorkTimer += FrameSeconds;
            if (unit.WorkTimer >= WorkSeconds)
            {
                unit.WorkTimer = 0f;
                int take = Mathf.Min(10, mine.Gold);
                mine.Gold -= take;
                if (unit.Type.EntersMine)
                {
                    unit.InMineRemaining = 0.6f;
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
}
