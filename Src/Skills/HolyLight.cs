using Godot;

namespace Launcher;

/// <summary>
/// 圣光术 AHhb。对标客户端 Src/Skills/HolyLight.cs：
/// 治疗友军 DataA 点；对亡灵造成一半数值的伤害（DualEffect 的 -1 分支）。
/// 物编：Cool=5 Cost=65 Rng=800 DataA=200/400/600。
/// </summary>
public sealed class HolyLight : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float HealAmount => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive || Owner == null) return false;
        if (target == Owner) return false;
        if (!InRange(target)) return false;

        // 客户端 Src/Skills/HolyLight.cs:34-52：友军一律可治；敌军只有亡灵可选（吃 HealAmount/2）。
        bool ally = world != null ? world.AreAllied(Owner.Player, target.Player) : Owner.Player == target.Player;
        return ally || IsUndead(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (target == null || !target.Alive || world == null) return false;

        bool ally = world.AreAllied(Owner.Player, target.Player);
        if (!ally)
        {
            // 敌方亡灵：半额神圣伤害（客户端 DamageType.Divine，Launcher 用 Universal 近似"无视护甲"）。
            world.DealDamage(World.MakeAbilityDamage(Owner, target, HealAmount * 0.5f, AttackType.Spells, DamageType.Universal));
        }
        else
        {
            target.Heal(HealAmount);
        }
        return true;
    }

    /// <summary>自动施法：给范围内最虚弱的友军（亡灵友军会被误伤，所以跳过）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit best = null;
        float bestRatio = 0.85f;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other == Owner) continue;
            if (!world.AreAllied(Owner.Player, other.Player)) continue;
            if (IsUndead(other)) continue;
            if (!InRange(other)) continue;

            float ratio = other.Hp / Mathf.Max(1f, other.MaxHp);
            if (ratio >= bestRatio) continue;
            bestRatio = ratio;
            best = other;
        }

        return best != null && world.TryCast(Owner, this, best, best.Position);
    }
}
