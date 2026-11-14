using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    /// <summary>玩家设置「游戏镜头距离」（缩放 6 档预设，1 = 原版）。</summary>
    public float CameraDistanceScale { get; private set; } = 1f;

    /// <summary>玩家设置「滑动镜头灵敏度」（方向键/边缘滚屏的平移速度倍率）。</summary>
    public float CameraPanSensitivity { get; private set; } = 1f;

    public void SetCameraDistanceScale(float value)
    {
        CameraDistanceScale = War3Camera.ClampScale(value);
        _camera?.SetPlayerDistanceScale(CameraDistanceScale);
        GD.Print($"[CAMERA] 镜头距离倍率 {CameraDistanceScale:0.###}");
    }

    public void SetCameraPanSensitivity(float value)
    {
        CameraPanSensitivity = War3Camera.ClampScale(value);
        GD.Print($"[CAMERA] 滑动灵敏度 {CameraPanSensitivity:0.###}");
    }

    public void SetCameraPreset(int preset)
    {
        _camera?.SetPreset(preset);
    }

    /// <summary>
    /// 镜头跟随热键（桌面端替代安卓面板那三个开关）。都带 Ctrl，避免和物编热键（纯字母）打架：
    /// Ctrl+F = 总跟随选中的单位，Ctrl+Shift+F = 锁定跟随，Ctrl+Alt+F = 标记并跟随标记单位。
    /// </summary>
    public bool HandleCameraFollowHotkey(Key keycode, bool shift, bool ctrl, bool alt)
    {
        if (keycode != Key.F || !ctrl) return false;
        if (alt) ToggleCameraFollowMarked();
        else if (shift) ToggleCameraFollowLock();
        else ToggleCameraFollowTrack();
        return true;
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
        // 客户端 CameraBounds 就是 w3i 里那 4 个角点（8 个 float）的包围盒，
        // 边界外靠 MiscData [CameraMargins] 放宽（在 War3Camera.ClampTarget 里）。
        // 之前这里把 w3i 边界和地形网格矩形求交集，带外围装饰/路肩的地图会被裁掉一截，
        // 镜头到不了地图角落。
        float[] bounds = _map.Info?.CameraBounds;
        if (bounds != null && bounds.Length >= 8)
        {
            float mapMinX = float.MaxValue, mapMaxX = float.MinValue;
            float mapMinY = float.MaxValue, mapMaxY = float.MinValue;
            for (int i = 0; i + 1 < bounds.Length; i += 2)
            {
                if (!float.IsFinite(bounds[i]) || !float.IsFinite(bounds[i + 1])) continue;
                mapMinX = MathF.Min(mapMinX, bounds[i]);
                mapMaxX = MathF.Max(mapMaxX, bounds[i]);
                mapMinY = MathF.Min(mapMinY, bounds[i + 1]);
                mapMaxY = MathF.Max(mapMaxY, bounds[i + 1]);
            }
            if (mapMaxX - mapMinX > 0f && mapMaxY - mapMinY > 0f)
            {
                // 世界坐标 X 取负，左右两端要跟着翻转。
                float worldMinX = -(mapMaxX - _map.Terrain.OffsetX);
                float worldMaxX = -(mapMinX - _map.Terrain.OffsetX);
                float worldMinZ = mapMinY - _map.Terrain.OffsetY;
                float worldMaxZ = mapMaxY - _map.Terrain.OffsetY;
                _camera.Bounds = new Vector4(worldMinX, worldMinZ, worldMaxX, worldMaxZ);
                GD.Print($"[INIT] 相机边界 w3i ({worldMinX:0},{worldMinZ:0})-({worldMaxX:0},{worldMaxZ:0})");
                return;
            }
        }

        // w3i 没写边界（或写坏）才退回地形矩形。
        float terrainMinX = -(_map.Terrain.Width - 1) * MapWorldBuilder.CellSize;
        float terrainMaxX = 0f;
        float terrainMinZ = 0f;
        float terrainMaxZ = (_map.Terrain.Height - 1) * MapWorldBuilder.CellSize;
        _camera.Bounds = new Vector4(terrainMinX, terrainMinZ, terrainMaxX, terrainMaxZ);
        GD.Print($"[INIT] 相机边界 地形 ({terrainMinX:0},{terrainMinZ:0})-({terrainMaxX:0},{terrainMaxZ:0})");
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
            foreach (MapPlayerInfo player in _map.Players)
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
