using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 客户端里同样是「内部能力对象」的码：采集、返还、商店买卖、集结、金矿、腐地标记、视觉标记。
/// 真正的行为在本工程里由订单（采金/伐木/返金）、商店 UI、集结点与 World 的世界系统承担，
/// 这里只提供一个被动对象，让 abilList 里的码有实现、命令卡也不会多出按不动的按钮。
/// </summary>
public sealed class InternalAbility : Ability
{
    public override bool IsPassive => true;
    public override bool OnCast(World world, Unit target, Vector3 point) => false;
}

/// <summary>漫游（Awan）：野怪没敌人、没指令时在营地附近随机走动（对标客户端 RuntimeWanderAbility）。</summary>
public sealed class Wander : Ability
{
    public override bool IsPassive => true;

    public float Radius => Area > 0f ? Area : 400f;

    private float _timer;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;
        if (Owner.IsBuilding || Owner.AutoAcquire) return;
        if (Owner.Target != null || Owner.MoveTarget.HasValue) return;
        if (Owner.State.Stunned || Owner.State.Snared || Owner.IsChanneling) return;

        _timer -= delta;
        if (_timer > 0f) return;
        _timer = 6f;

        int steps = world.RandomIndex(5) + 1;
        float angle = world.RandomIndex(8) * 0.7853982f;
        float distance = Radius * (0.3f + 0.14f * steps);
        Owner.MoveTarget = Owner.Home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }
}

/// <summary>
/// 坟场产尸（Agyd）：每逢 Dur(15)s 在建筑旁留下一个 UnitID(ugho) 尸体，同时在场的最多 DataA(5) 具。
/// </summary>
public sealed class Graveyard : Ability
{
    public override bool IsPassive => true;

    public float Interval => Duration > 0f ? Duration : 15f;
    public int MaxCorpses => Mathf.Max(1, (int)Data('A'));
    public float Radius => Area > 0f ? Area : 200f;
    public string CorpseUnitId => Type?.SummonAt(Level) ?? "ugho";

    private float _elapsed;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        _elapsed += delta;
        if (_elapsed < Interval) return;
        _elapsed = 0f;

        int alive = 0;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse) continue;
            if (!string.Equals(other.Type?.Id, CorpseUnitId, StringComparison.OrdinalIgnoreCase)) continue;
            if (Owner.DistanceTo(other) > Radius * 2f) continue;
            alive++;
        }
        if (alive >= MaxCorpses) return;

        Vector3 spot = Owner.Position + new Vector3(Radius * 0.6f, 0f, Radius * 0.3f);
        Unit corpse = world.CreateFor(Owner, CorpseUnitId, spot);
        if (corpse == null) return;

        corpse.Hp = 0f;
        corpse.EnterDeath();
        if (corpse.View != null) corpse.View.Root.Position = spot;
    }
}

/// <summary>
/// 混乱之雨（Anrc / ANr3）。对标客户端 RuntimeRainOfChaosAbility：
/// DataA 是地狱火技能码（ANin），在 Area(900) 内随机砸下 DataB 只地狱火单位；
/// 每颗落地按 ANin 的 DataA 伤害 / Area 半径 / Dur 眩晕，召唤物寿命取 ANin 的 DataB。
/// </summary>
public sealed class RainOfChaos : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public int Count => Mathf.Max(1, (int)Data('B'));
    public float Radius => Area > 0f ? Area : 900f;

    public string InfernalAbilityId
    {
        get
        {
            string raw = (Type?.DataTextAt(Level, 'A') ?? "").Split(',')[0].Trim();
            if (raw.Length >= 4) return raw[..4];
            string own = Type?.SummonAt(Level) ?? "";
            return own.Length >= 4 ? own[..4] : "ANin";
        }
    }

    private AbilityType InfernalData => AbilityCatalog.Get(InfernalAbilityId);

    public string InfernalId
    {
        get
        {
            string unit = InfernalData?.SummonAt(1) ?? "";
            unit = unit.Trim();
            if (unit.Length >= 4 && unit != "-") return unit[..4];
            string own = Type?.SummonAt(Level) ?? "";
            own = own.Trim();
            return own.Length >= 4 && own != "-" ? own[..4] : "ninf";
        }
    }

    public float ImpactDamage => InfernalData?.DataAt(1, 'A') ?? 0f;
    public float ImpactRadius => InfernalData?.AreaAt(1) ?? 0f;
    public float ImpactStun => InfernalData?.DurationAt(1) ?? 0f;
    public float ImpactStunOnHero => InfernalData?.HeroDurationAt(1) ?? ImpactStun;
    public float SummonSeconds => InfernalData?.DataAt(1, 'B') ?? 0f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        float impactRadius = ImpactRadius > 0f ? ImpactRadius : 200f;
        int summoned = 0;
        for (int index = 0; index < Count; index++)
        {
            float angle = (world.RandomIndex(16) / 16f) * Mathf.Tau;
            float distance = Radius * (0.25f + 0.05f * world.RandomIndex(10));
            Vector3 spot = Owner.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            if (ImpactDamage > 0f)
            {
                foreach (Unit victim in world.EnemiesInRange(Owner, spot, impactRadius))
                {
                    world.DealDamage(World.MakeAbilityDamage(Owner, victim, ImpactDamage, AttackType.Spells, DamageType.Magic));
                }
            }
            if (ImpactStun > 0f)
            {
                foreach (Unit victim in world.EnemiesInRange(Owner, spot, impactRadius))
                {
                    victim.State.Add(StateMod.Stun, victim.IsHero ? ImpactStunOnHero : ImpactStun);
                }
            }

            if (world.Summon(Owner, InfernalId, spot, SummonSeconds) != null) summoned++;
        }
        return summoned > 0;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

