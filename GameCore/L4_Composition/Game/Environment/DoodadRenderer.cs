using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    private void BuildDoodads(W3eTerrain terrain, War3MapDoodads doodads)
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
        int num5 = 0;
        int num6 = 0;
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
            MeshInstance3D shadow = AddDoodadShadow(node3D, doodadModel, node3D2.Position, entry.Id, num);
            if (shadow != null)
            {
                num5++;
            }
            else if (!string.IsNullOrEmpty(doodadModel.Shadow))
            {
                num6++;
            }
            DoodadDefinition doodadDefinition = DoodadMeshLibrary.Describe(entry.Id);
            if (doodadDefinition != null && doodadDefinition.IsTree)
            {
                Trees.Add(new TreeState
                {
                    Node = node3D2,
                    Shadow = shadow,
                    Position = node3D2.Position,
                    Id = entry.Id,
                    Hp = doodadDefinition.Hp
                });
            }
            num++;
        }
        GD.Print($"[DOODAD] 装饰物 {num}（模型 {DoodadMeshLibrary.Count} 种），隐藏 {num2} 死亡 {num3} 缺模型 {num4}，树 {Trees.Count}，"
            + $"阴影 {num5} 缺贴图 {num6}");
    }

    /// <summary>装饰物/树的落地阴影：贴图在 ReplaceableTextures/Shadows，一格 32 单位，底边锚在贴图 30%/70% 处。</summary>
    private static MeshInstance3D AddDoodadShadow(Node3D parent, DoodadModel model, Vector3 position, string id, int index)
    {
        if (string.IsNullOrEmpty(model.Shadow))
        {
            return null;
        }

        Texture2D texture = Assets.LoadTexture(Assets.Res("ReplaceableTextures/Shadows/" + model.Shadow + ".png"));
        if (texture == null)
        {
            return null;
        }

        Vector2 textureSize = texture.GetSize();
        float width = textureSize.X * 32f;
        float height = textureSize.Y * 32f;

        var shadow = new MeshInstance3D
        {
            Name = $"Shadow_{id}_{index}",
            Mesh = new QuadMesh { Size = new Vector2(width, height) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = texture,
                AlbedoColor = new Color(0f, 0f, 0f, 0.65f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            },
            Position = position + new Vector3(-0.2f * width, 2f, 0.2f * height),
            Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0f),
            Scale = new Vector3(-1f, 1f, 1f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(shadow, forceReadableName: false, Node.InternalMode.Disabled);
        return shadow;
    }

    private static Vector3 DoodadScale(float baseScale, DoodadPlacement entry)
    {
        float num = ((entry.ScaleX > 0f) ? entry.ScaleX : 1f);
        float num2 = ((entry.ScaleY > 0f) ? entry.ScaleY : 1f);
        float num3 = ((entry.ScaleZ > 0f) ? entry.ScaleZ : 1f);
        return new Vector3(baseScale - 1f + num, baseScale - 1f + num2, baseScale - 1f + num3);
    }
}
