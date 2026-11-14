using Godot;
using System;

namespace OpenWC3.Game;

public sealed class SkyBackground
{
    const int SkyCanvasLayer = 0;
    const int BackgroundCanvasLimit = SkyCanvasLayer + 1;

    readonly SkyDomeRenderer _sky;
    readonly SubViewport _viewport;
    readonly Camera3D _camera;
    readonly TextureRect _screen;

    private SkyBackground(SkyDomeRenderer sky, SubViewport viewport, Camera3D camera,
        TextureRect screen)
    {
        _sky = sky;
        _viewport = viewport;
        _camera = camera;
        _screen = screen;
    }

    public static SkyBackground TryCreate(Node parent, int skyboxType)
    {
        var viewport = new SubViewport
        {
            Name = "SkyViewport",
            OwnWorld3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Disabled,
        };
        parent.AddChild(viewport);

        var world = new Node3D { Name = "SkyWorld" };
        viewport.AddChild(world);

        var camera = new Camera3D
        {
            Name = "SkyCamera",
            Projection = Camera3D.ProjectionType.Perspective,
            Near = 1f,
            Far = 8000f,
        };
        world.AddChild(camera);
        camera.Current = true;

        SkyDomeRenderer sky = SkyDomeRenderer.TryCreate(world, skyboxType);
        if (sky == null)
        {
            viewport.QueueFree();
            return null;
        }

        var layer = new CanvasLayer { Name = "SkyBackground", Layer = SkyCanvasLayer };
        parent.AddChild(layer);
        var screen = new TextureRect
        {
            Name = "SkyImage",
            Texture = viewport.GetTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(screen);

        var environment = new WorldEnvironment
        {
            Name = "SkyCanvas",
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Canvas,
                BackgroundCanvasMaxLayer = BackgroundCanvasLimit,
                AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            },
        };
        parent.AddChild(environment);

        GD.Print($"[SKY] 背景层 视口 {viewport.Size}，画布层 {SkyCanvasLayer}");
        return new SkyBackground(sky, viewport, camera, screen);
    }

    public void FrameSeconds(Camera3D view, float hours)
    {
        if (view == null || !GodotObject.IsInstanceValid(_viewport)) return;
        Vector2 visible = view.GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        var size = new Vector2I(Mathf.Max(1, (int)visible.X), Mathf.Max(1, (int)visible.Y));
        if (_viewport.Size != size) _viewport.Size = size;
        if (_screen.Size != visible)
        {
            _screen.Position = Vector2.Zero;
            _screen.Size = visible;
        }
        _camera.Fov = view.Fov;
        _camera.Near = Mathf.Max(0.01f, view.Near);
        _camera.Far = Mathf.Max(_camera.Near + 1f, view.Far);
        _camera.Rotation = view.GlobalRotation;
        _sky.FrameSeconds(Vector3.Zero, hours);
    }
}
