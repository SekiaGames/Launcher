using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class RuntimeMonsoonAbility : Ability
{
    readonly War3ObjectData _objects;
    string _rainArt = "";
    string _boltArt = "";
    SpecialEffect _rain;

    public RuntimeMonsoonAbility(War3ObjectData objects) => _objects = objects;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;
    public override IReadOnlyList<string> RuntimePreloadArts
    {
        get
        {
            RefreshArtData();
            var arts = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(_rainArt)) arts.Add(_rainArt);
            if (!string.IsNullOrWhiteSpace(_boltArt)) arts.Add(_boltArt);
            return arts;
        }
    }

    public float Damage => Data('A');
    public float SlowPct => Mathf.Clamp(Data('C'), 0f, 0.9f);
    public float Radius => Area > 0f ? Area : 300f;

    public override float ChannelWaveInterval => Data('B') > 0.2f ? Data('B') : 1.5f;
    public override float ChannelTotal => Duration > 0f ? Duration : 15f;

    public override void OnLevelUp() => RefreshArtData();
    public override void OnLevelDown() => RefreshArtData();

    void RefreshArtData()
    {
        int level = Mathf.Max(1, Level);
        string effectId = RuntimeAbilityFactory.ReadEffectRawcode(ObjectData, level);
        _rainArt = AbilityArt.FirstArt(_objects?.GetBuff(effectId), 1, "Effectart", "");
        string[] boltIds = RuntimeAbilityFactory.ReadString(ObjectData, "BuffID", level)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string boltId = boltIds.Length > 0 ? boltIds[0] : "";
        _boltArt = AbilityArt.FirstArt(_objects?.GetBuff(boltId), 1, "Targetart", "");
    }

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        RefreshArtData();
        ClearRain();
        Vector3 flatPoint = Flatten(point);
        if (!world.BeginChannel(Owner, this, flatPoint, ChannelTotal, ChannelWaveInterval))
            return false;

        if (!string.IsNullOrWhiteSpace(_rainArt))
        {
            _rain = world.AddSpecialEffect(_rainArt, new Vec2(flatPoint.X, flatPoint.Z),
                lifeSpan: ChannelTotal);
            _rain.PlayStandAfterBirth = true;
        }
        return true;
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius))
        {
            if (!string.IsNullOrWhiteSpace(_boltArt))
                world.AddSpecialEffectTarget(_boltArt, victim, "origin", lifeSpan: 1f);
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage,
                AttackType.Spells, DamageType.Magic));
            if (victim.Alive && SlowPct > 0f) victim.ApplySlow(SlowPct, 0f, 2f);
        }
    }

    public override void OnChannelEnded(World world, bool interrupted) => ClearRain();

    void ClearRain()
    {
        _rain?.Destroy();
        _rain = null;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 500f)) return false;
        return world.TryCast(Owner, this, null, Owner.Target?.Position ?? Owner.Position);
    }
}
