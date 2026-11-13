using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.Zero;
        Camera3D camera = _camera.Camera;
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = camera.ProjectRayNormal(screenPosition);

        Vector3 previous = origin;
        for (float travel = 16f; travel < 20000f; travel += 16f)
        {
            Vector3 sample = origin + direction * travel;
            float height = MapWorldBuilder.HeightAt(_map.Terrain, sample.X, sample.Z);
            if (sample.Y <= height)
            {
                Vector3 low = previous;
                Vector3 high = sample;
                for (int i = 0; i < 14; i++)
                {
                    Vector3 mid = (low + high) * 0.5f;
                    if (mid.Y <= MapWorldBuilder.HeightAt(_map.Terrain, mid.X, mid.Z)) high = mid;
                    else low = mid;
                }
                point = high;
                return true;
            }
            previous = sample;
        }
        return false;
    }
}
