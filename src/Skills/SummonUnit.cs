using Godot;

namespace Launcher;

/// <summary>
/// 召唤类技能（水元素 AHwe / 树人 AEst / 幽灵狼 AOsf / 熔岩怪 ANsg）。
/// 单位 id 取物编的 UnitID{level} 列，个数取 DataA，存在时间取 Dur。
/// 建单位本身要靠 <see cref="World.SummonHandler"/>（模拟层没有场景）。
/// </summary>
public sealed class SummonUnit : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;

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
}

/// <summary>
/// 复活 AHre：把半径 Area 内最多 DataA 个友军尸体满血复活（物编 DataA=6 Area=900）。
/// 只复活非英雄（英雄走祭坛流程），与客户端 Resurrection 的口径一致。
/// </summary>
public sealed class Resurrection : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;

    public int MaxTargets => Mathf.Max(1, (int)Data('A'));
    public float Radius => Area > 0f ? Area : 900f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        int revived = 0;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || other.IsHero) continue;
            if (!other.IsCorpse) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (Owner.DistanceTo(other) > Radius) continue;

            other.ReviveFromCorpse(other.Position);
            revived++;
            if (revived >= MaxTargets) break;
        }
        return revived > 0;
    }
}
