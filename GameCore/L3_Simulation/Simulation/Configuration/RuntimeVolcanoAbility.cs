using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class RuntimeVolcanoAbility : Ability
{
    readonly War3ObjectData _objects;
    string _volcanoModel = "";
    string _missileArt = "";
    float _missileSpeed = 400f;
    float _missileArc;
    SpecialEffect _visual;

    public RuntimeVolcanoAbility(War3ObjectData objects) => _objects = objects;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;
    public override IReadOnlyList<string> RuntimePreloadArts
    {
        get
        {
            RefreshArtData();
            var arts = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(_volcanoModel)) arts.Add(_volcanoModel);
            if (!string.IsNullOrWhiteSpace(_missileArt)) arts.Add(_missileArt);
            return arts;
        }
    }

    public int RingCount => Mathf.Max(0, Mathf.RoundToInt(Data('A')));
    public int Waves => Mathf.Max(1, Mathf.RoundToInt(Data('B')));
    public float WaveInterval => Data('C') > 0f ? Data('C') : 5f;
    public float Damage => Data('E');
    public float BuildingFactor => Data('D') > 0f ? Data('D') : 2f;
    public float Radius => Area > 0f ? Area : 500f;

    public override float ChannelWaveInterval => WaveInterval;
    public override float ChannelTotal => Waves * WaveInterval;

    public override void OnLevelUp() => RefreshArtData();
    public override void OnLevelDown() => RefreshArtData();

    void RefreshArtData()
    {
        int level = Mathf.Max(1, Level);
        string effectId = RuntimeAbilityFactory.ReadEffectRawcode(ObjectData, level);
        BuffTypeData effect = _objects?.GetBuff(effectId);
        _volcanoModel = DestructableModel(Type?.SummonAt(level));
        if (string.IsNullOrWhiteSpace(_volcanoModel))
            _volcanoModel = AbilityArt.FirstArt(effect, 1, "Specialart", "");
        _missileArt = AbilityArt.FirstArt(effect, 1, "Missileart", "");
        _missileSpeed = effect?.GetFloat("Missilespeed", 0, 0, 400f) ?? 400f;
        _missileArc = effect?.GetFloat("Missilearc", 0, 0, 0f) ?? 0f;
    }

    static string DestructableModel(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return "";
        SlkTable table = War3SlkReader.DestructableData();
        if (table == null) return "";
        foreach (int row in table.DataRows)
        {
            if (!string.Equals((table.Get(row, "doodID") ?? "").Trim(), typeId,
                    StringComparison.OrdinalIgnoreCase)) continue;
            return (table.Get(row, "file") ?? "").Trim();
        }
        return "";
    }

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        RefreshArtData();
        ClearVisual();
        Vector3 flatPoint = Flatten(point);
        if (!world.BeginChannel(Owner, this, flatPoint, ChannelTotal, WaveInterval))
            return false;

        if (!string.IsNullOrWhiteSpace(_volcanoModel))
        {
            _visual = world.AddSpecialEffect(_volcanoModel, new Vec2(flatPoint.X, flatPoint.Z),
                lifeSpan: ChannelTotal);
            _visual.PlayStandAfterBirth = true;
        }
        return true;
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            float damage = victim.IsBuilding ? Damage * BuildingFactor : Damage;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, damage, AttackType.Spells, DamageType.Magic));
        }
        LaunchWave(world, point);
    }

    public override void OnChannelEnded(World world, bool interrupted) => ClearVisual();

    void ClearVisual()
    {
        _visual?.Destroy();
        _visual = null;
    }

    /// <summary>每波按客户端环形排布从火山口抛出一批熔岩弹。</summary>
    void LaunchWave(World world, Vector3 point)
    {
        if (string.IsNullOrWhiteSpace(_missileArt)) return;

        var center = new Vec2(point.X, point.Z);
        float step = Radius / Mathf.Max(0.5f, RingCount + 0.5f);
        Vec2[] targets = RuntimeVolcanoWavePattern.Create(center, RingCount, step,
            () => world.Rng.NextSingle(), count => world.Rng.Next(count));
        foreach (Vec2 target in targets)
        {
            world.AddEffect(new RuntimeVolcanoMissileEffect(center, target,
                _missileArt, _missileSpeed, _missileArc, Owner.Team));
        }
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, target.Position);
    }
}

public static class RuntimeVolcanoWavePattern
{
    public static int Count(int rings) => rings <= 0
        ? 1
        : (int)Math.Min(32, 1L + rings * ((long)rings + 4));

    public static Vec2[] Create(Vec2 center, int rings, float step,
        Func<float> real, Func<int, int> integer)
    {
        var points = new Vec2[Count(rings)];
        int left = points.Length;
        float jitter = step * 0.5f * 0.3f;
        points[--left] = center + new Vec2((real() - 0.5f) * jitter, (real() - 0.5f) * jitter);
        float rotation = integer(100) * (Mathf.Tau / 100);
        for (int ring = 0; ring < rings && left > 0; ring++)
        {
            int count = 5 + ring * 2;
            float start = rotation + (ring % 2 == 0 ? Mathf.Pi : 0f);
            for (int index = 0; index < count && left > 0; index++)
            {
                float radius = step * (ring + 0.5f) + step * 0.5f * (0.75f + (20 + integer(10)) / 100f);
                float angle = start + index * (Mathf.Tau / count);
                points[--left] = center + new Vec2(
                    Mathf.Cos(angle) * radius + real() * jitter,
                    Mathf.Sin(angle) * radius + real() * jitter);
            }
        }
        return points;
    }
}

public sealed class RuntimeVolcanoMissileEffect : MissileEffect
{
    readonly Vec2 _start;
    readonly Vec2 _end;
    readonly float _distance;
    readonly float _speed;
    readonly float _arcHeight;
    readonly string _missileArt;
    readonly int _team;
    float _age;
    float _progress;

    public RuntimeVolcanoMissileEffect(Vec2 start, Vec2 end, string missileArt,
        float speed, float arcHeight, int team)
    {
        _start = start;
        _end = end;
        _missileArt = missileArt ?? "";
        _speed = MathF.Max(1f, speed);
        _arcHeight = MathF.Max(0f, arcHeight);
        _team = team;
        _distance = MathF.Max(1f, Vec2.Dist(start, end));
    }

    public override bool Step(World world, float dt)
    {
        _age += dt;
        _progress = Math.Min(1f, _progress + _speed * MathF.Max(0f, dt) / _distance);
        return _progress < 1f;
    }

    public override Vec2 Pos => _start + (_end - _start) * _progress;
    public override float TravelProgress => _progress;
    public override float ArcOffset => 4f * _arcHeight * _distance * _progress * (1f - _progress);
    protected override float ArcHeightRatio => _arcHeight;
    public override Vec2 SegmentLaunchPos => _start;
    public override Vec2 SegmentTargetPos => _end;
    public override float LaunchHeight => 0f;
    public override float ImpactHeight => 0f;
    public override float Age => _age;
    public override Vec2 Direction => (_end - _start).Normalized;
    public override int Team => _team;
    public override string MissileArt => _missileArt;
}