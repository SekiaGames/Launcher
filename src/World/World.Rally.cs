using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    public void SetBuild(Unit worker, Unit site)
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

    public void SetChop(Unit worker, TreeState tree)
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

    public void SetGather(Unit worker, Unit mine)
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

    public void ClearOrders(Unit unit)
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

    /// <summary>建筑设集结点：点 / 单位 / 树 三选一，互相覆盖。</summary>
    public void SetRally(Unit building, Vector3 point)
    {
        if (building == null || !building.IsBuilding) return;
        building.RallyPoint = point;
        building.RallyTarget = null;
        building.RallyTree = null;
    }

    public void SetRally(Unit building, Unit target)
    {
        if (building == null || !building.IsBuilding || target == null) return;
        building.RallyPoint = null;
        building.RallyTarget = target;
        building.RallyTree = null;
    }

    public void SetRally(Unit building, TreeState tree)
    {
        if (building == null || !building.IsBuilding || tree == null) return;
        building.RallyPoint = null;
        building.RallyTarget = null;
        building.RallyTree = tree;
    }

    /// <summary>新单位出生：按产出建筑的集结点决定去哪（采集 / 伐木 / 攻击 / 走位）。</summary>
    public void SendToRally(Unit fresh, Unit building)
    {
        if (fresh == null || building == null) return;

        Unit target = building.RallyTarget;
        if (target != null && target.Alive)
        {
            if (target.Gold > 0 && fresh.Type.CanGatherGold)
            {
                SetGather(fresh, target);
                return;
            }
            if (fresh.Type.CanAttack && !IsAllied(fresh.Player, target.Player))
            {
                fresh.GoldMine = null;
                fresh.Tree = null;
                fresh.MoveTarget = null;
                fresh.Path = null;
                fresh.Target = target;
                return;
            }
        }

        TreeState tree = building.RallyTree;
        if (tree != null && tree.Alive && fresh.Type.CanGatherLumber)
        {
            SetChop(fresh, tree);
            return;
        }

        if (building.RallyPoint.HasValue)
        {
            fresh.Target = null;
            fresh.GoldMine = null;
            fresh.Tree = null;
            fresh.Path = null;
            fresh.MoveTarget = building.RallyPoint.Value;
        }
    }
}
