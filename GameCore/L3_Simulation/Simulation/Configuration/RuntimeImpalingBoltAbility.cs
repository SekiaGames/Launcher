using Godot;

namespace Launcher;

public sealed class ImpalingBolt : Ability
{
    public override bool IsPassive => true;

    public float Ratio => 0.5f;
    public float Width => 120f;
    public float Length => 900f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || info.Amount <= 0f) return;

        Vector3 forward = info.Target.Position - Owner.Position;
        forward.Y = 0f;
        if (forward.Length() < 1f) return;
        forward = forward.Normalized();

        foreach (Unit victim in world.EnemiesInRange(Owner, info.Target.Position, 300f))
        {
            if (victim == info.Target) continue;

            Vector3 offset = victim.Position - Owner.Position;
            offset.Y = 0f;
            float along = offset.Dot(forward);
            if (along < 0f || along > Length) continue;
            if ((offset - forward * along).Length() > Width) continue;

            world.DealDamage(World.MakeAbilityDamage(Owner, victim, info.Amount * Ratio,
                AttackType.Pierce, DamageType.Normal));
        }
    }
}
