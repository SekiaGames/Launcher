using Godot;
using System;

namespace OpenWC3.Game;

public sealed class SkyDomeRenderer
{
    static readonly string[] SkyModels =
    {
        "LordaeronSummerSky", "LordaeronSummerSky", "LordaeronWinterSky", "LordaeronWinterSky",
        "LordaeronWinterSkyRed", "LordaeronWinterSkyBrightGreen", "LordaeronWinterSky",
        "LordaeronWinterSkyPink", "LordaeronWinterSkyPurple", "LordaeronWinterSkyYellow",
        "FoggedSky", "BlizzardSky", "DalaranSky", "FelwoodSky", "LordaeronFallSky",
    };

    const string CelestialModel = @"Environment\Sky\Sky\skyLight";
    const float ShellSizeLimit = 3000f;

    readonly Node3D _dome;
    readonly Node3D _celestial;

    private SkyDomeRenderer(Node3D dome, Node3D celestial)
    {
        _dome = dome;
        _celestial = celestial;
    }

    public static SkyDomeRenderer TryCreate(Node parent, int skyboxType)
    {
        string skyName = SkyNameFor(skyboxType);
        Node3D model = Load($@"Environment\Sky\{skyName}\{skyName}");
        if (model == null)
        {
            skyName = SkyModels[0];
            model = Load($@"Environment\Sky\{skyName}\{skyName}");
        }
        if (model == null) return null;

        var dome = new Node3D { Name = "SkyDome" };
        model.Name = "Model";
        dome.AddChild(model);
        parent.AddChild(dome);

        Node3D celestial = Load(CelestialModel);
        if (celestial != null)
        {
            celestial.Name = "Celestial";
            RemoveSkyShell(celestial);
            dome.AddChild(celestial);
        }

        GD.Print($"[SKY] 穹顶 {skyName} 日月 {(celestial != null ? "已载入" : "缺模型")}");
        return new SkyDomeRenderer(dome, celestial);
    }

    static Node3D Load(string war3Path)
    {
        PackedScene scene = Assets.LoadScene(PathResolve.ModelScene(war3Path));
        if (scene == null)
        {
            GD.Print($"[SKY] 缺模型 {war3Path}");
            return null;
        }
        Node3D model = scene.Instantiate<Node3D>();
        if (model?.FindChild("AnimationTree", true, false) is AnimationTree tree) tree.Active = false;
        return model;
    }

    static void RemoveSkyShell(Node3D model)
    {
        foreach (Node node in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            Vector3 size = mesh.Mesh.GetAabb().Size;
            if (Mathf.Max(size.X, Mathf.Max(size.Y, size.Z)) > ShellSizeLimit) mesh.Visible = false;
        }
    }

    public static string SkyNameFor(int skyboxType)
        => skyboxType is >= 0 and < 15 ? SkyModels[skyboxType] : SkyModels[0];

    public void FrameSeconds(Vector3 focus, float hours)
    {
        if (_dome == null || !GodotObject.IsInstanceValid(_dome)) return;
        _dome.Position = focus;
        if (_celestial == null || !GodotObject.IsInstanceValid(_celestial)) return;
        _celestial.Rotation = new Vector3(0f, Mathf.Tau * (hours / 24f), 0f);
    }
}
