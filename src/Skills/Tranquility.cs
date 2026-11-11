using Godot;

namespace Launcher;

/// <summary>
/// 宁静 AEtq（丛林守护者终极）。引导 HeroDur(30) 秒，每秒给 Area(900) 内友军回 DataA(20) 生命。
/// 物编：Cool=60 Cost=125。
/// </summary>
public sealed class Tranquility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool IsChanneled => true;
    public override float ChannelWaveInterval => 1f;
    public override float ChannelTotal => EffectiveDuration;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Owner.Position, ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Area))
        {
            ally.Heal(Data('A'));
        }
    }

    /// <summary>AI：附近至少两个残血友军才开。</summary>
    public override bool TryAutoCast(World world)
    {
        int wounded = 0;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Area))
        {
            if (ally.Hp / Mathf.Max(1f, ally.MaxHp) > 0.7f) continue;
            wounded++;
        }
        return wounded >= 2 && world.TryCast(Owner, this, null, Owner.Position);
    }
}
