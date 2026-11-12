using Godot;

namespace OpenWC3;

public sealed class RuntimeManaShieldBuff : Buff
{
    public float ManaPerDamage = 2f;

    public override bool IsHarmful => false;

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (info == null || info.Amount <= 0f || Owner == null || ManaPerDamage <= 0f) return;
        if (Owner.Mana <= 0f) return;

        float absorbed = Mathf.Min(info.Amount, Owner.Mana / ManaPerDamage);
        Owner.Mana = Mathf.Max(0f, Owner.Mana - absorbed * ManaPerDamage);
        info.Amount -= absorbed;
        if (Owner.Mana <= 0f) Remaining = 0f;
    }
}
