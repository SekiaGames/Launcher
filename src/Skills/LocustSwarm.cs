using Godot;

namespace Launcher;

/// <summary>
/// 蝗群 AUls（地穴领主终极）。对标 Wiki RuntimeLocustSwarmAbility：
/// 每隔 DataB(0.2s) 放出一只 UnitID(uloc)，共 DataA(20) 只，Dur(30) 秒后消失；
/// 蝗虫自带 Aloc（无碰撞），会自己扑向附近敌人。物编 Cool=180 Cost=150。
/// </summary>
public sealed class LocustSwarm : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int SwarmCount => Mathf.Clamp(Mathf.RoundToInt(Data('A')), 1, 40);
    public float ReleaseInterval => Data('B') > 0.05f ? Data('B') : 0.05f;
    public float SwarmDuration => Duration > 0f ? Duration : 30f;
    public string SwarmUnitId => Type?.SummonAt(Level) ?? "uloc";

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.AddGroundSpell(new LocustSwarmRelease
        {
            Caster = Owner,
            UnitId = SwarmUnitId,
            Count = SwarmCount,
            Interval = ReleaseInterval,
            Lifetime = SwarmDuration,
            Factor = Data('D'),
        });
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !IsFighting) return false;
        float radius = Mathf.Max(700f, Owner.Type?.AcquireRange ?? 0f);
        int enemies = NearbyEnemyCount(world, radius, out bool anyHero);
        if (enemies < 3 && !anyHero) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}

/// <summary>蝗群的放出节奏：每 Interval 只出一只，出满 Count 只后结束。</summary>
public sealed class LocustSwarmRelease : IGroundSpell
{
    public Unit Caster;
    public string UnitId;
    public int Count;
    public float Interval;
    public float Lifetime;
    public float Factor;

    private int _released;
    private float _timer;

    public int Released => _released;

    public bool Step(World world, float delta)
    {
        if (Caster == null || !Caster.Alive) return false;
        if (_released >= Count) return false;

        _timer -= Mathf.Max(0f, delta);
        int guard = 0;
        while (_timer <= 0f && _released < Count && guard++ < 40)
        {
            Unit locust = world.Summon(Caster, UnitId, Caster.Position, Lifetime);
            if (locust != null)
            {
                var agent = new LocustAgent { Owner = locust, Caster = Caster, Factor = Factor, Level = 1 };
                locust.Abilities.Add(agent);
            }
            _released++;
            _timer += Mathf.Max(World.Tick, Interval);
        }
        return _released < Count;
    }
}

/// <summary>蝗虫代理：把打出的伤害存起来，蝗虫消失时按 DataD 比例回给施法者（RuntimeLocustSwarmAbility）。</summary>
public sealed class LocustAgent : Ability
{
    public Unit Caster;
    public float Factor;

    private float _stored;

    public override bool IsPassive => true;

    public override void OnAttackLanded(World world, DamageInfo info)
    {
        if (Caster == null || !Caster.Alive || info == null) return;
        _stored += info.Amount * Factor;
    }

    public override void OnOwnerDeath(World world)
    {
        if (Caster == null || !Caster.Alive || Mathf.Abs(_stored) < 0.5f) return;
        if (_stored > 0f) Caster.Heal(_stored);
        else Caster.Hp = Mathf.Max(1f, Caster.Hp + _stored);
        _stored = 0f;
    }
}