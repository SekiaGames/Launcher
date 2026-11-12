namespace OpenWC3;

/// <summary>技能订单的目标形态。Any 用于尚未专门实现目标规则的旧技能。</summary>
public enum AbilityTargetMode
{
    Any = -1,
    None = 0,
    Unit = 1,
    Point = 2,
    UnitOrPoint = 3,
    Destructable = 4,
}
