using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class MapWorldBuilder
{
    internal static float GroundUnitsAtCorner(TerrainData terrain, int column, int row)
        => CornerWorld(terrain, column, row) / 128f;
}

public sealed class ShorelineWaveRenderer
{
    readonly Node3D _root;

    public int InstanceCount { get; private set; }

    public ShorelineWaveRenderer(Node3D parent, TerrainData terrain, War3WaterConfig water)
    {
        if (parent == null || terrain == null || water == null) return;
        if (string.IsNullOrEmpty(water.ShoreDirectory)) return;
        _root = new Node3D { Name = "ShorelineWaves" };
        parent.AddChild(_root);
        var scenes = new Dictionary<string, PackedScene>(StringComparer.OrdinalIgnoreCase);
        var rareAnimations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int missing = 0;
        int rareWaves = 0;

        for (int row = 0; row < terrain.Height - 1; row++)
        {
            for (int column = 0; column < terrain.Width - 1; column++)
            {
                TerrainData.Corner a = terrain.Corners[row * terrain.Width + column];
                TerrainData.Corner b = terrain.Corners[row * terrain.Width + column + 1];
                TerrainData.Corner c = terrain.Corners[(row + 1) * terrain.Width + column + 1];
                TerrainData.Corner d = terrain.Corners[(row + 1) * terrain.Width + column];
                if (!a.HasWater && !b.HasWater && !c.HasWater && !d.HasWater) continue;
                bool cliff = a.LayerHeight != b.LayerHeight || a.LayerHeight != c.LayerHeight
                    || a.LayerHeight != d.LayerHeight;
                if (cliff ? !water.ShowCliffWaves : !water.ShowRollingWaves) continue;

                int ad = DepthReachesWaves(terrain, water, column, row, a) ? 1 : 0;
                int bd = DepthReachesWaves(terrain, water, column + 1, row, b) ? 1 : 0;
                int cd = DepthReachesWaves(terrain, water, column + 1, row + 1, c) ? 1 : 0;
                int dd = DepthReachesWaves(terrain, water, column, row + 1, d) ? 1 : 0;
                int count = ad + bd + cd + dd;
                string file;
                int variations;
                float rotation;
                if (count == 1)
                {
                    file = water.InsideCornerFile;
                    variations = water.InsideCornerVariations;
                    rotation = CornerRotation(ad, bd, cd) - 3f * MathF.PI / 4f;
                }
                else if (count == 2)
                {
                    rotation = StraightRotation(ad, bd, cd, dd);
                    if (float.IsNaN(rotation)) continue;
                    file = water.StraightFile;
                    variations = water.StraightVariations;
                }
                else if (count == 3)
                {
                    file = water.OutsideCornerFile;
                    variations = water.OutsideCornerVariations;
                    rotation = CornerRotation(1 ^ ad, 1 ^ bd, 1 ^ cd) + 5f * MathF.PI / 4f;
                }
                else continue;
                if (string.IsNullOrEmpty(file)) continue;

                uint seed = Hash((uint)(column * 73856093) ^ (uint)(row * 19349663));
                int variation = variations <= 1 ? 0 : (int)(seed % (uint)variations);
                string relative = $"{water.ShoreDirectory}/{file}/{file}{variation}";
                if (!scenes.TryGetValue(relative, out PackedScene scene))
                {
                    string path = ModelLibrary.Find(relative);
                    scene = path == null ? null : GD.Load<PackedScene>(path);
                    scenes[relative] = scene;
                }
                if (scene == null)
                {
                    missing++;
                    continue;
                }

                Node3D node = scene.Instantiate<Node3D>();
                models.Add(relative);
                float mapX = column * 128f + 64f;
                float mapY = row * 128f + 64f;
                float waterHeight = (a.WaterWorld + b.WaterWorld + c.WaterWorld + d.WaterWorld)
                    * 0.25f + water.HeightOffset;
                node.Position = new Vector3(-mapX, waterHeight * 128f + 1f, mapY);
                node.Rotation = new Vector3(0f, MathF.PI + rotation, 0f);
                node.Name = $"{file}{variation}_{column}_{row}";
                ModelLibrary.ApplyMissingMaterials(node, ModelLibrary.Find(relative));
                _root.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
                InstanceCount++;
                if (IsRareStand(seed) && TryPlayRareWave(node, relative, seed, rareAnimations))
                {
                    rareWaves++;
                }
            }
        }

        if (InstanceCount == 0)
        {
            _root.QueueFree();
            _root = null;
            return;
        }
        GD.Print($"[WATER] 岸线浪 {InstanceCount} 个（{models.Count} 种模型，缺失 {missing}，偶发大浪 {rareWaves}）");
    }

    static bool IsRareStand(uint seed)
    {
        return Random01(Hash(seed + 17u)) < 0.07f;
    }

    static float Random01(uint value)
    {
        return (value & 0x00ffffffu) / 16777216f;
    }

    static bool TryPlayRareWave(Node3D node, string model, uint seed, Dictionary<string, string> cache)
    {
        AnimationPlayer player = node.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (player == null) return false;

        if (!cache.TryGetValue(model, out string animation))
        {
            animation = RareWaveAnimation(player);
            cache[model] = animation;
        }
        if (string.IsNullOrEmpty(animation) || !player.HasAnimation(animation)) return false;

        AnimationTree tree = node.GetNodeOrNull<AnimationTree>("AnimationTree");
        if (tree != null) tree.Active = false;
        player.Autoplay = "";
        player.Play(animation);
        Animation clip = player.GetAnimation(animation);
        if (clip != null && clip.Length > 0f)
        {
            player.Seek(Random01(Hash(seed + 0x51ed270bu)) * clip.Length, true);
        }
        return true;
    }

    static string RareWaveAnimation(AnimationPlayer player)
    {
        foreach (string name in player.GetAnimationList())
        {
            Animation animation = player.GetAnimation(name);
            if (animation == null) continue;
            for (int track = 0; track < animation.GetTrackCount(); track++)
            {
                if (animation.TrackGetType(track) != Animation.TrackType.Value) continue;
                if (!animation.TrackGetPath(track).ToString().EndsWith(":visible", StringComparison.Ordinal)) continue;
                if (TrackShowsAnyKey(animation, track)) return name;
            }
        }
        return null;
    }

    static bool TrackShowsAnyKey(Animation animation, int track)
    {
        int keys = animation.TrackGetKeyCount(track);
        for (int key = 0; key < keys; key++)
        {
            if (animation.TrackGetKeyValue(track, key).AsBool()) return true;
        }
        return false;
    }

    static bool DepthReachesWaves(TerrainData terrain, War3WaterConfig water,
        int column, int row, TerrainData.Corner corner)
    {
        float waterHeight = corner.WaterWorld + water.HeightOffset;
        return corner.HasWater
            && waterHeight - MapWorldBuilder.GroundUnitsAtCorner(terrain, column, row)
                > water.WavesDepth;
    }

    static float CornerRotation(int a, int b, int c)
    {
        if (a != 0) return -3f * MathF.PI / 4f;
        if (b != 0) return -MathF.PI / 4f;
        if (c != 0) return MathF.PI / 4f;
        return 3f * MathF.PI / 4f;
    }

    static float StraightRotation(int a, int b, int c, int d)
    {
        if (a != 0 && b != 0) return -MathF.PI / 2f;
        if (b != 0 && c != 0) return 0f;
        if (c != 0 && d != 0) return MathF.PI / 2f;
        if (a != 0 && d != 0) return MathF.PI;
        return float.NaN;
    }

    static uint Hash(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        return value ^ (value >> 16);
    }
}