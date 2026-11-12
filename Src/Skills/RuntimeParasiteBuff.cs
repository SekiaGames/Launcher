using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

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
