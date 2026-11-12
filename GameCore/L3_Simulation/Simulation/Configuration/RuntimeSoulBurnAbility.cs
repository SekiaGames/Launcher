using Godot;

namespace Launcher;

public sealed class RuntimeSoulBurnAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float DamagePerSecond => Data('A');
    public float SilenceSeconds => Data('C') > 0f ? Data('C') : 0.5f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        if (seconds <= 0f) seconds = 14f;

        world.ApplyBuff(target, new VenomBuff
        {
            DamageSource = Owner,
            DamagePerTick = DamagePerSecond,
            Interval = 1f,
        }, seconds);
        target.State.Add(StateMod.Silence, seconds * SilenceSeconds);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
