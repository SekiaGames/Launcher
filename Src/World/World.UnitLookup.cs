using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    private TreeState NearestTree(Unit worker, float maxDistance)
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

    /// <summary>吃树（Eat Tree）用：取范围内最近的一棵活树，不改变任何状态。</summary>
    public TreeState TakeNearestTree(Vector3 from, float maxDistance)
    {
        TreeState best = null;
        float bestDistance = maxDistance;
        foreach (TreeState tree in _trees)
        {
            if (tree == null || !tree.Alive) continue;
            float dx = from.X - tree.Position.X;
            float dz = from.Z - tree.Position.Z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = tree;
        }
        return best;
    }

    /// <summary>把树吃掉/砍倒：隐藏模型并解开它占的那圈阻挡（与砍倒同一套处理）。</summary>
    public void ConsumeTree(TreeState tree)
    {
        if (tree == null || !tree.Alive) return;
        tree.Alive = false;
        if (tree.Node != null) tree.Node.Visible = false;
        _pathGrid?.ClearArea(tree.Position, 24f);
    }

    /// <summary>
    /// 把一个单位真正从这个世界上拿掉（骑乘合成、分离这类「原地换人」）。
    /// 场景层监听 <see cref="UnitRemoved"/> 释放模型节点。
    /// </summary>
    public void RemoveUnit(Unit unit)
    {
        if (unit == null) return;

        unit.Alive = false;
        unit.IsCorpse = false;
        unit.Target = null;
        unit.MoveTarget = null;
        ReleaseCargoOnDeath(unit);
        Units.Remove(unit);
        UnitRemoved?.Invoke(unit);
    }

    /// <summary>
    /// 造一个属于某单位的玩家、但不带召唤关系的单位（骑乘合成、分离用）。
    /// 与 <see cref="Summon"/> 的区别：不会随召唤者死亡消失，也不会自己到期。
    /// </summary>
    public Unit CreateFor(Unit owner, string unitId, Vector3 position)
    {
        if (owner == null || SummonHandler == null || string.IsNullOrEmpty(unitId)) return null;

        Unit created = SummonHandler(owner, unitId, position);
        if (created == null) return null;
        created.Summoner = null;
        created.SummonRemaining = 0f;
        if (!Units.Contains(created)) Units.Add(created);
        return created;
    }

    /// <summary>古树拔根：放开自己占的格子（半径取拔根前的建筑足迹）。</summary>
    public void ReleaseFootprint(Unit unit, float radius)
    {
        if (unit == null) return;
        _pathGrid?.RemoveBlocker(unit.Position, radius);
    }

    /// <summary>古树扎根：重新占住脚下的格子。</summary>
    public void OccupyFootprint(Unit unit)
    {
        if (unit == null) return;
        _pathGrid?.AddBlocker(unit.Position, unit.Radius);
    }

    private Unit NearestHall(Unit worker)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit unit in Units)
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
}
