using Godot;
using System;

namespace OpenWC3;

public sealed class TreeState
{
    public Node3D Node;
    public AnimationPlayer Animator;
    public Vector3 Position;
    public string Id;
    public float Hp = 50f;
    public bool Alive = true;

    private const string DeathSequence = "death";
    private string _playingDeath;

    public void Chop(float amount)
    {
        if (!Alive) return;
        Hp -= amount;
        if (Hp <= 0f) EnterDeath();
    }

    public void EnterDeath()
    {
        if (!Alive) return;
        Alive = false;
        string clip = ResolveDeathSequence(Animator);
        if (clip == null) return;
        Animator.Play(clip);
        Animator.AnimationFinished += OnAnimationFinished;
        _playingDeath = clip;
    }

    public void HoldDeathPose()
    {
        Alive = false;
        HoldDeathPose(Animator);
    }

    private void OnAnimationFinished(StringName animation)
    {
        if (!animation.ToString().Equals(_playingDeath, StringComparison.Ordinal)) return;
        Animator.AnimationFinished -= OnAnimationFinished;
        FreezeDeathPose(Animator, _playingDeath);
    }

    public static AnimationPlayer ResolveAnimator(Node node)
    {
        if (node.FindChild("AnimationTree", true, false) is AnimationTree tree) tree.Active = false;
        return node.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
    }

    public static void HoldDeathPose(AnimationPlayer animator)
    {
        string clip = ResolveDeathSequence(animator);
        if (clip == null) return;
        animator.Play(clip);
        FreezeDeathPose(animator, clip);
    }

    private static string ResolveDeathSequence(AnimationPlayer animator)
    {
        if (animator == null) return null;
        foreach (string name in animator.GetAnimationList())
        {
            if (name.Equals(DeathSequence, StringComparison.OrdinalIgnoreCase)) return name;
        }
        return null;
    }

    private static void FreezeDeathPose(AnimationPlayer animator, string clip)
    {
        Animation animation = animator.GetAnimation(clip);
        if (animation == null) return;
        animator.Seek(animation.Length, true);
        animator.Pause();
    }
}