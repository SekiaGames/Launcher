using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenWC3.Game;

public static class AttachmentProbe
{
    private static readonly string[] Models =
    {
        "Units/Human/Footman/Footman",
        "Units/Human/Rifleman/Rifleman",
        "Units/Human/HeroArchMage/HeroArchMage",
        "Units/Orc/Grunt/grunt",
        "Units/Orc/HeroBladeMaster/HeroBladeMaster",
        "Units/Undead/Acolyte/Acolyte",
        "Units/NightElf/Archer/Archer",
        "Buildings/Human/TownHall/TownHall",
    };

    private static readonly string[] Requests = { "overhead", "origin", "weapon", "chest", "hand right" };

    public static int Run()
    {
        int failed = 0;
        foreach (string model in Models)
        {
            string path = Assets.Res(model + ".tscn");
            PackedScene scene = Assets.LoadScene(path);
            if (scene == null)
            {
                GD.Print($"[ATTACH] 缺 {path}");
                failed++;
                continue;
            }

            Node3D root = scene.Instantiate<Node3D>();
            var skeleton = root.FindChild("Skeleton3D", true, false) as Skeleton3D;

            var attachments = new List<string>();
            foreach (Node node in root.FindChildren("attach_*", "", true, false))
            {
                if (node is not Node3D attach) continue;
                string label = attach.Name.ToString();
                if (label.StartsWith("attach_node", StringComparison.Ordinal)) continue;

                string boneText = "";
                if (attach is BoneAttachment3D bone)
                {
                    int boneIndex = skeleton != null ? skeleton.FindBone(bone.BoneName) : -1;
                    boneText = $"(骨骼 {bone.BoneName}{(boneIndex >= 0 ? "✓" : "✗")})";
                }
                Node3D point = attach.GetChildren().OfType<Node3D>().FirstOrDefault();
                float height = point != null ? point.Position.Y : attach.Position.Y;
                attachments.Add($"{label}{boneText}y{height:0}");
            }

            var resolved = new List<string>();
            foreach (string request in Requests)
            {
                string hit = AttachmentPoints.Describe(root, request);
                resolved.Add(hit.Length > 0 ? $"{request}→{hit}" : $"{request}→缺");
            }

            (int tracks, int broken, string sample) = CheckAnimationBinding(root, skeleton);
            if (broken > 0) failed++;

            GD.Print($"[ATTACH] {model}  骨骼 {skeleton?.GetBoneCount() ?? 0}  挂点 {attachments.Count}");
            GD.Print($"         {string.Join("  ", attachments)}");
            GD.Print($"         查询 {string.Join("  ", resolved)}");
            GD.Print($"         动画轨道 {tracks} 条，未绑定骨骼 {broken} 条{(sample.Length > 0 ? "  例 " + sample : "")}");
            root.Free();
        }

        GD.Print($"[ATTACH] 模型 {Models.Length} 个，异常 {failed} 个");
        return failed == 0 ? 0 : 1;
    }

    private static (int Tracks, int Broken, string Sample) CheckAnimationBinding(Node3D root, Skeleton3D skeleton)
    {
        if (skeleton == null) return (0, 0, "");
        var player = root.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (player == null)
        {
            GD.Print("         缺 AnimationPlayer 节点");
            return (0, 0, "");
        }

        string[] names = player.GetAnimationList();
        if (names.Length == 0)
        {
            GD.Print($"         动画库为空（libraries {player.GetAnimationLibraryList().Count} 个，autoplay {player.Autoplay}）");
            return (0, 0, "");
        }

        int tracks = 0;
        int broken = 0;
        string sample = "";
        foreach (string name in names)
        {
            Animation animation = player.GetAnimation(name);
            if (animation == null) continue;
            for (int index = 0; index < animation.GetTrackCount(); index++)
            {
                string path = animation.TrackGetPath(index).ToString();
                int colon = path.LastIndexOf(':');
                if (colon < 0) continue;
                if (!path[..colon].EndsWith("Skeleton3D", StringComparison.Ordinal)) continue;

                tracks++;
                string boneName = path[(colon + 1)..];
                if (skeleton.FindBone(boneName) >= 0) continue;
                broken++;
                if (sample.Length == 0) sample = $"{name} {path}";
            }
        }
        return (tracks, broken, sample);
    }
}
