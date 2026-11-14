using Godot;
using System;

namespace OpenWC3.Game;

public sealed class ScoutedBlight
{
    private const float CellSize = MapWorldBuilder.CellSize;

    private readonly bool[] _world;
    private readonly bool[] _observed;
    private readonly int _width;
    private readonly int _height;

    public int Version { get; private set; }

    public int BlightedCells { get; private set; }

    public int WorldCells { get; private set; }

    public ScoutedBlight(W3eTerrain terrain)
    {
        _width = Math.Max(1, terrain.Width);
        _height = Math.Max(1, terrain.Height);
        _world = new bool[_width * _height];
        _observed = new bool[_width * _height];

        W3eTerrain.Corner[] corners = terrain.Corners;
        for (int index = 0; index < _world.Length && index < corners.Length; index++)
        {
            if (!corners[index].Blight) continue;
            _world[index] = true;
            _observed[index] = true;
            BlightedCells++;
            WorldCells++;
        }
    }

    public bool IsBlighted(int x, int y) =>
        x >= 0 && y >= 0 && x < _width && y < _height && _observed[y * _width + x];

    public bool IsMinimapBlighted(float mapX, float mapY)
    {
        int x = (int)MathF.Floor(mapX / CellSize);
        int y = (int)MathF.Floor(mapY / CellSize);
        return x >= 0 && y >= 0 && x + 1 < _width && y + 1 < _height
            && IsBlighted(x, y) && IsBlighted(x + 1, y)
            && IsBlighted(x, y + 1) && IsBlighted(x + 1, y + 1);
    }

    public int Stamp(Vector3 world, float radius)
    {
        if (radius <= 0f) return 0;

        float centerX = -world.X / CellSize;
        float centerY = world.Z / CellSize;
        float radiusCells = radius / CellSize;
        float radiusSquared = radiusCells * radiusCells;

        int minX = Math.Max(0, (int)MathF.Floor(centerX - radiusCells));
        int maxX = Math.Min(_width - 1, (int)MathF.Ceiling(centerX + radiusCells));
        int minY = Math.Max(0, (int)MathF.Floor(centerY - radiusCells));
        int maxY = Math.Min(_height - 1, (int)MathF.Ceiling(centerY + radiusCells));

        int painted = 0;
        for (int y = minY; y <= maxY; y++)
        {
            float dy = y + 0.5f - centerY;
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - centerX;
                if (dx * dx + dy * dy > radiusSquared) continue;

                int index = y * _width + x;
                if (_world[index]) continue;
                _world[index] = true;
                painted++;
            }
        }
        WorldCells += painted;
        return painted;
    }

    public bool Refresh(FogOfWar fog, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = _width;
        minY = _height;
        maxX = -1;
        maxY = -1;
        bool changed = false;

        for (int y = 0; y < _height; y++)
        {
            float worldZ = y * CellSize;
            for (int x = 0; x < _width; x++)
            {
                int index = y * _width + x;
                bool current = _world[index];
                if (_observed[index] == current) continue;
                if (fog != null && !fog.IsVisible(new Vector3(-x * CellSize, 0f, worldZ))) continue;

                _observed[index] = current;
                BlightedCells += current ? 1 : -1;
                changed = true;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (!changed) return false;
        Version++;
        return true;
    }
}
