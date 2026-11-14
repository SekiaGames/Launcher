using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    public sealed record TreeShadow(string TextureName, Vector3 Position, TreeState Tree);

    private readonly List<TreeShadow> _treeShadows = new();

    public IReadOnlyList<TreeShadow> TreeShadows => _treeShadows;

    private void BuildDoodads(TerrainData terrain, War3MapDoodads doodads)
    {
        Node3D node3D = new Node3D
        {
            Name = "MapDoodads"
        };
        Root.AddChild(node3D, forceReadableName: false, Node.InternalMode.Disabled);
        float offsetX = terrain.OffsetX;
        float offsetY = terrain.OffsetY;
        int num = 0;
        int num2 = 0;
        int num3 = 0;
        int num4 = 0;
        foreach (DoodadPlacement entry in doodads.Entries)
        {
            if (entry.Hidden)
            {
                num2++;
                continue;
            }
            if (entry.Dead)
            {
                num3++;
                continue;
            }
            DoodadModel doodadModel = DoodadMeshLibrary.Get(entry.Id, entry.Variation);
            if (doodadModel?.Scene == null)
            {
                num4++;
                continue;
            }
            Node3D node3D2 = doodadModel.Scene.Instantiate<Node3D>(PackedScene.GenEditState.Disabled);
            node3D2.Position = new Vector3(0f - (entry.X - offsetX), entry.Z, entry.Y - offsetY);
            node3D2.Rotation = new Vector3(0f, (float)Math.PI + entry.Angle, 0f);
            node3D2.Scale = DoodadScale(doodadModel.BaseScale, entry);
            node3D2.Name = $"{entry.Id}_{num}";
            ModelLibrary.ApplyMissingMaterials(node3D2, doodadModel.ScenePath);
            DoodadMeshLibrary.ApplyReplaceableTexture(node3D2, doodadModel);
            ApplyUnitTextureFilter(node3D2);
            node3D.AddChild(node3D2, forceReadableName: false, Node.InternalMode.Disabled);
            DoodadDefinition doodadDefinition = DoodadMeshLibrary.Describe(entry.Id);
            TreeState tree = null;
            if (doodadDefinition != null && doodadDefinition.IsTree)
            {
                tree = new TreeState
                {
                    Node = node3D2,
                    Position = node3D2.Position,
                    Id = entry.Id,
                    Hp = doodadDefinition.Hp
                };
                Trees.Add(tree);
            }
            if (!string.IsNullOrEmpty(doodadModel.Shadow))
            {
                _treeShadows.Add(new TreeShadow(doodadModel.Shadow, node3D2.Position, tree));
            }
            // 桥/平台：模型表面就是那里的通行高度（客户端同样按 walkable 收集）。
            if (doodadDefinition is { Walkable: true }) Walkable.Add(node3D2);
            num++;
        }
        GD.Print($"[DOODAD] 装饰物 {num}（模型 {DoodadMeshLibrary.Count} 种），隐藏 {num2} 死亡 {num3} 缺模型 {num4}，树 {Trees.Count}，"
            + $"带阴影 {_treeShadows.Count}");
    }

    private static Vector3 DoodadScale(float baseScale, DoodadPlacement entry)
    {
        float num = ((entry.ScaleX > 0f) ? entry.ScaleX : 1f);
        float num2 = ((entry.ScaleY > 0f) ? entry.ScaleY : 1f);
        float num3 = ((entry.ScaleZ > 0f) ? entry.ScaleZ : 1f);
        return new Vector3(baseScale - 1f + num, baseScale - 1f + num2, baseScale - 1f + num3);
    }
}
