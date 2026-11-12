using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed class StasisTrapSpell : IGroundSpell
{
    public Unit Caster;
    public Vector3 Position;
    public float TriggerRadius = 250f;
    public float StunRadius = 400f;
    public float StunSeconds = 6f;
    public float Remaining = 150f;

    public bool Step(World world, float delta)
    {
        Remaining -= delta;
        if (Remaining <= 0f || world == null || Caster == null || !Caster.Alive) return false;

        bool triggered = false;
        foreach (Unit enemy in world.EnemiesInRange(Caster, Position, TriggerRadius, false))
        {
            triggered = true;
            break;
        }
        if (!triggered) return true;

        foreach (Unit victim in world.EnemiesInRange(Caster, Position, StunRadius))
        {
            if (victim.IsBuilding) continue;
            victim.State.Add(StateMod.Stun, StunSeconds);
        }
        return false;
    }
}
