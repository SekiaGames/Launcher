using Godot;

namespace Launcher;

/// <summary>
/// 放逐 AHbn（血法师）。对标 Wiki RuntimeBanishAbility：
/// 目标 Dur(12) 秒虚无（英雄 HeroDur 4 秒）：不能普攻、只吃魔法伤害，
/// 移动速度 -DataA(50%)、攻速 -DataB(0)。可以对敌人用，也可以救自己人。
/// </summary>
public sealed class RuntimeBanishAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float MoveSlowPct => Data('A');
    public float AttackSlowPct => Data('B');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.IsUnderConstruction) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        if (seconds <= 0f) return false;

        var buff = new BanishBuff
        {
            Owner = target,
            Source = this,
            Name = Name,
            Remaining = seconds,
            MoveSlowPct = MoveSlowPct,
            AttackSlowPct = AttackSlowPct,
        };
        buff.OnApply(world);
        target.Buffs.Add(buff);
        return true;
    }

    /// <summary>AI：控制类，优先把敌方英雄放逐掉（打不动也放不出来）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true,
            accept: other => !other.IsBuilding && !other.State.Ethereal);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
