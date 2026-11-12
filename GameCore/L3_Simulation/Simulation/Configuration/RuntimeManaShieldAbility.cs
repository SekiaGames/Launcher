using Godot;

namespace Launcher;

public sealed class RuntimeManaShieldAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float ManaPerDamage => Data('A') > 0f ? Data('A') : 2f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new RuntimeManaShieldBuff { ManaPerDamage = ManaPerDamage }, Duration > 0f ? Duration : 30f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.Mana <= 0f) return false;
        if (!HasEnemyNearby(world, 700f)) return false;
        if (Owner.FindBuff<RuntimeManaShieldBuff>() != null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
