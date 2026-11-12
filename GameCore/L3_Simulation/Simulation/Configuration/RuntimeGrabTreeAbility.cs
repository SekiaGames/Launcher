using Godot;
using System;

namespace OpenWC3.Game;

/// <summary>
/// 战棍（Agra）：让山岭巨人拔一棵树当武器——攻击变成攻城型、射程 +DataE，持续 DataE 次攻击。
/// 点树施放（对标客户端 RuntimeGrabTreeAbility 的可破坏物目标）。
/// </summary>
public sealed class RuntimeGrabTreeAbility : Ability
{
    private const float ReachPadding = 160f;
    private const float TreePickRadius = 96f;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        float reach = CastRange + ReachPadding;
        if (FlatDistance(Owner.Position, point) > reach) return false;
        return world.TakeNearestTree(point, TreePickRadius) != null;
    }

    public float Charges => Mathf.Max(1f, Data('E'));
    public float RangeBonus => Data('E');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        if (Owner.FindBuff<RuntimeGrabTreeBuff>() != null) return false;
        if (Owner.GrabbedTree) return false;

        TreeState tree = world.TakeNearestTree(point, TreePickRadius)
            ?? world.TakeNearestTree(Owner.Position, CastRange + ReachPadding);
        if (tree == null) return false;

        world.ConsumeTree(tree);
        world.ApplyBuff(Owner, new RuntimeGrabTreeBuff
        {
            Charges = Mathf.RoundToInt(Charges),
            RangeBonus = RangeBonus,
        }, 120f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 700f)) return false;
        TreeState tree = world.TakeNearestTree(Owner.Position, CastRange + ReachPadding);
        if (tree == null) return false;
        return world.TryCast(Owner, this, null, tree.Position);
    }
}
