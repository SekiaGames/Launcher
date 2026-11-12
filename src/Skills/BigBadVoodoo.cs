using Godot;

namespace Launcher;

/// <summary>
/// 巫毒 AOvd（暗影猎手终极）。引导 HeroDur(30) 秒，每秒把 Area(800) 内友军刷成无敌，
/// **施法者自己不无敌**（原版口径）。物编：Cool=180 Cost=200。
/// </summary>
public sealed class BigBadVoodoo : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool IsChanneled => true;
    public override float ChannelWaveInterval => 0.5f;
    public override float ChannelTotal => EffectiveDuration;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Owner.Position, ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        // 客户端 RuntimeVoodooBuff：每 0.5s 扫描一次，单次护盾 1s，无敌 + 魔免
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Area))
        {
            if (ReferenceEquals(ally, Owner) || ally.IsChanneling) continue;
            ally.State.Add(StateMod.Invulnerable, 1f);
            ally.State.Add(StateMod.MagicImmune, 1f);
        }
    }

    /// <summary>AI：附近有敌人在打、并且身边有残血友军时才跳。</summary>
    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Area)) return false;

        int wounded = 0;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Area))
        {
            if (ally.Hp / Mathf.Max(1f, ally.MaxHp) > 0.75f) continue;
            wounded++;
        }
        return wounded >= 2 && world.TryCast(Owner, this, null, Owner.Position);
    }
}
