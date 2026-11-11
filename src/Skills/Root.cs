using Godot;
using System;

namespace Launcher;

/// <summary>
/// 扎根 / 拔根（Aro1 古树、Aro2 远古守护者）。对标客户端 RuntimeRootAbility：
/// 扎根时是建筑（能生产、占格子、护甲类型用单位类型自带的 Fort），
/// 拔根后变成能走的单位（护甲类型换 DataD、放开占地、射程内可近战），再按一次原地扎根。
/// 原版扎根要点一个落点并先走过去；这里退化成原地开关，移动交给普通指令。
/// </summary>
public sealed class Root : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public bool Rooted => Owner?.IsBuilding == true;

    /// <summary>DataD：拔根后的护甲类型（0 Small / 1 Medium / 2 Large …）。</summary>
    public DefenseType MobileDefense => (DefenseType)Mathf.Clamp(Mathf.RoundToInt(Data('D')), 0, 7);

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner?.Type == null || !Owner.Alive) return false;
        if (Owner.State.Stunned || Owner.IsChanneling || Owner.IsUnderConstruction) return false;

        if (Rooted)
        {
            Uproot(world);
            return true;
        }

        if (!world.CanStandAt(Owner.Position, Owner.Radius)) return false;
        Plant(world);
        return true;
    }

    private void Uproot(World world)
    {
        float footprint = Owner.Radius;
        Owner.CancelAttack();
        Owner.Target = null;
        Owner.MoveTarget = null;
        Owner.Path = null;
        Owner.IsBuilding = false;
        Owner.DefenseTypeOverride = MobileDefense;
        world.ReleaseFootprint(Owner, footprint);
        Owner.View?.Play("Stand");
    }

    private void Plant(World world)
    {
        Owner.CancelAttack();
        Owner.Target = null;
        Owner.MoveTarget = null;
        Owner.Path = null;
        Owner.IsBuilding = true;
        Owner.DefenseTypeOverride = null;
        world.OccupyFootprint(Owner);
        Owner.View?.Play("Stand");
    }

    public override bool TryAutoCast(World world) => false;
}