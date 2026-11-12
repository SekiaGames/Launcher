using Godot;

namespace Launcher;

public sealed class ManaFlare : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 750f;
    public float ManaCostPct => Data('A') > 0f ? Mathf.Clamp(Data('A') * 0.1f, 0f, 1f) : 0.3f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new ManaFlareBuff
        {
            DamageSource = Owner,
            Radius = Radius,
            ManaCostPct = ManaCostPct,
        }, Duration > 0f ? Duration : 30f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner.Mana < ManaCost) return false;
        if (!HasEnemyNearby(world, Radius)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
