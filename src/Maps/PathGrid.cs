using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed class PathGrid
{
    private const float CellWorld = 32f;

    private readonly bool[] _blocked;
    private readonly int _width;
    private readonly int _height;
    private readonly float _offsetX;
    private readonly float _offsetY;

    public bool Ready { get; }

    private PathGrid(W3mPathing pathing, W3eTerrain terrain)
    {
        _width = pathing.Width;
        _height = pathing.Height;
        _offsetX = terrain.OffsetX;
        _offsetY = terrain.OffsetY;
        _blocked = new bool[_width * _height];

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _blocked[y * _width + x] = (pathing.At(x, y) & W3mPathing.Unwalkable) != 0;
            }
        }
        Ready = true;
    }

    public static PathGrid Build(W3mPathing pathing, W3eTerrain terrain)
    {
        return pathing == null || terrain == null ? null : new PathGrid(pathing, terrain);
    }

    public bool IsBlockedWorld(float worldX, float worldZ)
    {
        WorldToCell(worldX, worldZ, out int x, out int y);
        return Blocked(x, y);
    }

    public bool IsAreaFree(Vector3 world, float radiusWorld)
    {
        int cells = Mathf.Max(1, Mathf.CeilToInt(radiusWorld / CellWorld));
        WorldToCell(world.X, world.Z, out int cx, out int cy);
        for (int dz = -cells; dz <= cells; dz++)
        {
            for (int dx = -cells; dx <= cells; dx++)
            {
                if (Blocked(cx + dx, cy + dz)) return false;
            }
        }
        return true;
    }

    public void AddBlocker(Vector3 world, float radiusWorld)
    {
        int cells = Mathf.Max(1, Mathf.CeilToInt(radiusWorld / CellWorld));
        WorldToCell(world.X, world.Z, out int cx, out int cy);
        for (int dz = -cells; dz <= cells; dz++)
        {
            for (int dx = -cells; dx <= cells; dx++)
            {
                int x = cx + dx;
                int y = cy + dz;
                if (x < 0 || y < 0 || x >= _width || y >= _height) continue;
                _blocked[y * _width + x] = true;
            }
        }
    }

    private bool Blocked(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height) return true;
        return _blocked[y * _width + x];
    }

    private void WorldToCell(float worldX, float worldZ, out int x, out int y)
    {
        x = Mathf.FloorToInt(-worldX / CellWorld);
        y = Mathf.FloorToInt(worldZ / CellWorld);
    }

    private Vector3 CellToWorld(int x, int y)
    {
        return new Vector3(-(x + 0.5f) * CellWorld, 0f, (y + 0.5f) * CellWorld);
    }

    public List<Vector3> FindPath(Vector3 start, Vector3 end, float unitRadius)
    {
        if (!Ready) return null;

        WorldToCell(start.X, start.Z, out int sx, out int sy);
        WorldToCell(end.X, end.Z, out int ex, out int ey);
        sx = Mathf.Clamp(sx, 0, _width - 1);
        sy = Mathf.Clamp(sy, 0, _height - 1);
        ex = Mathf.Clamp(ex, 0, _width - 1);
        ey = Mathf.Clamp(ey, 0, _height - 1);

        int clearance = unitRadius > 16f ? 1 : 0;
        if (SearchBlocked(ex, ey, clearance))
        {
            if (!TryFindNear(ex, ey, clearance, out ex, out ey)) return null;
        }

        int startIndex = sy * _width + sx;
        int goal = ey * _width + ex;
        if (startIndex == goal)
        {
            return new List<Vector3> { CellToWorld(ex, ey) };
        }

        var open = new PriorityQueue<int, float>();
        var gScore = new Dictionary<int, float>(512);
        var cameFrom = new Dictionary<int, int>(512);
        gScore[startIndex] = 0f;
        open.Enqueue(startIndex, Heuristic(sx, sy, ex, ey));

        int guard = 0;
        while (open.TryDequeue(out int current, out _))
        {
            if (current == goal) return BuildPath(cameFrom, goal, sx, sy, unitRadius);
            if (++guard > 20000) return null;

            int cx = current % _width;
            int cy = current / _width;
            float baseCost = gScore[current];

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;

                    int nx = cx + dx;
                    int ny = cy + dz;
                    if (nx < 0 || ny < 0 || nx >= _width || ny >= _height) continue;
                    if (SearchBlocked(nx, ny, clearance)) continue;
                    if (dx != 0 && dz != 0 && (SearchBlocked(cx + dx, cy, clearance) || SearchBlocked(cx, cy + dz, clearance)))
                    {
                        continue;
                    }

                    int next = ny * _width + nx;
                    float cost = baseCost + (dx != 0 && dz != 0 ? 1.4142f : 1f);
                    if (gScore.TryGetValue(next, out float existing) && existing <= cost) continue;

                    gScore[next] = cost;
                    cameFrom[next] = current;
                    open.Enqueue(next, cost + Heuristic(nx, ny, ex, ey));
                }
            }
        }
        return null;
    }

    private List<Vector3> BuildPath(Dictionary<int, int> cameFrom, int goal, int sx, int sy, float unitRadius)
    {
        var cells = new List<int>();
        int current = goal;
        int start = sy * _width + sx;
        while (current != start && cameFrom.TryGetValue(current, out int previous))
        {
            cells.Add(current);
            current = previous;
            if (cells.Count > 4096) break;
        }
        cells.Reverse();

        var points = new List<Vector3>(cells.Count);
        foreach (int cell in cells)
        {
            Vector3 point = CellToWorld(cell % _width, cell / _width);
            point.Y = 0f;
            points.Add(point);
        }

        var simplified = new List<Vector3>(points.Count);
        Vector3 anchor = CellToWorld(sx, sy);
        anchor.Y = 0f;
        int index = 0;
        while (index < points.Count)
        {
            int farthest = index;
            for (int probe = points.Count - 1; probe >= index; probe--)
            {
                if (Clear(anchor, points[probe], unitRadius))
                {
                    farthest = probe;
                    break;
                }
            }
            simplified.Add(points[farthest]);
            anchor = points[farthest];
            index = farthest + 1;
        }
        return simplified;
    }

    private bool Clear(Vector3 from, Vector3 to, float unitRadius)
    {
        float distance = new Vector2(to.X - from.X, to.Z - from.Z).Length();
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (CellWorld * 0.5f)));
        int clearance = unitRadius > 16f ? 1 : 0;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = from.Lerp(to, (float)i / steps);
            WorldToCell(point.X, point.Z, out int x, out int y);
            if (SearchBlocked(x, y, clearance)) return false;
        }
        return true;
    }

    private bool SearchBlocked(int x, int y, int clearance)
    {
        for (int dz = -clearance; dz <= clearance; dz++)
        {
            for (int dx = -clearance; dx <= clearance; dx++)
            {
                if (Blocked(x + dx, y + dz)) return true;
            }
        }
        return false;
    }

    private bool TryFindNear(int x, int y, int clearance, out int foundX, out int foundY)
    {
        for (int radius = 1; radius <= 8; radius++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dz;
                    if (!SearchBlocked(nx, ny, clearance))
                    {
                        foundX = nx;
                        foundY = ny;
                        return true;
                    }
                }
            }
        }
        foundX = x;
        foundY = y;
        return false;
    }

    private static float Heuristic(int x, int y, int goalX, int goalY)
    {
        int dx = Math.Abs(x - goalX);
        int dy = Math.Abs(y - goalY);
        return Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy);
    }
}