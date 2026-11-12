using Godot;

namespace Launcher;

/// <summary>
/// 巫毒 AOvd（暗影猎手终极）。引导 HeroDur(30) 秒，每秒把 Area(800) 内友军刷成无敌，
/// **施法者自己不无敌**（原版口径）。物编：Cool=180 Cost=200。
/// </summary>
public sealed class RuntimeBigBadVoodooAbility : Ability
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

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.MaxHp <= 0f) return false;
        if (Owner.Hp / Owner.MaxHp < 0.4f) return false;

        int fightingAllies = 0;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Area))
        {
            if (ReferenceEquals(ally, Owner) || ally.IsBuilding) continue;
            if (ally.Target == null || !ally.Target.Alive) continue;
            fightingAllies++;
        }
        if (fightingAllies < 3) return false;

        int enemies = NearbyEnemyCount(world, Area, out _);
        if (enemies < 3) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
