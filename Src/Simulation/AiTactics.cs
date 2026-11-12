using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// AI 战术判定。对标客户端 Captain 的目标优先级（英雄 → 攻城 → 普通）与
/// 口袋阵型选择思路：优先咬英雄/后排，切入时挑敌人稀的那一侧。
/// </summary>
public static class AiTactics
{
    /// <summary>
    /// 威胁分：英雄 4、攻城 3、远程/法师 2、普通 1，再加「残血」小分（最多 0.5）。
    /// 幻象不算英雄（客户端同样用「大写 rawcode + 非幻象」判定）。
    /// </summary>
    public static float ThreatScore(Unit enemy)
    {
        if (enemy == null || enemy.Type == null) return 0f;

        bool siege = enemy.Type.Weapon != null && enemy.Type.Weapon.AttackType == AttackType.Siege;
        float score;
        if (enemy.IsHero && !enemy.IsIllusion) score = 4f;
        else if (siege) score = 3f;
        else if (!enemy.IsBuilding && enemy.Type.FormationRank >= 2) score = 2f;
        else score = 1f;

        float ratio = enemy.MaxHp > 0f ? enemy.Hp / enemy.MaxHp : 1f;
        return score + 0.5f * (1f - Mathf.Clamp(ratio, 0f, 1f));
    }

    public static Vector3 LeadPoint(Unit target, float seconds = 0.45f)
    {
        if (target == null || target.Type == null) return target != null ? target.Position : Vector3.Zero;

        Vector3 destination = target.Position;
        if (target.Path != null && target.PathIndex < target.Path.Count)
        {
            destination = target.Path[target.PathIndex];
        }
        else if (target.MoveTarget.HasValue)
        {
            destination = target.MoveTarget.Value;
        }

        Vector3 direction = destination - target.Position;
        direction.Y = 0f;
        if (direction.LengthSquared() < 1f) return target.Position;

        float lead = Mathf.Clamp(target.Type.Speed * seconds, 0f, 260f);
        return target.Position + direction.Normalized() * lead;
    }

    /// <summary>远程/法师（物编 formation=2）：交战时值得往后拉。</summary>
    public static bool IsBackline(Unit unit)
    {
        return unit?.Type != null && !unit.IsBuilding && unit.Type.FormationRank >= 2;
    }

    /// <summary>
    /// 切入方向：在目标两侧各取一个落点，挑 600 范围内敌人少的那一侧。
    /// 没有侧翼数据时退化成「从原点看向目标」的垂直方向（即目标正侧方）。
    /// </summary>
    public static Vector3 FlankPoint(Unit focus, Vector3 origin, IReadOnlyList<Unit> units,
        Func<int, int, bool> allied, int player, float offset = 420f, Func<Vector3, bool> passable = null)
    {
        if (focus == null) return origin;

        Vector3 axis = focus.Position - origin;
        axis.Y = 0f;
        if (axis.LengthSquared() < 1f) axis = new Vector3(0f, 0f, 1f);
        axis = axis.Normalized();
        Vector3 lateral = new(-axis.Z, 0f, axis.X);

        Vector3 left = focus.Position + lateral * offset;
        Vector3 right = focus.Position - lateral * offset;
        bool leftOk = passable == null || passable(left);
        bool rightOk = passable == null || passable(right);
        if (leftOk || rightOk)
        {
            if (leftOk && rightOk)
            {
                return CountNear(units, allied, player, left, 600f) <= CountNear(units, allied, player, right, 600f)
                    ? left
                    : right;
            }
            return leftOk ? left : right;
        }

        for (int step = 0; step < 6; step++)
        {
            float angle = Mathf.Tau * step / 6f;
            var candidate = new Vector3(
                focus.Position.X + Mathf.Cos(angle) * offset,
                focus.Position.Y,
                focus.Position.Z + Mathf.Sin(angle) * offset);
            if (passable(candidate)) return candidate;
        }
        return focus.Position;
    }

    private static int CountNear(IReadOnlyList<Unit> units, Func<int, int, bool> allied, int player,
        Vector3 point, float radius)
    {
        int count = 0;
        if (units == null) return count;

        foreach (Unit unit in units)
        {
            if (unit == null || !unit.Alive || unit.IsBuilding) continue;
            if (unit.Player == player || (allied != null && allied(unit.Player, player))) continue;
            float dx = unit.Position.X - point.X;
            float dz = unit.Position.Z - point.Z;
            if (dx * dx + dz * dz > radius * radius) continue;
            count++;
        }
        return count;
    }
}