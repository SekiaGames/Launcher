using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeCurseAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float MissChance => Mathf.Clamp(Data('A'), 0f, 0.9f);

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.State.MagicImmune) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        world.ApplyBuff(target, new CurseBuff { MissChance = MissChance }, seconds > 0f ? seconds : 60f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true,
            accept: other => !other.IsBuilding && !other.State.MagicImmune && !other.HasBuff(harmful: true));
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
