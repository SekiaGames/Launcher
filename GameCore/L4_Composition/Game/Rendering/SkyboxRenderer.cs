using Godot;
using System;

namespace OpenWC3.Game;

public sealed class SkyboxRenderer
{
    private readonly WorldEnvironment _world;
    private readonly Godot.Environment _environment;

    private SkyboxRenderer(WorldEnvironment world, Godot.Environment environment)
    {
        _world = world;
        _environment = environment;
    }

    public static SkyboxRenderer TryCreate(Node parent, int skyboxType)
    {
        string skyName = SkyDomeRenderer.SkyNameFor(skyboxType);
        string relative = $"Environment/Sky/{skyName}/{skyName}.png";
        if (!Assets.Exists(relative))
        {
            skyName = "LordaeronSummerSky";
            relative = $"Environment/Sky/{skyName}/{skyName}.png";
            if (!Assets.Exists(relative)) return null;
        }

        Texture2D panorama = Assets.LoadTexture(Assets.Res(relative));
        if (panorama == null) return null;

        var sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = panorama } };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
        };

        var world = new WorldEnvironment { Name = "Skybox", Environment = environment };
        parent.AddChild(world);
        GD.Print($"[SKY] 全景底 {skyName}");
        return new SkyboxRenderer(world, environment);
    }

    public void SetEnabled(bool enabled)
    {
        if (_world == null || !GodotObject.IsInstanceValid(_world)) return;
        _world.Environment = enabled ? _environment : null;
    }

}
