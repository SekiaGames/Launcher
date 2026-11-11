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
}
