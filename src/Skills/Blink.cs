using Godot;

namespace Launcher;

/// <summary>
/// 闪烁 AHbl。对标客户端 Src/Skills/Blink.cs：
/// DataA = 最远距离、DataB = 最近距离（0 表示贴身也能闪）。
/// 准入只判「落点能站人 + 不短于最近距离」；超距沿方向钳制（客户端实测钳到 0.8×DataA），
/// 落点不可站就沿来路回推，全程不可站则原地不动。
/// </summary>
public sealed class Blink : Ability
{
    /// <summary>客户端 FUN_6f4af640 实测：超距时钳到 0.8×上限，不是上限本身。</summary>
    private const float OvershootClamp = 0.8f;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;

    public float MaximumRange => Data('A') > 0f ? Data('A') : 600f;
    public float MinimumRange => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || world == null) return false;
        if (FlatDistance(Owner.Position, point) < MinimumRange) return false;
        return world.CanStandAt(Flatten(point), Owner.Radius);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 from = Owner.Position;
        Vector3 dest = Flatten(point);
        float distance = FlatDistance(from, dest);

        if (distance > MaximumRange && distance > 0.001f)
        {
            Vector3 direction = new((dest.X - from.X) / distance, 0f, (dest.Z - from.Z) / distance);
            float length = MaximumRange * OvershootClamp;
            dest = new Vector3(from.X + direction.X * length, from.Y, from.Z + direction.Z * length);
        }

        if (!world.CanStandAt(dest, Owner.Radius))
        {
            dest = PushBackToWalkable(world, from, dest);
            if (dest == from) return false;
        }

        world.MoveUnitTo(Owner, dest);
        Owner.Target = null;
        Owner.View?.Play("Stand");
        return true;
    }

    /// <summary>沿来路回推找最近的能站点（客户端 FUN_6f4af640 的深度回推）。</summary>
    private Vector3 PushBackToWalkable(World world, Vector3 from, Vector3 dest)
    {
        float total = FlatDistance(from, dest);
        if (total <= 0.001f) return from;

        float step = Mathf.Max((Owner?.Radius ?? 16f) * 0.5f, 8f);
        int steps = Mathf.Max(1, (int)(total / step));
        for (int index = steps - 1; index >= 0; index--)
        {
            float ratio = index * step / total;
            var candidate = new Vector3(
                from.X + (dest.X - from.X) * ratio,
                from.Y,
                from.Z + (dest.Z - from.Z) * ratio);
            if (index == 0 || world.CanStandAt(candidate, Owner?.Radius ?? 16f)) return candidate;
        }
        return from;
    }
}
