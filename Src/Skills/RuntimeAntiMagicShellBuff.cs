using Godot;

namespace Launcher;

/// <summary>反魔法外壳的载体：期间魔法免疫，到期摘掉。</summary>
public sealed class AntiMagicShellBuff : Buff
{
    public override bool IsHarmful => false;

    public override void OnApply(World world)
    {
        Owner?.State.Add(StateMod.MagicImmune, Remaining);
    }

    public override void OnRemove(World world)
    {
        Owner?.State.RemoveAll(StateMod.MagicImmune);
    }
}
