using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public enum PathLayer
{
    Ground,
    Water,
    Air,
    Amphibious,
}

public sealed class PathGrid
{
    private const float CellWorld = 32f;
    private const int TerrainCornerCells = 4;

    private readonly bool[] _blocked;
    private readonly bool[] _water;
    private readonly bool[] _unflyable;
    private readonly bool[] _cleared;
    private readonly bool[] _dynamic;
    private readonly int _width;
    private readonly int _height;

    public bool Ready { get; }

    private PathGrid(W3mPathing pathing, W3eTerrain terrain)
    {
        _width = pathing.Width;
        _height = pathing.Height;
        _blocked = new bool[_width * _height];
        _water = new bool[_width * _height];
        _unflyable = new bool[_width * _height];
        _cleared = new bool[_width * _height];
        _dynamic = new bool[_width * _height];

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int index = y * _width + x;
                byte cell = pathing.At(x, y);
                _blocked[index] = (cell & W3mPathing.Unwalkable) != 0;
                _unflyable[index] = (cell & W3mPathing.Unflyable) != 0;

                int terrainX = Mathf.Clamp(x / TerrainCornerCells, 0, terrain.Width - 1);
                int terrainY = Mathf.Clamp(y / TerrainCornerCells, 0, terrain.Height - 1);
                _water[index] = terrain.Corners[terrainY * terrain.Width + terrainX].HasWater;
            }
        }
        Ready = true;
    }

    public static PathGrid Build(W3mPathing pathing, W3eTerrain terrain)
    {
        return pathing == null || terrain == null ? null : new PathGrid(pathing, terrain);
    }

    public bool IsBlockedWorld(float worldX, float worldZ, PathLayer layer = PathLayer.Ground)
    {
        WorldToCell(worldX, worldZ, out int x, out int y);
        return Blocked(x, y, layer);
    }

    public bool IsAreaFree(Vector3 world, float radiusWorld, PathLayer layer = PathLayer.Ground)
    {
        int cells = Mathf.Max(1, Mathf.CeilToInt(radiusWorld / CellWorld));
        WorldToCell(world.X, world.Z, out int cx, out int cy);
        for (int dz = -cells; dz <= cells; dz++)
        {
            for (int dx = -cells; dx <= cells; dx++)
            {
                if (Blocked(cx + dx, cy + dz, layer)) return false;
            }
        }
        return true;
    }

    /// <summary>建筑物占格子（动态层）：古树扎根/拔根就靠这一层开关，不动地形阻挡。</summary>
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
                _dynamic[y * _width + x] = true;
            }
        }
    }

    /// <summary>放开建筑物占的格子（拔根、建筑物被拆）。</summary>
    public void RemoveBlocker(Vector3 world, float radiusWorld)
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
                _dynamic[y * _width + x] = false;
            }
        }
    }

    /// <summary>树被砍倒后把这一小圈覆盖成可走（只解锁 doodad 占的格子，不动地形阻挡）。</summary>
    public void ClearArea(Vector3 world, float radiusWorld)
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
                _cleared[y * _width + x] = true;
            }
        }
    }

    private bool Blocked(int x, int y, PathLayer layer = PathLayer.Ground)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height) return true;
        int index = y * _width + x;

        switch (layer)
        {
            case PathLayer.Air:
                return _unflyable[index];
            case PathLayer.Water:
                return !_water[index];
            case PathLayer.Amphibious:
                if (_water[index]) return false;
                if (_cleared[index]) return false;
                if (_dynamic[index]) return true;
                return _blocked[index];
            default:
                if (_cleared[index]) return false;
                if (_dynamic[index]) return true;
                return _blocked[index];
        }
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

    public List<Vector3> FindPath(Vector3 start, Vector3 end, float unitRadius, PathLayer layer = PathLayer.Ground)
    {
        if (!Ready) return null;

        WorldToCell(start.X, start.Z, out int sx, out int sy);
        WorldToCell(end.X, end.Z, out int ex, out int ey);
        sx = Mathf.Clamp(sx, 0, _width - 1);
        sy = Mathf.Clamp(sy, 0, _height - 1);
        ex = Mathf.Clamp(ex, 0, _width - 1);
        ey = Mathf.Clamp(ey, 0, _height - 1);

        int clearance = unitRadius > 16f ? 1 : 0;
        if (SearchBlocked(ex, ey, clearance, layer))
        {
            if (!TryFindNear(ex, ey, clearance, layer, out ex, out ey)) return null;
        }

        if (SearchBlocked(sx, sy, clearance, layer))
        {
            if (!TryFindNear(sx, sy, clearance, layer, out sx, out sy)) return null;
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
            if (current == goal) return BuildPath(cameFrom, goal, sx, sy, unitRadius, layer);
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
                    if (SearchBlocked(nx, ny, clearance, layer)) continue;
                    if (dx != 0 && dz != 0 && (SearchBlocked(cx + dx, cy, clearance, layer) || SearchBlocked(cx, cy + dz, clearance, layer)))
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

    private List<Vector3> BuildPath(Dictionary<int, int> cameFrom, int goal, int sx, int sy, float unitRadius, PathLayer layer)
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
                if (Clear(anchor, points[probe], unitRadius, layer))
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

    private bool Clear(Vector3 from, Vector3 to, float unitRadius, PathLayer layer)
    {
        float distance = new Vector2(to.X - from.X, to.Z - from.Z).Length();
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (CellWorld * 0.5f)));
        int clearance = unitRadius > 16f ? 1 : 0;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = from.Lerp(to, (float)i / steps);
            WorldToCell(point.X, point.Z, out int x, out int y);
            if (SearchBlocked(x, y, clearance, layer)) return false;
        }
        return true;
    }

    private bool SearchBlocked(int x, int y, int clearance, PathLayer layer)
    {
        for (int dz = -clearance; dz <= clearance; dz++)
        {
            for (int dx = -clearance; dx <= clearance; dx++)
            {
                if (Blocked(x + dx, y + dz, layer)) return true;
            }
        }
        return false;
    }

    private bool TryFindNear(int x, int y, int clearance, PathLayer layer, out int foundX, out int foundY)
    {
        for (int radius = 1; radius <= 8; radius++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dz;
                    if (!SearchBlocked(nx, ny, clearance, layer))
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