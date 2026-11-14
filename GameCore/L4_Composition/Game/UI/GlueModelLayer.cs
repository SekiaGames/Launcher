using Godot;

namespace OpenWC3.Game.UI;

public sealed class GlueModelLayer
{
    public const string SkirmishBackground =
        "res://extract/UI/Glues/MainMenu/MainMenu3d_exp/MainMenu3d_exp.tscn";

    static readonly SceneLight GlueLight = new(
        new Vector3(1f, 0f, 0f), Vector3.One, 0.62f, Vector3.One, 0.22f);

    readonly SubViewport _viewport;
    readonly Camera3D _camera;
    readonly Node3D _target;

    public GlueModelLayer(Node parent, string modelPath)
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(1920, 1080),
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
        ModelLightRig.Apply(model, GlueLight);
    }

    public Texture2D Texture => _viewport.GetTexture();

    public void Resize(Vector2I size)
    {
        if (size.X <= 0 || size.Y <= 0 || _viewport.Size == size) return;
        _viewport.Size = size;
    }

    public void FollowCameraTarget()
    {
        if (_camera == null || _target == null) return;
        Vector3 goal = _target.GlobalPosition;
        if (_camera.GlobalPosition.DistanceSquaredTo(goal) < 1f) return;
        _camera.LookAt(goal, Vector3.Up);
    }
}
