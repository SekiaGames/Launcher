using Godot;
using System;

namespace OpenWC3.Game;

internal static class UnitTerrainTilt
{
    internal static (float Pitch, float Roll) Compute(
        float worldX, float worldZ, float facing, float radius,
        float maxPitchDegrees, float maxRollDegrees, Func<float, float, float> heightAt)
    {
        float fixedPitch = maxPitchDegrees < 0f ? maxPitchDegrees * Mathf.Pi / 180f : 0f;
        float fixedRoll = maxRollDegrees < 0f ? maxRollDegrees * Mathf.Pi / 180f : 0f;
        if (radius <= 0.01f || heightAt == null) return (fixedPitch, fixedRoll);

        float sin = Mathf.Sin(facing);
        float cos = Mathf.Cos(facing);

        float forward = heightAt(worldX + radius * sin, worldZ - radius * cos);
        float backward = heightAt(worldX - radius * sin, worldZ + radius * cos);
        float right = heightAt(worldX + radius * cos, worldZ + radius * sin);
        float left = heightAt(worldX - radius * cos, worldZ - radius * sin);

        float maxPitch = Mathf.Pi / 180f * Mathf.Max(0f, maxPitchDegrees);
        float maxRoll = Mathf.Pi / 180f * Mathf.Max(0f, maxRollDegrees);
        float pitch = Mathf.Clamp(Mathf.Atan2(forward - backward, radius * 2f), -maxPitch, maxPitch);
        float roll = Mathf.Clamp(Mathf.Atan2(left - right, radius * 2f), -maxRoll, maxRoll);
        return (maxPitchDegrees < 0f ? fixedPitch : pitch, maxRollDegrees < 0f ? fixedRoll : roll);
    }
}
