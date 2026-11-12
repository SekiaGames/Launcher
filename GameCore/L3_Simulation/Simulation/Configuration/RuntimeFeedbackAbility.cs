using Godot;

namespace Launcher;

public sealed class Feedback : Ability
{
    public override bool IsPassive => true;

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (Owner == null || target == null || !target.Alive) return raw;
        if (target.IsBuilding || target.Mana <= 0f) return raw;

        float limit = target.IsHero ? Data('C') : Data('A');
        float ratio = target.IsHero ? Data('D') : Data('B');
        float drained = Mathf.Min(target.Mana, limit);
        if (drained <= 0f) return raw;

        target.Mana -= drained;
        float bonus = drained * Mathf.Max(0f, ratio);
        if (target.IsSummon) bonus += Data('E');
        return raw + bonus;
    }
}
