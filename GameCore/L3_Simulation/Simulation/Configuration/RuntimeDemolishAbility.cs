using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>粉碎（ANde）：普攻打建筑时按 DataA 概率把伤害乘 DataB 并追加 DataC。</summary>
public sealed class RuntimeDemolishAbility : Ability
{
    public override bool IsPassive => true;

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (world == null || target == null || !target.IsBuilding) return raw;
        if (!world.RollPercent(Data('A'))) return raw;
        return raw * Mathf.Max(0f, Data('B')) + Data('C');
    }
}
