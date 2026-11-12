using Godot;

namespace Launcher;

/// <summary>
/// 载具货舱（Sch3 运输船/飞艇、Sch5 战船、Sch2 绞肉车、Advc 吞噬、Aenc 金矿、Achd 尸体舱、Slo2 缠绕金矿）。
/// 对标客户端 RuntimeCargoHoldAbility：只负责把容量写进单位、并表明「装人不影响开火」。
/// 真正的装卸由 <see cref="LoadCargoOrder"/> / <see cref="DropCargoOrder"/> 与 <see cref="World"/> 的货舱接口完成。
/// </summary>
public sealed class TransportHold : Ability
{
    public override bool IsPassive => true;

    public int Capacity => Mathf.Max(1, (int)Data('A'));

    public override void OnLearned()
    {
        if (Owner == null) return;
        Owner.CargoCapacity = Mathf.Max(Owner.CargoCapacity, Capacity);
    }
}

/// <summary>装载（Aloa 运输船 / Slo3 战船）：点一个友军把它装进来。</summary>
public sealed class LoadCargoOrder : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.CanEnterCargo(Owner, target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null) return false;
        if (!world.CanEnterCargo(Owner, target)) return false;
        return world.LoadCargo(Owner, target);
    }

    public override bool TryAutoCast(World world) => false;
}

/// <summary>卸载（Adro 全部卸下 / Sdro 战船卸下 / Adri 瞬时卸下）：把人摆回船周围。</summary>
public sealed class DropCargoOrder : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.UnloadAllCargo(Owner);
    }

    public override bool TryAutoCast(World world) => false;
}