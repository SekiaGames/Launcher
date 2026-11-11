using Godot;

namespace Launcher;

/// <summary>
/// 技能运行时实例。对标客户端 Src/Abilities/Ability.cs：
/// 每个单位持有一份实例，等级与冷却都在实例上，数值全部从 <see cref="AbilityType"/> 按等级读。
/// </summary>
public abstract class Ability
{
    public Unit Owner;
    public AbilityType Type;
    /// <summary>0 = 已学会但没加点（英雄技能），非英雄技能直接 1。</summary>
    public int Level;
    public float CooldownRemaining;

    public string Id => Type?.Id ?? "";
    public string Name => string.IsNullOrEmpty(Type?.Name) ? Id : Type.Name;
    public string Hotkey => Type?.Hotkey ?? "";
    public string IconArt => Type?.IconArt ?? "";

    public virtual bool IsPassive => false;
    public virtual bool IsAura => false;
    public virtual bool IsUltimate => Type != null && Type.Ultimate;
    public virtual AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

    public float Cooldown => Type?.CooldownAt(Level) ?? 0f;
    public float ManaCost => Type?.CostAt(Level) ?? 0f;
    public float CastRange => Type?.RangeAt(Level) ?? 0f;
    public float Area => Type?.AreaAt(Level) ?? 0f;
    public float Duration => Type?.DurationAt(Level) ?? 0f;

    public bool Learned => Level >= 1;
    public bool CanLevelUp => Type != null && Level < Type.MaxLevel;
    public bool Ready => Learned && CooldownRemaining <= 0f;

    /// <summary>DataA..DataI，按当前等级。</summary>
    protected float Data(char slot) => Type?.DataAt(Level, slot) ?? 0f;

    /// <summary>施法距离判定：Rng 为 0 的技能不限距离（自身/AoE 型）。</summary>
    protected bool InRange(Unit target)
    {
        if (Owner == null || target == null) return false;
        float range = CastRange;
        if (range <= 0f) return true;
        return Owner.DistanceTo(target) <= range + target.Radius;
    }

    public virtual bool CheckTarget(World world, Unit target, Vector3 point) => true;

    /// <summary>执行技能效果；返回 false 表示没放出去（不扣法力、不进冷却）。被动技能用默认实现。</summary>
    public virtual bool OnCast(World world, Unit target, Vector3 point) => false;

    /// <summary>AI 自动施法钩子：返回 true 表示这次真的放出去了。</summary>
    public virtual bool TryAutoCast(World world) => false;

    /// <summary>普攻伤害钩子：暴击、重击这类被动改这里。</summary>
    public virtual float ModifyOutgoingAttack(World world, Unit target, float raw) => raw;

    public virtual void OnLearned() { }
}
