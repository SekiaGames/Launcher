using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class RuntimeCloudOfFogAbility : Ability
{
    readonly War3ObjectData _objects;
    string _cloudArt = "";

    public RuntimeCloudOfFogAbility(War3ObjectData objects) => _objects = objects;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;
    public override IReadOnlyList<string> RuntimePreloadArts
    {
        get
        {
            RefreshArtData();
            return string.IsNullOrWhiteSpace(_cloudArt) ? Array.Empty<string>() : new[] { _cloudArt };
        }
    }

    public float Radius => Area > 0f ? Area : 300f;
    public float Lifetime => Duration > 0f ? Duration : 30f;
    public float MissChance => 0.7f;

    public override void OnLevelUp() => RefreshArtData();
    public override void OnLevelDown() => RefreshArtData();

    void RefreshArtData()
    {
        string effectId = RuntimeAbilityFactory.ReadEffectRawcode(ObjectData, Mathf.Max(1, Level));
        _cloudArt = AbilityArt.FirstArt(_objects?.GetBuff(effectId), 1, "Effectart", "");
    }

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        RefreshArtData();
        Vector3 position = Flatten(point);
        var spell = new CloudOfFogSpell
        {
            Caster = Owner,
            Position = position,
            Radius = Radius,
            Remaining = Lifetime,
            MissChance = MissChance,
        };
        if (!string.IsNullOrWhiteSpace(_cloudArt))
        {
            spell.Cloud = world.AddSpecialEffect(_cloudArt, new Vec2(position.X, position.Z),
                lifeSpan: Lifetime);
            spell.Cloud.PlayStandAfterBirth = true;
            spell.Cloud.TeamColorIndex = Owner.TeamColorIndex;
        }
        world.AddGroundSpell(spell);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, CastRange > 0f ? CastRange : 1000f)) return false;

        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, CastRange > 0f ? CastRange : 1000f))
        {
            if (enemy.Type == null || !enemy.Type.Flying) continue;
            return world.TryCast(Owner, this, null, enemy.Position);
        }
        return false;
    }
}
