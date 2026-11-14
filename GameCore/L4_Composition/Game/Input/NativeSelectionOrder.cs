using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 选择面板、框选与 Tab 共用的单位先后。客户端取自原生 674250：类型权重(prio)主导，
/// 低位是背包占用这种小量；幻象整体压到最末。
/// </summary>
internal static class NativeSelectionOrder
{
    internal static float Score(Unit unit)
    {
        int low = unit.ItemCount;
        if (PlayerSelectionController.CommandHeroLike(unit))
            low = (unit.World?.HeroSelectionAge(unit) ?? 0) + Unit.InventorySize + 1;
        float score = unit.SelectionPriority * 40000f + low;
        return unit.IsIllusion ? score - 100000f : score;
    }

    internal static bool Before(Unit candidate, Unit existing)
    {
        float left = Score(candidate), right = Score(existing);
        if (left != right) return left > right;
        // 同分英雄保持插入顺序；普通单位按 rawcode、再按实例号，保证顺序稳定。
        if (PlayerSelectionController.CommandHeroLike(candidate)) return false;
        int type = StringComparer.Ordinal.Compare(candidate.Type?.Id, existing.Type?.Id);
        return type < 0 || (type == 0 && candidate.SimulationId < existing.SimulationId);
    }

    internal static void Sort(List<Unit> units)
    {
        if (units == null) return;
        for (int i = 1; i < units.Count; i++)
        {
            Unit unit = units[i];
            int j = i;
            while (j > 0 && Before(unit, units[j - 1])) { units[j] = units[j - 1]; j--; }
            units[j] = unit;
        }
    }
}
