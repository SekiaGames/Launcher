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
