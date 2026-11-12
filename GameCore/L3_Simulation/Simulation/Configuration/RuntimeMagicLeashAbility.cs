using Godot;

namespace Launcher;

public sealed class AerialShackles : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;
    public override bool IsChanneled => true;

    public float DamagePerSecond => Data('A');
    public override float ChannelWaveInterval => 1f;
    public override float ChannelTotal => EffectiveDuration > 0f ? EffectiveDuration : 40f;

    private Unit _target;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.Type == null || !target.Type.Flying) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        _target = target;
        return world.BeginChannel(Owner, this, target.Position, ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null || _target == null || !_target.Alive) return;
        if (Owner.DistanceTo(_target) > 800f) return;

        world.DealDamage(World.MakeAbilityDamage(Owner, _target, DamagePerSecond,
            AttackType.Spells, DamageType.Magic));
    }

    public override void OnChannelEnded(World world, bool interrupted) => _target = null;
}
