using Godot;
using System;

namespace Launcher;

/// <summary>AI 自动施法的出手次序：控制先把人按住，再爆发，最后支援/增益。</summary>
public enum AbilityCastPriority
{
    Control = 0,
    Burst = 1,
    Support = 2,
    Utility = 3,
}

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

    /// <summary>AI 出手次序：控制(0) → 爆发(1) → 支援(2) → 其他(3)。World.TickAutoCast 按它排序。</summary>
    public virtual AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    /// <summary>目标必须是尸体（腐尸甲虫这类取材技能），拾取时要把尸体算进去。</summary>
    public virtual bool TargetsCorpse => false;

    // —— 光环：每 tick 由 World.RefreshAuras 汇总，同源取最大 ——
    /// <summary>光环半径（物编 Area 列，缺省按 900）。</summary>
    public virtual float AuraRadius => Area > 0f ? Area : 900f;

    /// <summary>把自己的光环数值填进汇总（子类只填自己那一项）。</summary>
    public virtual void CollectAura(ref AuraBonus bonus) { }

    // —— 反伤：荆棘光环走光环字段，尖刺甲壳这类被动直接给比例 ——
    /// <summary>受普攻时按该比例把伤害反给攻击者（0 = 不反）。</summary>
    public virtual float ReflectRatio => 0f;

    /// <summary>反伤是否也吃远程攻击（荆棘光环只反近战，甲壳不管远近）。</summary>
    public virtual bool ReflectsRangedAttack => false;

    /// <summary>被动提供的护甲（尖刺甲壳这类「常驻加成」写在技能上，不往单位字段里加）。</summary>
    public virtual float PassiveArmor => 0f;

    // —— 开关型技能（献祭）：开着时每 tick 由 World 推进 OnUpkeep ——
    public virtual bool IsToggle => false;
    public bool ToggledOn;

    public void SetToggled(World world, bool on)
    {
        if (ToggledOn == on) return;
        ToggledOn = on;
        OnToggle(world, on);
    }

    public virtual void OnToggle(World world, bool on) { }

    /// <summary>开着时每 tick 调用（扣法力、结算每秒伤害）。</summary>
    public virtual void OnUpkeep(World world) { }

    public float Cooldown => Type?.CooldownAt(Level) ?? 0f;
    public float ManaCost => Type?.CostAt(Level) ?? 0f;
    public float CastRange => Type?.RangeAt(Level) ?? 0f;
    public float Area => Type?.AreaAt(Level) ?? 0f;
    public float Duration => Type?.DurationAt(Level) ?? 0f;
    /// <summary>物编 Cast 列（暗影突袭的持续伤害间隔这类）。</summary>
    public float CastInterval => Type?.CastAt(Level) ?? 0f;
    /// <summary>物编 HeroDur 列：英雄身上的持续时间（HeroDur 有值就以它为准）。</summary>
    public float HeroDuration => Type?.HeroDurationAt(Level) ?? 0f;
    public float EffectiveDuration => HeroDuration > 0f ? HeroDuration : Duration;

    public bool Learned => Level >= 1;
    public bool CanLevelUp => Type != null && Level < Type.MaxLevel;
    public bool Ready => Learned && CooldownRemaining <= 0f;

    /// <summary>DataA..DataI，按当前等级。</summary>
    protected float Data(char slot) => Type?.DataAt(Level, slot) ?? 0f;

    /// <summary>UnitData.race=undead：圣光术、死亡缠绕、死亡契约都按这个判定。</summary>
    protected static bool IsUndead(Unit unit) =>
        unit?.Type != null && string.Equals(unit.Type.Race, "undead", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>活物：非建筑、非亡灵（死亡缠绕/腐臭蜂群这类「有机单位」口径的近似）。</summary>
    protected static bool IsLiving(Unit unit) =>
        unit != null && !unit.IsBuilding && !IsUndead(unit) && unit.Type != null;

    /// <summary>半径内有没有敌人：AI 判断「正在交战」用（避免和平时期乱放消耗性技能）。</summary>
    protected bool HasEnemyNearby(World world, float radius)
    {
        if (world == null || Owner == null) return false;

        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (Owner.DistanceTo(other) <= radius) return true;
        }
        return false;
    }

    /// <summary>敌对判定：不同玩家且非同队。</summary>
    protected bool IsHostile(World world, Unit other)
    {
        return world != null && Owner != null && other != null && !world.AreAllied(Owner.Player, other.Player);
    }

    /// <summary>
    /// AI 选目标：先用手上的目标；打不到（或目标不合法）就在施法距离内另找一个敌人。
    /// 控制类传 preferHero=true，优先按住敌方英雄；accept 用来过滤「必须是活物 / 有法力」这类条件。
    /// </summary>
    protected Unit FindEnemyInRange(World world, bool preferHero = false, Func<Unit, bool> accept = null)
    {
        if (world == null || Owner == null) return null;

        Unit current = Owner.Target;
        bool currentUsable = current != null && current.Alive && IsHostile(world, current) && InRange(current)
            && (accept == null || accept(current));
        if (currentUsable && (!preferHero || current.IsHero)) return current;

        Unit best = null;
        float bestScore = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive) continue;
            if (!IsHostile(world, other)) continue;
            if (!InRange(other)) continue;
            if (accept != null && !accept(other)) continue;

            float score = Owner.DistanceTo(other) + (preferHero && !other.IsHero ? 100000f : 0f);
            if (score >= bestScore) continue;
            bestScore = score;
            best = other;
        }
        return best;
    }

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

    /// <summary>持有者死亡时（镜像分身要把幻象一起收掉）。</summary>
    public virtual void OnOwnerDeath(World world) { }
}
