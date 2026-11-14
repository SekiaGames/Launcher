using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    /// <summary>
    /// 相机目标点的高度，与客户端 <c>CameraTargetHeightAt</c> 同口径：
    ///   max(地形渲染网格表面（同一条 TL–BR 三角面 + 斜坡抬升）, 双线性水面, 可通行面)
    /// 之前走的是四角双线性 <c>HeightAt</c>：网格是三角面、采样是双线性，坡地/悬崖边
    /// 镜头高度与看到的地面差几十单位；桥面完全不查，过桥时镜头贴着桥下地面。
    /// </summary>
    internal float CameraTargetHeightAt(float worldX, float worldZ)
    {
        if (_map?.Terrain == null) return 0f;
        float height = MathF.Max(
            MapWorldBuilder.SurfaceHeightAt(_map.Terrain, worldX, worldZ),
            MapWorldBuilder.WaterPlaneHeightAt(_map.Terrain, worldX, worldZ));
        if (_terrainBuilder != null && _terrainBuilder.TryGetWalkableHeight(worldX, worldZ, out float deck))
            height = MathF.Max(height, deck);
        return height;
    }

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
