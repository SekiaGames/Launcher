using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    public void SetCameraPreset(int preset)
    {
        _camera?.SetPreset(preset);
    }

    public void FocusMapPoint(float mapX, float mapY)
    {
        float worldX = -(mapX - _map.Terrain.OffsetX);
        float worldZ = mapY - _map.Terrain.OffsetY;
        _camera?.Focus(new Vector3(worldX, MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ), worldZ));
    }

    public bool SetCinematicModel(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || _camera == null) return false;
        PackedScene scene = Assets.LoadScene(PathResolve.ModelScene(modelPath));
        if (scene == null)
        {
            GD.PushWarning($"[CAMERA] 电影镜头模型缺失 {modelPath}");
            return false;
        }
        Node3D root = scene.Instantiate<Node3D>();
        if (!_camera.SetCinematicModel(root))
        {
            GD.PushWarning($"[CAMERA] {modelPath} 里没有可用的 camera0 轨道");
            return false;
        }
        GD.Print($"[CAMERA] 电影镜头 {modelPath}");
        return true;
    }

    private void SetCameraBounds()
    {
        // 地形覆盖范围（世界坐标 X 取负）
        float terrainMinX = -(_map.Terrain.Width - 1) * MapWorldBuilder.CellSize;
        float terrainMaxX = 0f;
        float terrainMinZ = 0f;
        float terrainMaxZ = (_map.Terrain.Height - 1) * MapWorldBuilder.CellSize;

        // w3i 的 CameraBounds 是地图坐标下的矩形，转成世界坐标后与地形取交集
        float[] bounds = _map.Info?.CameraBounds;
        if (bounds != null && bounds.Length >= 4)
        {
            float mapMinX = bounds[0];
            float mapMinY = bounds[1];
            float mapMaxX = bounds[2];
            float mapMaxY = bounds[3];
            if (mapMaxX - mapMinX > 0f && mapMaxY - mapMinY > 0f)
            {
                float worldMinX = -(mapMaxX - _map.Terrain.OffsetX);
                float worldMaxX = -(mapMinX - _map.Terrain.OffsetX);
                float worldMinZ = mapMinY - _map.Terrain.OffsetY;
                float worldMaxZ = mapMaxY - _map.Terrain.OffsetY;

                terrainMinX = Mathf.Max(terrainMinX, worldMinX);
                terrainMaxX = Mathf.Min(terrainMaxX, worldMaxX);
                terrainMinZ = Mathf.Max(terrainMinZ, worldMinZ);
                terrainMaxZ = Mathf.Min(terrainMaxZ, worldMaxZ);
            }
        }

        _camera.Bounds = new Vector4(terrainMinX, terrainMinZ, terrainMaxX, terrainMaxZ);
        GD.Print($"[INIT] 相机边界 ({terrainMinX:0},{terrainMinZ:0})-({terrainMaxX:0},{terrainMaxZ:0})");
    }

    private void FocusStartPosition()
    {
        Vector3 focus = Vector3.Zero;
        if (_startFocus.TryGetValue(ViewPlayer, out Vector3 planned))
        {
            focus = planned;
        }
        else
        {
            foreach (W3iPlayer player in _map.Players)
            {
                if (player.Id != ViewPlayer) continue;
                if (player.StartX == 0f && player.StartY == 0f) continue;
                focus = ToWorld(player.StartX, player.StartY);
                break;
            }
        }

        focus.Y = MapWorldBuilder.HeightAt(_map.Terrain, focus.X, focus.Z);
        GD.Print($"[INIT] 相机焦点 P{ViewPlayer} {focus}");
        _camera.Focus(focus);
    }
}
