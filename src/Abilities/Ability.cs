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

    /// <summary>水平距离（忽略高度差）：点目标技能的射程/半径判定都用这个。</summary>
    protected static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>地面点：把高度换成施法者脚下的高度，避免点在山坡上时落点悬空。</summary>
    protected Vector3 Flatten(Vector3 point) => new(point.X, Owner?.Position.Y ?? 0f, point.Z);

    public virtual bool CheckTarget(World world, Unit target, Vector3 point) => true;

    /// <summary>执行技能效果；返回 false 表示没放出去（不扣法力、不进冷却）。被动技能用默认实现。</summary>
    public virtual bool OnCast(World world, Unit target, Vector3 point) => false;

    // —— 投射物：想「飞过去再结算」的技能覆盖这三个 + OnMissileHit ——
    /// <summary>是否走投射物。</summary>
    public virtual bool UsesMissile => false;
    public virtual float MissileSpeed => 900f;
    /// <summary>弹道模型（war3 路径）。物编 Missileart 列没导出，按原版路径补。</summary>
    public virtual string MissileArt => "";

    /// <summary>投射物命中时结算（只有 UsesMissile 的技能会被调用）。</summary>
    public virtual void OnMissileHit(World world, Unit target) { }

    // —— 引导：暴风雪、死亡腐烂这类「按住不放、按波结算」的技能覆盖这三个 ——
    /// <summary>是否引导型。引导期间施法者不能动，被打断就停。</summary>
    public virtual bool IsChanneled => false;
    /// <summary>每波间隔（秒）；&lt;=0 表示只摆姿势不结算波。</summary>
    public virtual float ChannelWaveInterval => 0.5f;
    /// <summary>引导总时长（默认取 Dur 列；暴风雪那种「波数×间隔」的自己覆盖）。</summary>
    public virtual float ChannelTotal => Duration;
    /// <summary>每波结算：落点由 BeginChannel 传进来（引导期间施法者可能被推开）。</summary>
    public virtual void OnChannelWave(World world, Vector3 point) { }

    /// <summary>AI 自动施法钩子：返回 true 表示这次真的放出去了。</summary>
    public virtual bool TryAutoCast(World world) => false;

    /// <summary>普攻伤害钩子：暴击、重击这类被动改这里。</summary>
    public virtual float ModifyOutgoingAttack(World world, Unit target, float raw) => raw;

    /// <summary>受击钩子：闪避这类被动返回 false 把伤害取消掉。</summary>
    public virtual bool CanBeHitBy(World world, DamageInfo info) => true;

    public virtual void OnLearned() { }
}
