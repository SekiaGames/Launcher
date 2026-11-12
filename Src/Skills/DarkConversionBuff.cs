using Godot;

namespace OpenWC3;

/// <summary>黑暗转换：先沉睡，到期生成替代单位再让原单位消失。</summary>
public sealed class DarkConversionBuff : Buff
{
    public string ResultUnitId;
    public new Unit Caster;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        Owner.State.Add(StateMod.Pause);
        Owner.CancelAttack();
        Owner.MoveTarget = null;
        Owner.Path = null;
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;

        Owner.State.RemoveAll(StateMod.Pause);
        if (world == null || !Owner.Alive) return;

        Vector3 where = Owner.Position;
        world.Summon(Caster ?? Owner, ResultUnitId, where, 0f);
        world.KillUnit(Owner, Caster);
    }
}
