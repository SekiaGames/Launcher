using Godot;

namespace Launcher;

/// <summary>
/// 医疗波 AOhw（暗影猎手）。对标客户端 RuntimeHealingWaveAbility 的跳跃口径：
/// 首个目标回 DataA(130/215/300) 点，之后每跳减 DataC(25%)，最多跳 DataB(3/4/5) 次，跳跃半径 Area(500)。
/// 物编：Cool=9 Cost=90 Rng=700。
/// </summary>
public sealed class RuntimeHealingWaveAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        int jumps = Mathf.Max(0, Mathf.RoundToInt(Data('B')));
        float decay = Data('C') > 0f ? Data('C') : 0.25f;
        float amount = Data('A');

        var visited = new System.Collections.Generic.List<Unit> { target };
        Unit current = target;
        current.Heal(amount);

        for (int jump = 0; jump < jumps; jump++)
        {
            amount *= 1f - decay;
            Unit next = NearestAlly(world, current.Position, visited);
            if (next == null) break;
            next.Heal(amount);
            visited.Add(next);
            current = next;
        }

        LastChain.Clear();
        LastChain.AddRange(visited);
        return true;
    }

    /// <summary>最近一次的治疗链，场景层按它画逐段闪电。</summary>
    public readonly System.Collections.Generic.List<Unit> LastChain = new();

    private Unit NearestAlly(World world, Vector3 point, System.Collections.Generic.List<Unit> visited)
    {
        Unit best = null;
        float bestDistance = Area > 0f ? Area : 500f;

        foreach (Unit other in world.AlliesInRange(Owner, point, bestDistance))
        {
            if (visited.Contains(other)) continue;
            float distance = FlatDistance(point, other.Position);
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        return best;
    }

    /// <summary>AI：有友军掉血就丢（治疗链优先补最密的那团）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit best = null;
        float bestRatio = 0.85f;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (!InRange(other)) continue;

            float ratio = other.Hp / Mathf.Max(1f, other.MaxHp);
            if (ratio >= bestRatio) continue;
            bestRatio = ratio;
            best = other;
        }
        return best != null && world.TryCast(Owner, this, best, best.Position);
    }
}