/// <summary>
/// 黑暗之门（ANdp）：在 Area 内随机位置召出 DataB(3) 只 DataA 列表里的单位（nbal / nfel）。
/// </summary>
public sealed class DarkPortal : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int Count => Mathf.Max(1, (int)Data('B'));
    public float Radius => Area > 0f ? Area : 800f;
    public string[] PortalUnits => (Type?.DataTextAt(Level, 'A') ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        string[] units = PortalUnits;
        if (units.Length == 0) return false;

        int summoned = 0;
        for (int index = 0; index < Count; index++)
        {
            string unitId = units[world.RandomIndex(units.Length)].Trim();
            if (unitId.Length < 4) continue;
            float angle = (world.RandomIndex(16) / 16f) * Mathf.Tau;
            Vector3 spot = Owner.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (Radius * 0.3f);
            if (world.Summon(Owner, unitId[..4], spot, 0f) != null) summoned++;
        }
        return summoned > 0;
    }

    public override bool TryAutoCast(World world) => false;
}

/// <summary>
/// 寄生虫（ANpa / ACpa）。对标客户端 RuntimeParasiteAbility：
/// 命中挂一个每秒 DataA 点伤害的 Buff，目标在持续时间内死亡时召出 DataE 只 UnitID 小虫。
/// </summary>
public sealed class Parasite : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float DamagePerSecond => Data('A');
    public int SpawnCount => Mathf.Max(1, (int)Data('E'));
    public string SpawnUnitId => Type?.SummonAt(Level) is { Length: >= 4 } id ? id : "ncfs";

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (IsHostile(world, target) == false) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new ParasiteBuff
        {
            Source = this,
            Summoner = Owner,
            DamagePerSecond = DamagePerSecond,
            SpawnUnitId = SpawnUnitId,
            SpawnCount = SpawnCount,
        }, EffectiveDuration);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null) return false;
        Unit enemy = FindEnemyInRange(world, accept: unit => unit.FindBuff<ParasiteBuff>() == null);
        if (enemy == null) return false;
        return world.TryCast(Owner, this, enemy, enemy.Position);
    }
}

/// <summary>寄生虫的持续伤害与「寄生目标死亡时爆虫」。</summary>
public sealed class ParasiteBuff : Buff
{
    public Unit Summoner;
    public float DamagePerSecond;
    public string SpawnUnitId = "";
    public int SpawnCount = 1;

    private float _accumulator;

    public ParasiteBuff() => Name = "寄生虫";

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (world == null || Owner == null || !Owner.Alive) return false;

        _accumulator += delta;
        while (_accumulator >= 1f && Owner.Alive)
        {
            _accumulator -= 1f;
            if (DamagePerSecond <= 0f) continue;
            world.DealDamage(World.MakeAbilityDamage(Summoner, Owner, DamagePerSecond, AttackType.Spells, DamageType.Magic));
        }
        return alive;
    }

    public override void OnDeath(World world)
    {
        if (world == null || Owner == null || Summoner == null) return;
        if (string.IsNullOrEmpty(SpawnUnitId) || SpawnCount <= 0) return;

        for (int index = 0; index < SpawnCount; index++)
        {
            world.Summon(Summoner, SpawnUnitId, Owner.Position, 0f);
        }
    }
}