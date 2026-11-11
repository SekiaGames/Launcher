using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed partial class War3GameScene
{
    private sealed class SceneEffect
    {
        public Node3D Node;
        public float Remaining;
    }

    private readonly List<SceneEffect> _sceneEffects = new();
    private const int MaxSceneEffects = 60;

    private void PlayAbilityEffects(Unit caster, Ability ability, Unit target)
    {
        if (caster == null || ability?.Type == null) return;
        if (!EffectArtTable.TryResolve(ability.Id, out string resPath, out string mount, out bool stick))
        {
            if (_effectMissLog <= 0) return;
            _effectMissLog--;
            GD.Print($"[FX] {ability.Id} 无匹配特效");
            return;
        }

        Unit anchor = target != null && target.Alive ? target : caster;

        float seconds = 1.4f;
        if (stick)
        {
            float duration = ability.Duration > 0f ? ability.Duration : ability.HeroDuration;
            seconds = duration > 0f ? Mathf.Min(duration, 120f) + 0.5f : 3f;
        }

        string effectName = "Effect_" + System.IO.Path.GetFileNameWithoutExtension(resPath);
        if (stick && RefreshStickEffect(anchor, effectName, seconds)) return;

        Node3D node = CreateEffectNode(anchor, resPath, mount);
        if (node == null) return;

        _sceneEffects.Add(new SceneEffect { Node = node, Remaining = seconds });
        if (_effectLogBudget <= 0) return;
        _effectLogBudget--;
        GD.Print($"[FX] {ability.Id} {ability.Name} → {System.IO.Path.GetFileName(resPath)}"
            + $"（挂在 {anchor.Type?.Name} 的 {mount}，{seconds:0.0}s）");
    }

    private bool RefreshStickEffect(Unit anchor, string effectName, float seconds)
    {
        Node parent = anchor?.View?.Root;
        if (parent == null) return false;

        foreach (SceneEffect effect in _sceneEffects)
        {
            if (effect.Node == null || !IsInstanceValid(effect.Node)) continue;
            if (!string.Equals(effect.Node.Name.ToString(), effectName, StringComparison.Ordinal)) continue;
            if (!ReferenceEquals(effect.Node.GetParent(), parent)) continue;
            effect.Remaining = seconds;
            return true;
        }
        return false;
    }

    private Node3D CreateEffectNode(Unit anchor, string resPath, string mount)
    {
        if (anchor?.View?.Root == null || string.IsNullOrEmpty(resPath)) return null;
        if (_sceneEffects.Count >= MaxSceneEffects) return null;

        PackedScene scene = Assets.LoadScene(resPath);
        if (scene == null) return null;

        Node3D node = scene.Instantiate<Node3D>();
        if (node == null) return null;

        MapWorldBuilder.ApplyUnitTextureFilter(node);
        node.Position = MountOffset(anchor, mount);
        node.Name = "Effect_" + System.IO.Path.GetFileNameWithoutExtension(resPath);
        anchor.View.Root.AddChild(node);

        PlayFirstAnimation(node);
        return node;
    }

    private static Vector3 MountOffset(Unit anchor, string mount)
    {
        float height = anchor.View != null ? anchor.View.Height : 100f;
        return mount switch
        {
            "overhead" => new Vector3(0f, height * 0.85f, 0f),
            "target" => new Vector3(0f, height * 0.55f, 0f),
            _ => Vector3.Zero,
        };
    }

    private static void PlayFirstAnimation(Node3D node)
    {
        var player = node.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (player == null) return;

        string[] names = player.GetAnimationList();
        if (names == null || names.Length == 0) return;
        if (!string.IsNullOrEmpty(player.Autoplay) && player.HasAnimation(player.Autoplay)) return;

        foreach (string name in names)
        {
            if (!name.Contains("Birth", StringComparison.OrdinalIgnoreCase)) continue;
            player.Play(name);
            return;
        }
        player.Play(names[0]);
    }

    private void TickSceneEffects(float delta)
    {
        for (int index = _sceneEffects.Count - 1; index >= 0; index--)
        {
            SceneEffect effect = _sceneEffects[index];
            if (effect.Node == null || !IsInstanceValid(effect.Node))
            {
                _sceneEffects.RemoveAt(index);
                continue;
            }

            effect.Remaining -= delta;
            if (effect.Remaining > 0f) continue;

            effect.Node.QueueFree();
            _sceneEffects.RemoveAt(index);
        }
    }

    private static int _effectLogBudget = 12;
    private static int _effectMissLog = 6;

    public int ActiveEffectCount => _sceneEffects.Count;

    public string DescribeEffect(string code)
    {
        if (!EffectArtTable.TryResolve(code, out string path, out string mount, out bool stick))
        {
            return $"{code} 无匹配特效";
        }
        return $"{code} → {path}（挂点 {mount}{(stick ? "，持续" : "")}）";
    }
}