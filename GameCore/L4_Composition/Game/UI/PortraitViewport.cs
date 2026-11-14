using Godot;
using System;

namespace OpenWC3.Game;

internal sealed class PortraitViewport
{
    readonly SubViewport _viewport;
    readonly Node3D _stage;
    readonly Camera3D _camera;
    readonly DirectionalLight3D _light;
    readonly PortraitAnimator _animator = new();
    UnitSceneComponent _view;
    Unit _unit;
    string _typeId;
    string _modelPath;
    float _nowSeconds;

    public ViewportTexture Texture { get; }

    public PortraitViewport(Node parent)
    {
        _viewport = new SubViewport
        {
            Name = "PortraitViewport",
            Size = new Vector2I(214, 218),
            TransparentBg = true,
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        parent.AddChild(_viewport);
        _stage = new Node3D { Name = "PortraitStage" };
        _viewport.AddChild(_stage);
        _camera = new Camera3D
        {
            Name = "PortraitCamera",
            Current = true,
            Fov = 35f,
            Near = 1f,
            Far = 5000f,
        };
        _viewport.AddChild(_camera);
        _light = new DirectionalLight3D
        {
            Name = "PortraitLight",
            LightEnergy = 1.25f,
            ShadowEnabled = false,
        };
        _light.LookAtFromPosition(new Vector3(1f, 1f, 1f), Vector3.Zero, Vector3.Up);
        _viewport.AddChild(_light);
        Texture = _viewport.GetTexture();
    }

    public void Show(Unit unit)
    {
        string typeId = unit?.TypeId;
        string modelPath = unit?.Type?.ModelPath;
        if (ReferenceEquals(_unit, unit)
            && string.Equals(_typeId, typeId, StringComparison.Ordinal)
            && string.Equals(_modelPath, modelPath, StringComparison.Ordinal)) return;
        _unit = unit;
        _typeId = typeId;
        _modelPath = modelPath;
        if (_view != null)
        {
            _view.Root.QueueFree();
            _view = null;
        }
        if (unit?.Type == null || string.IsNullOrWhiteSpace(modelPath)) return;
        (PackedScene scene, string path) = LoadPortraitScene(modelPath);
        if (scene == null) return;
        _view = UnitSceneComponent.Create(scene, unit.Player,
unit.Type.ModelScale, unit.Type.IsBuilding, unit.Type.FootprintRadius, path,
            SceneLight.Fallback, War3FogConfig.None);
        _stage.AddChild(_view.Root);
        _view.Root.Position = Vector3.Zero;
        _view.Root.Rotation = new Vector3(0f, Mathf.Pi, 0f);
        FrameModel();
        _animator.Reset(_view, unit, _nowSeconds);
        _animator.Frame(_view, unit, _nowSeconds);
    }

    public void Talk(Unit unit, float durationSeconds)
    {
        if (_view == null || unit == null || !ReferenceEquals(_unit, unit)) return;
        _animator.Talk(unit, durationSeconds, _nowSeconds);
    }

    public void FrameSeconds(double delta)
    {
        _nowSeconds += (float)Math.Max(0d, delta);
        if (_view == null || _unit == null) return;
        _animator.Frame(_view, _unit, _nowSeconds);
    }

    static (PackedScene Scene, string Path) LoadPortraitScene(string mainPath)
    {
        const string root = "res://extract/";
        string relative = mainPath.StartsWith(root, StringComparison.Ordinal)
            ? mainPath[root.Length..] : mainPath;
        string stem = relative.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
            ? relative[..^5] : relative;
        foreach (string candidate in new[] { stem + "_Portrait", stem + "Portrait", stem })
        {
            string path = PathResolve.ResourceFile(candidate, ".tscn");
            PackedScene scene = Assets.LoadScene(path);
            if (scene != null) return (scene, path);
        }
        return (Assets.LoadScene(mainPath), mainPath);
    }

    void FrameModel()
    {
        Aabb bounds = Bounds(_view.Root);
        Vector3 center = bounds.Position + bounds.Size * .5f;
        float height = Mathf.Max(16f, bounds.Size.Y);
        float width = Mathf.Max(16f, Mathf.Max(bounds.Size.X, bounds.Size.Z));
        float distance = Mathf.Max(height, width) * 2.4f;
        Vector3 target = center + new Vector3(0f, height * .16f, 0f);
        _camera.Position = new Vector3(center.X, center.Y + height * .08f, center.Z + distance);
        _camera.LookAt(target, Vector3.Up);
        _camera.Near = Mathf.Max(.5f, distance * .05f);
        _camera.Far = distance * 8f;
    }

    static Aabb Bounds(Node root)
    {
        bool has = false;
        Aabb result = default;
        void Walk(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                Aabb local = mesh.Mesh.GetAabb();
                Aabb world = mesh.GlobalTransform * local;
                if (!has) { result = world; has = true; }
                else result = result.Merge(world);
            }
            foreach (Node child in node.GetChildren()) Walk(child);
        }
        Walk(root);
        return has ? result : new Aabb(new Vector3(-32f, 0f, -32f), new Vector3(64f, 128f, 64f));
    }
}