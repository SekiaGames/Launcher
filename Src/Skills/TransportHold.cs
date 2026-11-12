using Godot;

namespace OpenWC3;

/// <summary>
/// 载具货舱（Sch3 运输船/飞艇、Sch5 战船、Sch2 绞肉车、Advc 吞噬、Aenc 金矿、Achd 尸体舱、Slo2 缠绕金矿）。
/// 对标客户端 RuntimeCargoHoldAbility：只负责把容量写进单位、并表明「装人不影响开火」。
/// 真正的装卸由 <see cref="RuntimeCargoLoadAbility"/> / <see cref="RuntimeCargoDropAbility"/> 与 <see cref="World"/> 的货舱接口完成。
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
