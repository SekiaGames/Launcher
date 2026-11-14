using Godot;

namespace OpenWC3.Game.UI;

public sealed class GlueModelLayer
{
    public const string SkirmishBackground =
        "res://extract/UI/Glues/MainMenu/MainMenu3d_exp/MainMenu3d_exp.tscn";

    readonly SubViewport _viewport;
    readonly Camera3D _camera;
    readonly Node3D _target;

    public GlueModelLayer(Node parent, string modelPath)
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(1280, 720),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        parent.AddChild(_viewport);
        PackedScene scene = GD.Load<PackedScene>(modelPath);
        Node3D model = scene.Instantiate<Node3D>();
        _viewport.AddChild(model);
        _camera = model.GetNodeOrNull<Camera3D>("camera0");
        _target = model.GetNodeOrNull<Node3D>("camera0_target");
        if (_camera != null) _camera.Current = true;
        _viewport.AddChild(new DirectionalLight3D
        {
            LightEnergy = 1.2f,
            RotationDegrees = new Vector3(-55f, 35f, 0f),
        });
    }

    public Texture2D Texture => _viewport.GetTexture();

    public void FollowCameraTarget()
    {
        if (_camera == null || _target == null) return;
        Vector3 goal = _target.GlobalPosition;
        if (_camera.GlobalPosition.DistanceSquaredTo(goal) < 1f) return;
        _camera.LookAt(goal, Vector3.Up);
    }
}
