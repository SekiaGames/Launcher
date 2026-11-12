using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>缓慢光环（龙卷风 Aasl）：DataA/DataB 是带符号的移动/攻击速度增量，对范围内敌人持续刷新减速。</summary>
public sealed class RuntimeSlowAuraAbility : Ability
{
    public override bool IsPassive => true;

    private float _pulseTimer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        _pulseTimer -= delta;
        if (_pulseTimer > 0f) return;
        _pulseTimer = 0.5f;

        float move = Mathf.Clamp(-Data('A'), 0f, 0.95f);
        float attack = Mathf.Clamp(-Data('B'), 0f, 0.95f);
        if (move <= 0f && attack <= 0f) return;

        float radius = Area > 0f ? Area : 600f;
        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, radius))
        {
            if (victim.IsBuilding) continue;
            victim.ApplySlow(move, attack, 0.6f);
        }
    }
}
