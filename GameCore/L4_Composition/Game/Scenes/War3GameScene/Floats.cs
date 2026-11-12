using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed partial class War3GameScene
{
    private sealed class FloatingLabel
    {
        public Label3D Node;
        public float Remaining;
        public float Total;
        public Vector3 Origin;
    }

    private readonly List<FloatingLabel> _floatingLabels = new();
    private const int MaxFloatingLabels = 72;

    /// <summary>普通伤害数字（客户端默认关；暴击始终显示）。</summary>
    public bool ShowDamageNumbers;

    private static readonly Color CriticalColor = new(1f, 0.32f, 0.22f);
    private static readonly Color OrdinaryDamageColor = new(1f, 1f, 1f, 0.92f);
    private static readonly Color GoldColor = new(1f, 0.86f, 0f);
    private static readonly Color LumberColor = new(0f, 0.78f, 0.31f);
    private static readonly Color BountyColor = new(1f, 0.86f, 0f);
    private static readonly Color ManaBurnColor = new(0.32f, 0.32f, 1f);
    private static readonly Color MissColor = new(0.82f, 0.82f, 0.82f, 0.95f);

    private void OnDamageApplied(DamageData info)
    {
        if (info?.Target == null || info.Amount < 1f || info.Reflected) return;
        if (!IsAudible(info.Target.Position)) return;

        if (info.IsCritical && info.IsAttack)
        {
            ShowUnitFloat(info.Target, $"{(int)info.Amount}!", CriticalColor, 30f, 1.6f);
            return;
        }

        if (!ShowDamageNumbers || !info.IsAttack) return;
        ShowUnitFloat(info.Target, $"{(int)info.Amount}", OrdinaryDamageColor, 19f, 0.95f);
    }

    private void ShowMissFloat(Unit attacker)
    {
        if (attacker == null || !attacker.Alive || !IsAudible(attacker.Position)) return;
        ShowUnitFloat(attacker, "Miss", MissColor, 20f, 0.9f);
    }

    private void ShowManaBurnFloat(Unit target, float drained)
    {
        if (target == null || drained <= 0f || !IsAudible(target.Position)) return;
        ShowUnitFloat(target, $"-{(int)drained}", ManaBurnColor, 24f, 1.4f);
    }

    private void ShowResourceFloat(Unit anchor, int amount, bool lumber, bool bounty)
    {
        if (anchor == null || amount <= 0) return;
        Color color = bounty ? BountyColor : lumber ? LumberColor : GoldColor;
        ShowUnitFloat(anchor, $"+{amount}", color, bounty ? 24f : 20f, bounty ? 1.6f : 1.1f);
    }

    private void ShowUnitFloat(Unit anchor, string text, Color color, float size, float seconds)
    {
        if (anchor == null || string.IsNullOrEmpty(text)) return;
        float height = anchor.View?.Height ?? 90f;
        ShowFloat(anchor.Position + new Vector3(0f, height + 16f, 0f), text, color, size, seconds);
    }

    private void ShowFloat(Vector3 worldPoint, string text, Color color, float size, float seconds)
    {
        while (_floatingLabels.Count >= MaxFloatingLabels)
        {
            FloatingLabel oldest = _floatingLabels[0];
            _floatingLabels.RemoveAt(0);
            oldest.Node?.QueueFree();
        }

        var label = new Label3D
        {
            Text = text,
            FontSize = 48,
            PixelSize = Mathf.Max(0.004f, size / 48f),
            Modulate = color,
            OutlineSize = 12,
            OutlineModulate = new Color(0f, 0f, 0f, color.A),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Shaded = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderPriority = 12,
            Position = worldPoint,
        };
        AddChild(label);

        _floatingLabels.Add(new FloatingLabel
        {
            Node = label,
            Remaining = Mathf.Max(0.3f, seconds),
            Total = Mathf.Max(0.3f, seconds),
            Origin = worldPoint,
        });
    }

    private void TickFloatingLabels(float delta)
    {
        if (_floatingLabels.Count == 0) return;

        for (int index = _floatingLabels.Count - 1; index >= 0; index--)
        {
            FloatingLabel label = _floatingLabels[index];
            if (label.Node == null || !GodotObject.IsInstanceValid(label.Node))
            {
                _floatingLabels.RemoveAt(index);
                continue;
            }

            label.Remaining -= delta;
            if (label.Remaining <= 0f)
            {
                label.Node.QueueFree();
                _floatingLabels.RemoveAt(index);
                continue;
            }

            float age = label.Total - label.Remaining;
            label.Node.Position = label.Origin + new Vector3(0f, age * 34f, 0f);

            float fadeFrom = label.Total * 0.55f;
            Color color = label.Node.Modulate;
            color.A = age <= fadeFrom ? 1f : Mathf.Clamp(1f - (age - fadeFrom) / Mathf.Max(0.01f, label.Total - fadeFrom), 0f, 1f);
            label.Node.Modulate = color;
            Color outline = label.Node.OutlineModulate;
            outline.A = color.A;
            label.Node.OutlineModulate = outline;
        }
    }
}