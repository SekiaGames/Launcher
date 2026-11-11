using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 野怪、古树与召唤物自带的技能（客户端 L3_Simulation/Configuration/Runtime*Ability 的同款实现）：
/// 吃树回血、狂暴、缓慢光环、永久献祭、龙卷风伤害。都不需要玩家下指令，全部走被动 tick。
/// </summary>
public sealed class EatTree : Ability
{
    /// <summary>物编 Rng 只是「够得着」，实际按这个接近半径找树。</summary>
    private const float ReachPadding = 160f;

    public override bool IsPassive => true;

    private float _scanTimer;
    private float _restoreTimer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        if (_restoreTimer > 0f) _restoreTimer -= delta;
        _scanTimer -= delta;
        if (_scanTimer > 0f) return;
        _scanTimer = 2f;
        if (_restoreTimer > 0f) return;

        // 原版是玩家主动点树；这里退化成「血少了就吃最近的树」，免得古树站着不动永远不吃
        if (Owner.Hp > Owner.MaxHp * 0.7f) return;

        float total = Data('C');
        float seconds = Duration > 0f ? Duration : 30f;
        if (total <= 0f) return;

        TreeState tree = world.TakeNearestTree(Owner.Position, CastRange + ReachPadding);
        if (tree == null) return;

        world.ConsumeTree(tree);
        world.ApplyBuff(Owner, new HealOverTimeBuff { AmountPerTick = total / seconds, Interval = 1f }, seconds);
        _restoreTimer = seconds + 10f;
    }
}

public sealed class Frenzy : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new StatBuff
        {
            AttackSpeedPct = Data('A'),
            MoveSpeedPct = Data('B'),
        }, Duration > 0f ? Duration : 20f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 800f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

/// <summary>缓慢光环（龙卷风 Aasl）：DataA/DataB 是带符号的移动/攻击速度增量，对范围内敌人持续刷新减速。</summary>
public sealed class SlowAura : Ability
{
    public override bool IsPassive => true;

    private float _pulseTimer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        _pulseTimer -= delta;
        if (_pulseTimer > 0f) return;
        _pulseTimer = 0.5f;

        float move = Mathf.Clamp(-Data('A'), 0f, 0.95f);
        float attack = Mathf.Clamp(-Data('B'), 0f, 0.95f);
        if (move <= 0f && attack <= 0f) return;

        float radius = Area > 0f ? Area : 600f;
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, radius))
        {
            if (victim.IsBuilding) continue;
            victim.ApplySlow(move, attack, 0.6f);
        }
    }
}

/// <summary>永久献祭（地狱火 ANpi）：每 Dur 秒对 Area 内敌方地面单位结算 DataA。</summary>
public sealed class PermanentImmolation : Ability
{
    public override bool IsPassive => true;

    private float _pulseTimer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        float interval = Duration > 0f ? Duration : 1f;
        _pulseTimer += delta;
        if (_pulseTimer < interval) return;
        _pulseTimer -= interval;

        float damage = Data('A');
        if (damage <= 0f) return;

        float radius = Area > 0f ? Area : 220f;
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, radius, hitBuildings: false))
        {
            if (victim.Type != null && victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, damage, AttackType.Spells, DamageType.Magic));
        }
    }
}

/// <summary>
/// 龙卷风自身伤害（Atdg）：每秒一波，取「包含目标且半径最小」的那一圈结算，
/// 多圈不叠加（客户端 RuntimeTornadoDamageAbility 同口径）。
/// </summary>
public sealed class TornadoDamage : Ability
{
    public override bool IsPassive => true;

    private float _pulseTimer = 1f;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        _pulseTimer -= delta;
        if (_pulseTimer > 0f) return;
        _pulseTimer += 1f;

        float outerRadius = Area > 0f ? Area : 650f;
        float outerDamage = Data('A');
        float innerRadius = Data('B');
        float innerDamage = Data('C');

        float query = Mathf.Max(outerRadius, innerRadius);
        if (query <= 0f) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, query))
        {
            // 物编 targs = structure,enemy：龙卷风只磨建筑，不特殊处理单位
            if (!victim.IsBuilding) continue;

            float distance = FlatDistance(Owner.Position, victim.Position);
            float bestRadius = float.MaxValue;
            float amount = 0f;

            PickRing(outerRadius, outerDamage, distance, ref bestRadius, ref amount);
            PickRing(innerRadius, innerDamage, distance, ref bestRadius, ref amount);
            if (amount <= 0f) continue;

            world.DealDamage(World.MakeAbilityDamage(Owner, victim, amount, AttackType.Spells, DamageType.Magic));
        }
    }

    private static void PickRing(float radius, float damage, float distance, ref float bestRadius, ref float amount)
    {
        if (radius <= 0f || damage <= 0f || distance > radius || radius >= bestRadius) return;
        bestRadius = radius;
        amount = damage;
    }
}