using Godot;

namespace Launcher;

/// <summary>
/// 吸魔 AHdr（血法师）。对标 Wiki RuntimeDrainAbility：
/// 引导 Dur(6) 秒，每 DataC(1) 秒从敌方目标抽 DataB(15/30/45) 点法力转给施法者；
/// 对友军反向用：每秒把施法者 DataE(30/60/90) 点法力送过去。目标走远或死亡就断。
/// 物编 Cool=6 Cost=10 Rng=600 Area=800（Area 是拉扯上限）。
/// </summary>
public sealed class SiphonMana : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;
    public override bool IsChanneled => true;

    public float DrainInterval => Data('C') > 0f ? Data('C') : 1f;
    public float ManaPerTick => Data('B');
    public float GivePerTick => Data('E');
    public float TetherRange => Area > 0f ? Area : 800f;
    public override float ChannelWaveInterval => DrainInterval;
    public override float ChannelTotal => Duration > 0f ? Duration : 6f;

    private Unit _drainTarget;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || !InRange(target)) return false;

        if (world.AreAllied(Owner.Player, target.Player))
        {
            return Owner.Mana > 0f && target.MaxMana > 0f && target.Mana < target.MaxMana;
        }
        return target.Mana > 0f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null) return false;

        _drainTarget = target;
        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : ChannelTotal;
        return world.BeginChannel(Owner, this, target.Position, seconds, DrainInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        Unit target = _drainTarget;
        if (target == null || !target.Alive || FlatDistance(Owner.Position, target.Position) > TetherRange + target.Radius)
        {
            _drainTarget = null;
            world.InterruptChannel(Owner);
            return;
        }

        if (!world.AreAllied(Owner.Player, target.Player))
        {
            float drained = Mathf.Min(target.Mana, ManaPerTick);
            target.Mana -= drained;
            Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + drained);
            return;
        }

        float given = Mathf.Min(Owner.Mana, GivePerTick);
        float room = Mathf.Max(0f, target.MaxMana - target.Mana);
        float moved = Mathf.Min(given, room);
        Owner.Mana -= moved;
        target.Mana += moved;
    }

    /// <summary>AI：只对「有蓝的敌人」用（抽不到蓝等于白占引导）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => other.Mana >= 20f);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
