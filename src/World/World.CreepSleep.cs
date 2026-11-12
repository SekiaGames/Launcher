using Godot;

namespace Launcher;

public sealed partial class World
{
    private const float CreepWakeRadius = 500f;

    public void TickCreepSleep()
    {
        for (int index = 0; index < Units.Count; index++)
        {
            Unit unit = Units[index];
            if (unit == null || !unit.Alive || !unit.IsCreepSleeper) continue;

            bool enemyNear = false;
            foreach (Unit other in EnemiesInRange(unit, unit.Position, CreepWakeRadius, false))
            {
                if (!other.Alive || other.State.Invisible) continue;
                enemyNear = true;
                break;
            }

            bool shouldSleep = !IsNight && !enemyNear && unit.Target == null;
            if (shouldSleep)
            {
                if (!unit.State.Has(StateMod.Sleep)) unit.State.Add(StateMod.Sleep);
            }
            else if (unit.State.Has(StateMod.Sleep))
            {
                unit.State.RemoveAll(StateMod.Sleep);
            }
        }
    }
}