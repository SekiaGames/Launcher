using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 地精修补匠。对标客户端 RuntimeRoboGoblinAbility / RuntimeEngineeringUpgradeAbility /
/// RuntimeDemolishAbility（L3_Simulation/Simulation/Configuration），数值全部读物编：
/// ANrg 机器人地精（双向形态 + 力量/护甲）、ANeg 工程升级（移速/攻击 + 技能等级替换）、
/// ANde 粉碎（对建筑倍率）、ANsy 口袋工厂（召工厂并定时出兵）。
/// </summary>
public sealed class RoboGoblin : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    /// <summary>DataA 是人类形态、UnitID 是机器人形态（ANrg 系列四码各自覆盖）。</summary>
    public string HumanFormId => (Type?.DataTextAt(Level, 'A') ?? "").Trim();
    public string RobotFormId => Type?.SummonAt(Level) ?? "Nrob";

    public int StrengthBonus => Mathf.RoundToInt(Data('E'));
    public float ArmorBonus => Data('F');

    private int _appliedStrength;
    private float _appliedArmor;

    public bool InRobotForm => Owner?.Type != null
        && string.Equals(Owner.Type.Id, RobotFormId, StringComparison.OrdinalIgnoreCase);

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner?.Type == null || !Owner.Alive) return false;

        bool robotNow = InRobotForm;
        UnitType form = UnitCatalog.Get(robotNow ? HumanFormId : RobotFormId);
        if (form == null) return false;
        if (!world.BeginMorph(Owner, form, 0f, disable: false)) return false;

        ApplyFormBonus(robotNow ? 0 : StrengthBonus, robotNow ? 0f : ArmorBonus);
        SyncDemolish(!robotNow);
        return true;
    }

    /// <summary>粉碎是机器人形态的固有技能（英雄变形不会自动换技能栏，这里手动授/收）。</summary>
    private void SyncDemolish(bool robot)
    {
        if (Owner == null) return;
        if (robot)
        {
            foreach (string id in DemolishIds)
            {
                if (Owner.FindAbility(id) == null) continue;
                return;
            }
            Owner.GiveAbility("ANde", 1);
            return;
        }
        foreach (string id in DemolishIds) Owner.RemoveAbility(id);
    }

    private static readonly string[] DemolishIds = { "ANde", "ANd1", "ANd2", "ANd3" };

    /// <summary>机器人形态的力量按「+1 力量 = +25 生命」直接落地（变形期间不做属性重算）。</summary>
    private void ApplyFormBonus(int strength, float armor)
    {
        if (Owner == null) return;

        Owner.BonusStrength += strength - _appliedStrength;
        Owner.Strength += strength - _appliedStrength;
        Owner.MaxHp += (strength - _appliedStrength) * 25f;
        Owner.Hp = Mathf.Clamp(Owner.Hp + (strength - _appliedStrength) * 25f, 1f, Owner.MaxHp);
        Owner.BonusArmor += armor - _appliedArmor;
        Owner.View?.SetHp(Owner.Hp / Mathf.Max(1f, Owner.MaxHp), Owner.Alive);

        _appliedStrength = strength;
        _appliedArmor = armor;
    }

    public override bool TryAutoCast(World world) => false;

    public override void OnOwnerDeath(World world) => ApplyFormBonus(0, 0f);
}

public sealed class EngineeringUpgrade : Ability
{
    private static readonly char[] PairSlots = { 'C', 'D', 'E', 'F' };

    public override bool IsPassive => true;

