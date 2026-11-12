using Godot;

namespace Launcher;

public sealed class ShadowMeld : Ability
{
    public override bool IsPassive => true;

    public float Delay => Data('B') > 0f ? Data('B') : 2.5f;

    private float _still;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        bool idle = Owner.Target == null && Owner.MoveTarget == null && !Owner.AttackWindupActive;
        if (!world.IsNight || !idle)
        {
            _still = 0f;
            Owner.State.RemoveAll(StateMod.Invisible);
            return;
        }

        _still += delta;
        if (_still < Delay) return;
        Owner.State.Add(StateMod.Invisible, 0.5f);
    }
}
