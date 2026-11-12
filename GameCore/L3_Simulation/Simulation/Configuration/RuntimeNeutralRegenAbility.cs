using Godot;

namespace Launcher;

public sealed class NeutralRegen : Ability
{
    public override bool IsPassive => true;

    public float RegenRate => Data('A') > 0f ? Data('A') : 0.01f;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive) return;

        bool manaOnly = string.Equals(Type?.Id, "ANre", System.StringComparison.OrdinalIgnoreCase);
        if (!manaOnly && Owner.Hp < Owner.MaxHp)
        {
            Owner.Hp = Mathf.Min(Owner.MaxHp, Owner.Hp + Owner.MaxHp * RegenRate * delta);
        }
        if (Owner.MaxMana > 0f && Owner.Mana < Owner.MaxMana)
        {
            Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + Owner.MaxMana * RegenRate * delta);
        }
    }
}
