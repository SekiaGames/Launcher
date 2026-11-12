using Godot;
using System;

namespace Launcher;

/// <summary>
/// 地精地雷（Amin）：敌人踩进探测半径就引爆自己（死亡后由 Amnx 结算爆炸伤害）。
/// 物编没给触发半径，取爆炸半径同口径的 150。
/// </summary>
public sealed class Mine : Ability
{
    private const float TriggerRadius = 150f;

    public override bool IsPassive => true;

    private float _timer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        _timer -= delta;
        if (_timer > 0f) return;
        _timer = 0.5f;

        foreach (Unit other in world.EnemiesInRange(Owner, Owner.Position, TriggerRadius, hitBuildings: false))
        {
            if (other.Type != null && other.Type.Flying) continue;
            world.KillUnit(Owner, other);
            return;
        }
    }
}
