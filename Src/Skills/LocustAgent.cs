using Godot;

namespace Launcher;

/// <summary>蝗虫代理：把打出的伤害存起来，蝗虫消失时按 DataD 比例回给施法者（RuntimeLocustSwarmAbility）。</summary>
public sealed class LocustAgent : Ability
{
    public Unit Caster;
    public float Factor;

    private float _stored;

    public override bool IsPassive => true;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (Caster == null || !Caster.Alive || info == null) return;
        _stored += info.Amount * Factor;
    }

    public override void OnOwnerDeath(World world)
    {
        if (Caster == null || !Caster.Alive || Mathf.Abs(_stored) < 0.5f) return;
        if (_stored > 0f) Caster.Heal(_stored);
        else Caster.Hp = Mathf.Max(1f, Caster.Hp + _stored);
        _stored = 0f;
    }
}
