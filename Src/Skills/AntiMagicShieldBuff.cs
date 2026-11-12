using Godot;

namespace Launcher;

public sealed class AntiMagicShieldBuff : Buff
{
    public Unit Caster;
    public float Absorb;

    public override bool IsHarmful => false;

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (info == null || info.Amount <= 0f || Absorb <= 0f) return;
        if (info.DamageType != DamageType.Magic) return;

        float taken = Mathf.Min(Absorb, info.Amount);
        info.Amount -= taken;
        Absorb -= taken;
        if (Absorb <= 0f) Remaining = 0f;
    }
}
