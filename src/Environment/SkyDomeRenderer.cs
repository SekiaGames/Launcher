using Godot;
using System;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

/// <summary>
/// 三维天空穹顶（客户端用 Sky 模型而不是全景贴图）：按 w3i tileset 选
/// `Environment/Sky/*` 的模型，跟着镜头走，保留全景天空做环境底。
/// </summary>
public sealed class SkyDomeRenderer
{
    private readonly Node3D _dome;

    private SkyDomeRenderer(Node3D dome)
    {
        _dome = dome;
    }

    public static SkyDomeRenderer TryCreate(Node parent, W3iInfo info)
    {
        string skyName = SkyboxRenderer.SkyNameFor(info != null ? info.Tileset : 'F');
        string path = PathResolve.ModelScene($@"Environment\Sky\{skyName}\{skyName}");
        if (path == null)
        {
            skyName = "Sky";
            path = PathResolve.ModelScene($@"Environment\Sky\{skyName}\{skyName}");
            if (path == null) return null;
        }

        PackedScene scene = Assets.LoadScene(path);
        if (scene == null) return null;

        Node3D model = scene.Instantiate<Node3D>();
        if (model == null) return null;

        var dome = new Node3D { Name = "SkyDome" };
        model.Name = "Model";
        dome.AddChild(model);
        parent.AddChild(dome);

        GD.Print($"[SKY] 穹顶 {skyName}");
        return new SkyDomeRenderer(dome);
    }

    public void Tick(Vector3 focus)
    {
        if (_dome == null || !GodotObject.IsInstanceValid(_dome)) return;
        _dome.Position = focus;
    }
}