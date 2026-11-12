using Godot;

namespace Launcher;

/// <summary>灵魂保存的保留状态：到期撤状态 → 换阵营 → 直接死亡。</summary>
public sealed class SoulPreservationBuff : Buff
{
    public Unit Taker;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        Owner.State.Add(StateMod.Invulnerable);
        Owner.State.Add(StateMod.MagicImmune);
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;

        Owner.State.RemoveAll(StateMod.Invulnerable);
        Owner.State.RemoveAll(StateMod.MagicImmune);
        if (!Owner.Alive) return;

        Owner.TransferTo(Taker?.Player ?? Owner.Player);
        world?.KillUnit(Owner, Taker);
    }
}
