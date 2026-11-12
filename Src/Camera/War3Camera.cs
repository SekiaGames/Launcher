using Godot;
using System;

namespace Launcher;

public sealed class War3Camera
{
    private static readonly float[] DistancePresets = { 1650f, 1600f, 1500f, 1400f, 1275f, 1100f };
    private static readonly float[] AngleFromVertical = { 56f, 49f, 42f, 35f, 28f, 21f };

    public Camera3D Camera { get; }
    public Vector3 Target { get; set; }
    public int Preset { get; private set; } = 2;
    public Vector4 Bounds { get; set; } = new(-1e9f, -1e9f, 1e9f, 1e9f);

    public War3Camera(Node parent)
    {
        Camera = new Camera3D
        {
            Name = "War3Camera",
            Fov = 54.43f,
            Near = 100f,
            Far = 5000f,
            Current = true,
        };
        parent.AddChild(Camera);
    }

    public void SetPreset(int preset)
    {
        Preset = Mathf.Clamp(preset, 0, DistancePresets.Length - 1);
        Update();
    }

    public void Zoom(int steps)
    {
        SetPreset(Preset + steps);
    }

    public void Pan(Vector2 delta)
    {
        float scale = DistancePresets[Preset] / 1500f;
        Target += new Vector3(delta.X * scale, 0f, delta.Y * scale);
        ClampTarget();
        Update();
    }

    public void Focus(Vector3 point)
    {
        Target = new Vector3(point.X, point.Y, point.Z);
        ClampTarget();
        Update();
    }

    private void ClampTarget()
    {
        Target = new Vector3(
            Mathf.Clamp(Target.X, Bounds.X, Bounds.Z),
            Target.Y,
            Mathf.Clamp(Target.Z, Bounds.Y, Bounds.W));
    }

    public void Update()
    {
        float angle = Mathf.DegToRad(AngleFromVertical[Preset]);
        float distance = DistancePresets[Preset];
        float horizontal = distance * Mathf.Sin(angle);
        float vertical = distance * Mathf.Cos(angle);

        var offset = new Vector3(0f, vertical, horizontal);
        var position = Target + offset;
        position.Y = Mathf.Max(position.Y, Target.Y + 60f);

        Camera.LookAtFromPosition(position, Target, Vector3.Up);
    }
}