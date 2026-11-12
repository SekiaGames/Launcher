using Godot;

namespace Launcher;

/// <summary>
/// 神圣护甲 AHds。对标客户端 Src/Skills/ 的无目标自身免疫技：
/// 自身 Duration 秒内免疫所有伤害（物编 Dur=15/25/35，Cool=35，Cost=25）。
/// </summary>
public sealed class RuntimeDivineShieldAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;

        Owner.State.Add(StateMod.Invulnerable, Duration > 0f ? Duration : 15f);
        return true;
    }

    /// <summary>AI：血低于一半且身边有敌人就开，用来骗对面的爆发。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.Hp > Owner.MaxHp * 0.5f) return false;
        if (!HasEnemyNearby(world, 700f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
