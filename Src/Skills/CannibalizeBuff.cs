using Godot;

namespace Launcher;

/// <summary>吞尸期间的持续回血。</summary>
public sealed class CannibalizeBuff : Buff
{
    public float HealPerSecond;

    public override bool OnTick(World world, float delta)
    {
        if (Owner != null && Owner.Alive) Owner.Heal(HealPerSecond * delta);
        return base.OnTick(world, delta);
    }
}
