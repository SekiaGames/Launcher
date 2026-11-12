using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 客户端里同样是「内部能力对象」的码：采集、返还、商店买卖、集结、金矿、腐地标记、视觉标记。
/// 真正的行为在本工程里由订单（采金/伐木/返金）、商店 UI、集结点与 World 的世界系统承担，
/// 这里只提供一个被动对象，让 abilList 里的码有实现、命令卡也不会多出按不动的按钮。
/// </summary>
public sealed class RuntimeHarvestAbility : Ability
{
    public override bool IsPassive => true;
    public override bool OnCast(World world, Unit target, Vector3 point) => false;
}
