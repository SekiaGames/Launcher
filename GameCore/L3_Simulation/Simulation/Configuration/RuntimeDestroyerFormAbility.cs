using Godot;

namespace Launcher;

public sealed class TwoWayForm : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public string FormAId => (Type?.DataTextAt(Level, 'A') ?? "").Trim();

    public string FormBId
    {
        get
        {
            string unit = Type?.SummonAt(Level) ?? "";
            if (!string.IsNullOrWhiteSpace(unit)) return unit.Trim();
            return (Type?.DataTextAt(Level, 'B') ?? "").Trim();
        }
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner?.Type == null || !Owner.Alive) return false;

        bool inA = string.Equals(Owner.Type.Id, FormAId, System.StringComparison.OrdinalIgnoreCase);
        UnitType form = UnitFactory.Get(inA ? FormBId : FormAId);
        return form != null && world.BeginMorph(Owner, form, 0f, disable: false);
    }

    public override bool TryAutoCast(World world) => false;
}
