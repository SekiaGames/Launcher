using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class RuntimeSentinelAbility : Ability
{
    readonly War3ObjectData _objects;
    string _owlArt = "";

    public RuntimeSentinelAbility(War3ObjectData objects) => _objects = objects;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;
    public override IReadOnlyList<string> RuntimePreloadArts
    {
        get
        {
            RefreshArtData();
            return string.IsNullOrWhiteSpace(_owlArt) ? Array.Empty<string>() : new[] { _owlArt };
        }
    }

    public float SightRadius => Data('B') > 0f ? Data('B') : 900f;
    public float Lifetime => Data('A') > 0f ? Data('A') : 100f;
    public float Height => Data('C');

    public override void OnLevelUp() => RefreshArtData();
    public override void OnLevelDown() => RefreshArtData();

    void RefreshArtData()
    {
        string effectId = RuntimeAbilityFactory.ReadEffectRawcode(ObjectData, Mathf.Max(1, Level));
        IRuntimeObjectData data = _objects?.GetBuff(effectId);
        data ??= War3ObjectData.GetAbility(effectId);
        _owlArt = AbilityArt.FirstArt(data, 1, "Targetart", "");
    }

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        RefreshArtData();
        Vector3 position = Flatten(point);
        world.AddFogReveal(Owner.Player, position, SightRadius, Lifetime);
        if (!string.IsNullOrWhiteSpace(_owlArt))
            world.AddSpecialEffect(_owlArt, new Vec2(position.X, position.Z),
                lifeSpan: Lifetime, height: Height);
        return true;
    }
}
