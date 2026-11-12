using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

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