    private float _appliedMove;
    private float _appliedDamage;
    private float _pairTimer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner?.Type == null || !Owner.Alive) return;

        float movePct = Data('A');
        float damage = Data('B');
        if (!Mathf.IsEqualApprox(movePct, _appliedMove) || !Mathf.IsEqualApprox(damage, _appliedDamage))
        {
            Owner.BonusMoveSpeedFlat += (movePct - _appliedMove) * Owner.Type.Speed;
            Owner.BonusDamageFlat += damage - _appliedDamage;
            _appliedMove = movePct;
            _appliedDamage = damage;
        }

        // 技能码替换：每秒重扫一次，后学的招也能吃到升级（已换过的不重复换）
        _pairTimer -= delta;
        if (_pairTimer > 0f) return;
        _pairTimer = 1f;
        for (int level = 1; level <= Level; level++) SwapPairs(world, level);
    }

    /// <summary>DataC..DataF 每格是「旧技能,新技能」：把已经学过的旧码换成升级后的码。</summary>
    private void SwapPairs(World world, int level)
    {
        foreach (char slot in PairSlots)
        {
            string raw = Type?.DataTextAt(level, slot) ?? "";
            string[] parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || parts[0].Length < 4 || parts[1].Length < 4) continue;

            string oldId = parts[0][..4];
            string newId = parts[1][..4];
            Ability old = Owner.FindAbility(oldId);
            if (old == null || Owner.FindAbility(newId) != null) continue;

            Owner.RemoveAbility(oldId);
            Owner.GiveAbility(newId, Mathf.Max(1, old.Level));
        }
    }

    public override void OnOwnerDeath(World world)
    {
        if (Owner == null) return;
        Owner.BonusMoveSpeedFlat -= _appliedMove * Owner.Type?.Speed ?? 0f;
        Owner.BonusDamageFlat -= _appliedDamage;
        _appliedMove = 0f;
        _appliedDamage = 0f;
    }
}

/// <summary>粉碎（ANde）：普攻打建筑时按 DataA 概率把伤害乘 DataB 并追加 DataC。</summary>
public sealed class Demolish : Ability
{
    public override bool IsPassive => true;

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (world == null || target == null || !target.IsBuilding) return raw;
        if (!world.RollPercent(Data('A'))) return raw;
        return raw * Mathf.Max(0f, Data('B')) + Data('C');
    }
}

/// <summary>口袋工厂（ANsy）：落点召唤工厂，工厂定时在半径内放出地精（地精寿命 DataC）。</summary>
public sealed class SummonFactory : Ability
{
    /// <summary>物编没写放出间隔，取原版节奏 4 秒一只（口径简化）。</summary>
    private const float SpawnInterval = 4f;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;

    public string FactoryId => Type?.SummonAt(Level) is { Length: >= 4 } id ? id : "nfac";
    public string GoblinId => (Type?.DataTextAt(Level, 'B') ?? "").Trim();
    public float Lifetime => Duration > 0f ? Duration : 40f;
    public float GoblinLifetime => Data('C');
    public float SpawnRadius => Data('D') > 0f ? Data('D') : 200f;
    public int MaxFactories => Mathf.Max(1, Mathf.RoundToInt(Data('A')));

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || world == null) return false;
        float range = CastRange > 0f ? CastRange : 500f;
        return FlatDistance(Owner.Position, point) <= range + 64f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        if (CountFactories(world) >= MaxFactories) return false;

        Unit factory = world.Summon(Owner, FactoryId, Flatten(point), Lifetime);
        if (factory == null) return false;

        world.AddGroundSpell(new FactorySpawnSpell
        {
            Factory = factory,
            GoblinId = GoblinId,
            Remaining = Lifetime,
            Interval = SpawnInterval,
            Radius = SpawnRadius,
            GoblinLifetime = GoblinLifetime,
        });
        return true;
    }

    private int CountFactories(World world)
    {
        int count = 0;
        foreach (Unit unit in world.Units)
        {
            if (unit == null || !unit.Alive) continue;
            if (unit.Summoner != Owner) continue;
            if (unit.Type == null || !string.Equals(unit.Type.Id, FactoryId, StringComparison.OrdinalIgnoreCase)) continue;
            count++;
        }
        return count;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

/// <summary>工厂的出兵节奏：工厂活着就每隔 Interval 放一只地精，工厂没了/时间到就停。</summary>
public sealed class FactorySpawnSpell : IGroundSpell
{
    public Unit Factory;
    public string GoblinId = "";
    public float Remaining;
    public float Interval = 4f;
    public float Radius = 200f;
    public float GoblinLifetime;

    private float _timer = 1f;
    private float _angle;

    public bool Step(World world, float delta)
    {
        if (Factory == null || !Factory.Alive || world == null) return false;

        Remaining -= delta;
        if (Remaining <= 0f) return false;

        _timer -= delta;
        if (_timer > 0f) return true;
        _timer = Interval;
        if (GoblinId.Length < 4) return true;

        _angle += 1.1f;
        var spot = new Vector3(
            Factory.Position.X + Mathf.Cos(_angle) * Radius * 0.5f,
            Factory.Position.Y,
            Factory.Position.Z + Mathf.Sin(_angle) * Radius * 0.5f);
        world.Summon(Factory, GoblinId, spot, GoblinLifetime);
        return true;
    }
}