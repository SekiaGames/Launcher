using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

public sealed partial class World
{
    public void SetBuild(Unit worker, Unit site, bool queue = false)
    {
        if (worker == null || site == null) return;
        if (queue && worker.BuildSite != null && !ReferenceEquals(worker.BuildSite, site))
        {
            if (!worker.BuildQueue.Contains(site)) worker.BuildQueue.Add(site);
            return;
        }
        ApplyBuild(worker, site);
        worker.BuildQueue.Clear();
    }

    private void ApplyBuild(Unit worker, Unit site)
    {
        InterruptChannel(worker);
        worker.BuildSite = site;
        worker.BuildingNow = false;
        worker.RepairTarget = null;
        worker.Repairing = false;
        worker.Tree = null;
        worker.GoldMine = null;
        worker.ReturnHall = null;
        worker.FollowTarget = null;
        worker.Target = null;
        worker.MoveTarget = null;
        worker.Path = null;
        worker.PathIndex = 0;
        worker.WorkTimer = 0f;
        ClearQueuedOrders(worker);
    }

    public void SetRepair(Unit worker, Unit target)
    {
        if (worker == null || target == null) return;
        InterruptChannel(worker);
        worker.RepairTarget = target;
        worker.Repairing = false;
        worker.RepairGoldDebt = 0f;
        worker.BuildSite = null;
        worker.BuildingNow = false;
        worker.Tree = null;
        worker.GoldMine = null;
        worker.ReturnHall = null;
        worker.FollowTarget = null;
        worker.Target = null;
        worker.MoveTarget = null;
        worker.Path = null;
        worker.PathIndex = 0;
        worker.WorkTimer = 0f;
        ClearQueuedOrders(worker);
        worker.BuildQueue.Clear();
    }

    /// <summary>携带资源时右键点主城：把这一趟送到指定建筑，送完继续回原来的矿/树。</summary>
    public bool SetReturnResources(Unit worker, Unit hall)
    {
        if (worker == null || hall == null || !worker.Alive) return false;
        if (worker.Carry <= 0 && worker.CarryLumber <= 0) return false;

        worker.FollowTarget = null;
        worker.ReturnHall = hall;
        worker.HomeHall = hall;
        worker.BuildSite = null;
        worker.BuildingNow = false;
        worker.RepairTarget = null;
        worker.Repairing = false;
        if (worker.GoldMine == null && worker.Tree == null)
        {
            worker.Target = null;
            worker.MoveTarget = hall.Position;
            worker.Path = null;
            worker.PathIndex = 0;
        }
        return true;
    }

    public void SetChop(Unit worker, TreeState tree)
    {
        if (worker == null || tree == null || !worker.Type.CanGatherLumber) return;
        InterruptChannel(worker);
        worker.Tree = tree;
        worker.GoldMine = null;
        worker.RepairTarget = null;
        worker.Repairing = false;
        worker.ReturnHall = null;
        worker.FollowTarget = null;
        worker.Target = null;
        worker.MoveTarget = null;
        worker.HomeHall = null;
        worker.WorkTimer = 0f;
        worker.Path = null;
        worker.PathIndex = 0;
        ClearQueuedOrders(worker);
    }

    public void SetGather(Unit worker, Unit mine)
    {
        if (worker == null || mine == null || !worker.Type.CanGatherGold) return;
        InterruptChannel(worker);
        worker.GoldMine = mine;
        worker.Tree = null;
        worker.RepairTarget = null;
        worker.Repairing = false;
        worker.ReturnHall = null;
        worker.FollowTarget = null;
        worker.HomeHall = null;
        worker.Carry = 0;
        worker.WorkTimer = 0f;
        worker.Target = null;
        worker.MoveTarget = null;
        worker.Path = null;
        worker.PathIndex = 0;
        ClearQueuedOrders(worker);
    }

    public void ClearOrders(Unit unit)
    {
        // 下令（移动/停止/攻击）都会打断引导，客户端同理
        InterruptChannel(unit);
        unit.Target = null;
        unit.MoveTarget = null;
        unit.GoldMine = null;
        unit.Tree = null;
        unit.BuildSite = null;
        unit.BuildingNow = false;
        unit.RepairTarget = null;
        unit.Repairing = false;
        unit.ReturnHall = null;
        unit.FollowTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        ClearQueuedOrders(unit);
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
