using Godot;

namespace OpenWC3;

/// <summary>
/// 黑暗仪式 AUdr（巫妖）。牺牲一个友军单位，把它最大生命的 DataA 比例换成自己的法力。
/// 物编：Cool=15 Cost=25 Rng=800 DataA=0.33/0.66/1.00。
/// </summary>
public sealed class RuntimeDarkRitualAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float ManaRatio => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (ReferenceEquals(target, Owner) || target.IsHero) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        float mana = target.MaxHp * ManaRatio;
        world.Sacrifice(target);
        if (Owner.MaxMana > 0f) Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + mana);
        return true;
    }

    /// <summary>AI：法力低于四成、正在交战时才牺牲，且先挑召唤物、绝不挑工人。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.MaxMana <= 0f) return false;
        if (Owner.Mana / Owner.MaxMana > 0.4f) return false;
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
