using Godot;

using OpenWC3.Game;
namespace OpenWC3;

public sealed class RuntimeCallToArmsAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 2000f;
    public float MilitiaSeconds => 45f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        UnitType militia = UnitFactory.Get("hmil");
        if (militia == null) return false;

        int converted = 0;
        foreach (Unit worker in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (!worker.Alive || worker.IsBuilding || worker.Type == null) continue;
            if (!string.Equals(worker.Type.Id, "hpea", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (world.BeginMorph(worker, militia, MilitiaSeconds, disable: false)) converted++;
        }
        return converted > 0;
    }
}
