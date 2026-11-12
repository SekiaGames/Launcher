using Godot;

namespace OpenWC3;

/// <summary>
/// 霜冻护甲 AUfa（巫妖）。给自己或友军加 DataB 点护甲，持续 DataA 秒。
/// 物编：Cool=2 Cost=40 Rng=800 DataA=45（增益时长）DataB=3/5/7（护甲）Dur=5（攻击者减速时长，未做）。
/// 被普攻命中时让攻击者减速 Dur(5) 秒（走 <see cref="Buff.OnAttacked"/>）。
/// </summary>
public sealed class RuntimeFrostArmorAbility : Ability
{
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float ArmorBonus => Data('B');
    public float BuffDuration => Data('A') > 0f ? Data('A') : 45f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (!world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null) return false;

        var buff = new FrostArmorBuff
        {
            Owner = target,
            Source = this,
            Name = Name,
            Remaining = BuffDuration,
            ArmorBonus = ArmorBonus,
            SlowMovePct = 0.3f,
            SlowSeconds = Duration > 0f ? Duration : 5f,
        };
        buff.OnApply(world);
        target.Buffs.Add(buff);
        return true;
    }

    /// <summary>AI：交战时就给最前面的友军套冰甲（冷却只有 2s，可以反复上）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;

        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.IsBuilding) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (!InRange(other)) continue;
            if (other.Target == null) continue;

            float distance = Owner.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        return best != null && world.TryCast(Owner, this, best, best.Position);
    }
}
