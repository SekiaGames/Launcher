using Godot;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 编队阵型。对标客户端 Core/Pathing/Group/MovementFormationOffsets.cs：
/// 物编 `formation` 分层（0 = 近战前排，2 = 远程/法师后排），层内按成员当前的左右顺序保留，
/// 沿移动方向排成若干排，前排贴着落点、后排往后铺。
/// 间距沿用客户端的格数常量（1 格 = 32）：成员 2 格、行距 2.5 格、层间 5.5 格。
/// </summary>
public static class GroupFormation
{
    public const float CellSize = 32f;
    public const float MemberGap = 2f * CellSize;
    public const float RowGap = 2.5f * CellSize;
    public const float RankGap = 5.5f * CellSize;

    public static Dictionary<Unit, Vector3> Layout(IReadOnlyList<Unit> members, Vector3 destination, Vector3 forward)
    {
        var slots = new Dictionary<Unit, Vector3>();
        if (members == null || members.Count == 0) return slots;

        Vector3 direction = new(forward.X, 0f, forward.Z);
        if (direction.LengthSquared() < 0.01f) direction = new Vector3(0f, 0f, 1f);
        direction = direction.Normalized();
        Vector3 lateral = new(-direction.Z, 0f, direction.X);

        var ranks = new SortedDictionary<int, List<Unit>>();
        foreach (Unit unit in members)
        {
            if (unit == null) continue;
            int rank = Mathf.Clamp(unit.FormationRank, 0, 15);
            if (!ranks.TryGetValue(rank, out List<Unit> bucket))
            {
                bucket = new List<Unit>();
                ranks[rank] = bucket;
            }
            bucket.Add(unit);
        }

        float depth = 0f;
        foreach (KeyValuePair<int, List<Unit>> pair in ranks)
        {
            List<Unit> bucket = pair.Value;
            bucket.Sort((first, second) => LateralOf(first, lateral).CompareTo(LateralOf(second, lateral)));

            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(bucket.Count)));
            int rows = Mathf.CeilToInt((float)bucket.Count / columns);

            for (int index = 0; index < bucket.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                int inRow = Mathf.Min(columns, bucket.Count - row * columns);
                float lateralOffset = (column - (inRow - 1) * 0.5f) * MemberGap;
                float back = depth + row * RowGap;
                slots[bucket[index]] = destination - direction * back + lateral * lateralOffset;
            }

            depth += rows * RowGap + RankGap;
        }
        return slots;
    }

    private static float LateralOf(Unit unit, Vector3 lateral) =>
        unit.Position.X * lateral.X + unit.Position.Z * lateral.Z;
}