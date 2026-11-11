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
