using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 治疗 Ahea（牧师/野怪牧师的自动施法治疗）。
/// 物编：Cool=1 Cost=5 Rng=250 DataA=25。每次治疗 DataA 点，AI 会自己找最虚弱的友军放。
/// </summary>
public sealed class Heal : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Amount => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive || Owner == null) return false;
        if (target == Owner) return false;
        bool ally = world != null ? world.AreAllied(Owner.Player, target.Player) : Owner.Player == target.Player;
        return ally && InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive) return false;
        target.Heal(Amount);
        return true;
    }

    /// <summary>自动施法：找范围内血量比例最低的友军（低于 90% 才治）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit best = null;
        float bestRatio = 0.9f;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other == Owner) continue;
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
