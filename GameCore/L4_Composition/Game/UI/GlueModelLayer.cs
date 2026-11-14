using Godot;

namespace OpenWC3.Game.UI;

public sealed class GlueModelLayer
{
    public const string SkirmishBackground =
        "res://extract/UI/Glues/MainMenu/MainMenu3d_exp/MainMenu3d_exp.tscn";

    static readonly Vector3 SceneDirection = new(1f, 0f, 0f);
    static readonly Vector3 SceneColor = new(0.62f, 0.62f, 0.62f);
    static readonly Vector3 SceneAmbient = new(0.22f, 0.22f, 0.22f);

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
        ApplyLights(model);
    }

    static void ApplyLights(Node3D model)
    {
        var lights = new DynamicLightCollector();
        foreach (Node child in model.FindChildren("*", "Node3D", true, false))
        {
            if (child.HasMeta("war3_light_type")) lights.Add(DynamicLight.FromNode((Node3D)child));
        }

        Texture2D data = DynamicLightUniforms.Build(lights);
        foreach (Node child in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mesh = (MeshInstance3D)child;
            for (int surface = 0; surface < mesh.GetSurfaceOverrideMaterialCount(); surface++)
            {
                if (mesh.GetSurfaceOverrideMaterial(surface) is not ShaderMaterial material) continue;
                material.SetShaderParameter("war3_lighting_enabled", true);
                material.SetShaderParameter("war3_light_data", data);
                material.SetShaderParameter("war3_light_count", lights.Count);
                material.SetShaderParameter("war3_scene_direction", SceneDirection);
                material.SetShaderParameter("war3_scene_color", SceneColor);
                material.SetShaderParameter("war3_scene_ambient", SceneAmbient);
            }
        }
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
