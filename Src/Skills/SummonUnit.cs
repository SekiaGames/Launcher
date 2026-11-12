using Godot;

namespace OpenWC3;

/// <summary>
/// 召唤类技能（水元素 AHwe / 树人 AEfn / 侦察猫头鹰 AEst / 幽灵狼 AOsf / 熊 ANsg）。
/// 单位 id 取物编的 UnitID{level} 列，个数取 DataA，存在时间取 Dur。
/// 建单位本身要靠 <see cref="World.SummonHandler"/>（模拟层没有场景）。
/// </summary>
public sealed class SummonUnit : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int Count => Mathf.Max(1, (int)Data('A'));
    public string SummonId => Type?.SummonAt(Level) ?? "";
    public float Lifetime => Duration > 0f ? Duration : 60f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world?.SummonHandler == null || Owner == null) return false;

        string unitId = SummonId;
        if (string.IsNullOrEmpty(unitId)) return false;

        Vector3 center = point == default ? Owner.Position : point;
        int created = 0;
        for (int index = 0; index < Count; index++)
        {
            float angle = Mathf.Tau * index / Count;
            var offset = new Vector3(Mathf.Cos(angle) * 80f, 0f, Mathf.Sin(angle) * 80f);
            if (world.Summon(Owner, unitId, center + offset, Lifetime) != null) created++;
        }
        return created > 0;
    }

    /// <summary>AI：身边有敌人就召，召唤物会被安排当前排（Ai.TickAiFocus）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 1200f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
