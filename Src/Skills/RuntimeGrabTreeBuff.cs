using Godot;
using System;

namespace OpenWC3;

/// <summary>战棍增益：攻城攻击 + 射程加成，挥满次数就恢复原口径。</summary>
public sealed class RuntimeGrabTreeBuff : Buff, IAttackLaunchBuff
{
    public int Charges;
    public float RangeBonus;

    public override bool IsHarmful => false;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        Owner.AttackTypeOverride = AttackType.Siege;
        Owner.AttackRangeBonus += RangeBonus;
        Owner.GrabbedTree = true;
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;
        Owner.AttackTypeOverride = null;
        Owner.AttackRangeBonus -= RangeBonus;
        Owner.GrabbedTree = false;
    }

    public void OnAttackLaunched(World world, Unit target, ref float raw)
    {
        if (Charges <= 0) return;
        Charges--;
        if (Charges > 0) return;
        Remaining = 0f;
    }
}
