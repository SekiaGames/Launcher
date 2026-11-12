using Godot;

namespace Launcher;

/// <summary>
/// 复活 AHre：把半径 Area 内最多 DataA 个友军尸体满血复活（物编 DataA=6 Area=900）。
/// 只复活非英雄（英雄走祭坛流程），与客户端 RuntimeResurrectionAbility 的口径一致。
/// </summary>
public sealed class RuntimeResurrectionAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

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

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Radius)) return false;

        int corpses = 0;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse || other.IsHero) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (Owner.DistanceTo(other) > Radius) continue;
            corpses++;
        }

        int required = Mathf.Min(2, MaxTargets);
        return corpses >= required && world.TryCast(Owner, this, null, Owner.Position);
    }
}
