using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public readonly record struct DynamicLight(Vector4 PositionOrDir, Vector4 Params, Vector4 Color, Vector4 Ambient)
{
    public const int MaxLights = 8;

    public static DynamicLight FromNode(Node3D node)
    {
        int type = node.GetMeta("war3_light_type").AsInt32();
        Vector3 color = node.GetMeta("war3_light_color").AsVector3();
        Vector3 ambient = node.GetMeta("war3_light_ambient_color").AsVector3();
        Vector3 origin = type == 1
            ? node.GetMeta("war3_light_direction").AsVector3()
            : node.GlobalPosition;
        return new DynamicLight(
            new Vector4(origin.X, origin.Y, origin.Z, type),
            new Vector4(node.GetMeta("war3_light_attenuation_start").AsSingle(),
                node.GetMeta("war3_light_attenuation_end").AsSingle(), 0f, 0f),
            new Vector4(color.X, color.Y, color.Z, node.GetMeta("war3_light_intensity").AsSingle()),
            new Vector4(ambient.X, ambient.Y, ambient.Z,
                node.GetMeta("war3_light_ambient_intensity").AsSingle()));
    }
}

public sealed class DynamicLightCollector
{
    readonly List<DynamicLight> _lights = new();
    public IReadOnlyList<DynamicLight> Lights => _lights;
    public int Count => _lights.Count;
    public bool IsFull => _lights.Count >= DynamicLight.MaxLights;

    public void Clear() => _lights.Clear();

    public void Add(DynamicLight light)
    {
        if (!IsFull) _lights.Add(light);
    }
}

public static class DynamicLightUniforms
{
    public static Texture2D Build(DynamicLightCollector lights)
    {
        int count = Math.Clamp(lights.Count, 0, DynamicLight.MaxLights);
        var image = Image.CreateEmpty(4, Math.Max(1, count), false, Image.Format.Rgbaf);
        for (int i = 0; i < count; i++)
        {
            DynamicLight light = lights.Lights[i];
            image.SetPixel(0, i, ToColor(light.PositionOrDir));
            image.SetPixel(1, i, ToColor(light.Color));
            image.SetPixel(2, i, ToColor(light.Ambient));
            image.SetPixel(3, i, ToColor(light.Params));
        }
        return ImageTexture.CreateFromImage(image);
    }

    static Color ToColor(Vector4 value) => new(value.X, value.Y, value.Z, value.W);
}

public static class ModelLightRig
{
    public static void Apply(Node3D model, SceneLight light)
    {
        if (!model.IsInsideTree())
        {
            model.Ready += () => Apply(model, light);
            return;
        }
        Callable next = Callable.From(() => Write(model, light));
        model.GetTree().Connect(SceneTree.SignalName.ProcessFrame, next,
            (uint)GodotObject.ConnectFlags.OneShot);
    }

    static void Write(Node3D model, SceneLight light)
    {
        if (!GodotObject.IsInstanceValid(model) || !model.IsInsideTree()) return;
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
                material.SetShaderParameter("war3_light_data", data);
                material.SetShaderParameter("war3_light_count", lights.Count);
                material.SetShaderParameter("war3_scene_direction", light.Direction);
                material.SetShaderParameter("war3_scene_color", light.DiffuseColor * light.DiffuseIntensity);
                material.SetShaderParameter("war3_scene_ambient", light.AmbientColor * light.AmbientIntensity);
            }
        }
    }
}
