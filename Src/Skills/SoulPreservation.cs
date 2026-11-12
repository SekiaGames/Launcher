using Godot;

namespace Launcher;

/// <summary>
/// 灵魂保存 ANsl（玛尔加尼斯）。对标客户端 Src/Skills/CampaignAndAuraSkills.cs 的 SoulPreservation：
/// 目标 Dur（3s）内无敌 + 魔免，到期后连人带所有权转给施法者并立即死亡（原版直接走死亡，不吃减伤）。
/// 物编 Cool=0.1 Rng=1000，只打非英雄活物。
/// </summary>
public sealed class SoulPreservation : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        foreach (Buff existing in target.Buffs)
        {
            if (existing is not SoulPreservationBuff kept) continue;
            kept.Remaining = Mathf.Max(kept.Remaining, Duration > 0f ? Duration : 3f);
            return true;
        }

        var buff = new SoulPreservationBuff
        {
            Owner = target,
            Source = this,
            Name = Name,
            Remaining = Duration > 0f ? Duration : 3f,
            Taker = Owner,
        };
        buff.OnApply(world);
        target.Buffs.Add(buff);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => !other.IsHero && !other.IsBuilding);
        return target != null && world.TryCast(Owner, this, target, target.Position);
    }
}

/// <summary>灵魂保存的保留状态：到期撤状态 → 换阵营 → 直接死亡。</summary>
public sealed class SoulPreservationBuff : Buff
{
    public Unit Taker;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        Owner.State.Add(StateMod.Invulnerable);
        Owner.State.Add(StateMod.MagicImmune);
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;

        Owner.State.RemoveAll(StateMod.Invulnerable);
        Owner.State.RemoveAll(StateMod.MagicImmune);
        if (!Owner.Alive) return;

        Owner.TransferTo(Taker?.Player ?? Owner.Player);
        world?.KillUnit(Owner, Taker);
    }
}