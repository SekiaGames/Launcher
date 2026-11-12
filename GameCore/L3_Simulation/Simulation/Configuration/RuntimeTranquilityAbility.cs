using Godot;

namespace Launcher;

/// <summary>
/// 宁静 AEtq（丛林守护者终极）。引导 HeroDur(30) 秒，每秒给 Area(900) 内友军回 DataA(20) 生命。
/// 物编：Cool=60 Cost=125。
/// </summary>
public sealed class RuntimeTranquilityAbility : Ability
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

    public override bool TryAutoCast(World world)
    {
        if (Owner == null) return false;

        float missing = 0f;
        int fighting = 0;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Area))
        {
            if (ally.Hp >= ally.MaxHp) continue;
            missing += ally.MaxHp - ally.Hp;
            if (ally.IsBuilding) continue;
            if (!IsEngaged(world, ally)) continue;
            fighting++;
        }

        if (fighting < 3 || missing < Data('A') * 2f) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }

    private static bool IsEngaged(World world, Unit ally)
    {
        if (ally.Target != null && ally.Target.Alive) return true;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive) continue;
            if (world.AreAllied(ally.Player, other.Player)) continue;
            if (ReferenceEquals(other.Target, ally)) return true;
        }
        return false;
    }
}
