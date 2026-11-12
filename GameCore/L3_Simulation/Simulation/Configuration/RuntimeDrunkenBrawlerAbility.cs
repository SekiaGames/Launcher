using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class RuntimeDrunkenBrawlerAbility : Ability
{
    public override bool IsPassive => true;

    public float CriticalPercent => Data('A');
    public float CriticalMultiplier => Data('B') > 0f ? Data('B') : 2f;
    public float EvasionChance => Mathf.Clamp(Data('D'), 0f, 0.95f);

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (world == null || CriticalPercent <= 0f) return raw;
        if (!world.RollPercent(CriticalPercent)) return raw;
        return raw * CriticalMultiplier;
    }

    public override bool CanBeHitBy(World world, DamageData info)
    {
        if (info == null || !info.IsAttack || EvasionChance <= 0f) return true;
        return !world.RollPercent(EvasionChance * 100f);
    }
}
