using Godot;

namespace Launcher;

public sealed class RuntimeDefendAbility : Ability
{
    public override bool IsToggle => true;

    public float RangedDamageReduction => Data('A');
    public float MoveSlowPct => Data('C');

    private float _appliedSlow;

    public override void OnToggle(World world, bool on)
    {
        if (Owner?.Type == null) return;

        if (on)
        {
            _appliedSlow = Owner.Type.Speed * Mathf.Clamp(MoveSlowPct, 0f, 0.9f);
            Owner.BonusMoveSpeedFlat -= _appliedSlow;
        }
        else
        {
            Owner.BonusMoveSpeedFlat += _appliedSlow;
            _appliedSlow = 0f;
        }
    }

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (!ToggledOn || info == null || !info.Ranged || info.Amount <= 0f) return;
        info.Amount *= 1f - Mathf.Clamp(RangedDamageReduction, 0f, 0.95f);
    }
}
