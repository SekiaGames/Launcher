using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class MapWorldBuilder
{
    private static bool TriangleHeightAt(List<Vector3> triangles, int start, float x, float z, out float y)
    {
        Vector3 vector = triangles[start];
        Vector3 vector2 = triangles[start + 1];
        Vector3 vector3 = triangles[start + 2];
        float num = (vector2.Z - vector3.Z) * (vector.X - vector3.X) + (vector3.X - vector2.X) * (vector.Z - vector3.Z);
        if (Mathf.Abs(num) < 0.0001f)
        {
            y = 0f;
            return false;
        }
        float num2 = ((vector2.Z - vector3.Z) * (x - vector3.X) + (vector3.X - vector2.X) * (z - vector3.Z)) / num;
        float num3 = ((vector3.Z - vector.Z) * (x - vector3.X) + (vector.X - vector3.X) * (z - vector3.Z)) / num;
        float num4 = 1f - num2 - num3;
        if (num2 < -0.02f || num3 < -0.02f || num4 < -0.02f)
        {
            y = 0f;
            return false;
        }
        y = num2 * vector.Y + num3 * vector2.Y + num4 * vector3.Y;
        return true;
    }
}
