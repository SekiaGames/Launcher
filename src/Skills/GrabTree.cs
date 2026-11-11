using Godot;
using System;

namespace Launcher;

/// <summary>
/// 战棍（Agra）：让山岭巨人拔一棵树当武器——攻击变成攻城型、射程 +DataE，持续 DataE 次攻击。
/// 原版是「点树」，这里没有树的拾取通道，所以改成「抓起半径内最近的一棵」。
/// </summary>
public sealed class GrabTree : Ability
{
    private const float ReachPadding = 160f;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public float Charges => Mathf.Max(1f, Data('E'));
    public float RangeBonus => Data('E');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        if (Owner.FindBuff<GrabTreeBuff>() != null) return false;
        if (Owner.GrabbedTree) return false;

        TreeState tree = world.TakeNearestTree(Owner.Position, CastRange + ReachPadding);
        if (tree == null) return false;

        world.ConsumeTree(tree);
        world.ApplyBuff(Owner, new GrabTreeBuff
        {
            Charges = Mathf.RoundToInt(Charges),
            RangeBonus = RangeBonus,
        }, 120f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 700f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

/// <summary>战棍增益：攻城攻击 + 射程加成，挥满次数就恢复原口径。</summary>
public sealed class GrabTreeBuff : Buff, IAttackLaunchBuff
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