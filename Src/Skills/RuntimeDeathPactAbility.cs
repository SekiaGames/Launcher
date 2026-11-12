using Godot;

namespace Launcher;

/// <summary>
/// 死亡契约 AUdp（死亡骑士）。杀死一个友军非英雄单位，按它最大生命的 DataB 倍回自己的血。
/// 物编 DataB=1/2/3（1 = 100%），ubertip 里当百分号显示是暴雪自己的写法问题，这里按倍数读。
/// </summary>
public sealed class RuntimeDeathPactAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Ratio => Data('B') > 0f ? Data('B') : 1f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (ReferenceEquals(target, Owner)) return false;
        if (target.IsHero) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        float heal = target.MaxHp * Ratio;
        world.Sacrifice(target);
        Owner.Heal(heal);
        return true;
    }

    /// <summary>AI：血掉到一半以下、正在交战时才用；先挑召唤物，绝不挑工人。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.Hp / Mathf.Max(1f, Owner.MaxHp) > 0.5f) return false;
        if (!HasEnemyNearby(world, 700f)) return false;

        Unit best = null;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.IsHero || other.IsBuilding) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (other.IsWorker) continue;
            if (!InRange(other)) continue;
            if (best == null || other.IsSummon && !best.IsSummon) best = other;
        }

        return best != null && world.TryCast(Owner, this, best, best.Position);
    }
}
