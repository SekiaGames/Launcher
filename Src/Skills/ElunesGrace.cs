using Godot;

namespace Launcher;

public sealed class ElunesGrace : Ability
{
    public override bool IsPassive => true;

    public float Reduction => Mathf.Clamp(Data('A'), 0f, 1f);

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (Owner == null || info == null || !info.Ranged) return;
        if (info.DamageType != DamageType.Normal) return;

        info.Amount *= 1f - Reduction;
    }
}
