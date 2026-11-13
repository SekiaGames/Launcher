using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal float CameraTargetHeightAt(float worldX, float worldZ) =>
        MathF.Max(MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ),
            MapWorldBuilder.WaterHeightAt(_map.Terrain, worldX, worldZ));

    /// <summary>w3i 地图坐标 → 世界坐标（与地形 mesh 的取负 X 约定一致）。</summary>
    private Vector3 ToWorld(float mapX, float mapY)
    {
        float worldX = -(mapX - _map.Terrain.OffsetX);
        float worldZ = mapY - _map.Terrain.OffsetY;
        return new Vector3(worldX, 0f, worldZ);
    }

    private Vector2 ToMap(Vector3 world)
    {
        return new Vector2(_map.Terrain.OffsetX - world.X, world.Z + _map.Terrain.OffsetY);
    }
}
