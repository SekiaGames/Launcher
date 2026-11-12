using Godot;
using System;

namespace Launcher;

/// <summary>
/// 纳迦潜水（Asb1 穆戈尔 / Asb2 皇家卫兵 / Asb3 飞龙）。形态切换与德鲁伊同源
/// （DataA 是水面形态、UnitID 是潜水形态），差异只是潜水时隐形且不能攻击，再按一次浮出水面。
/// </summary>
public sealed class RuntimeSubmergeAbility : RuntimeDruidFormAbility
{
    public bool Underwater => Owner?.Type != null
        && string.Equals(Owner.Type.Id, MorphFormId, StringComparison.OrdinalIgnoreCase);

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        bool cast = base.OnCast(world, target, point);
        if (cast) ApplyState();
        return cast;
    }

    public override void OnWorldTick(World world, float delta) => ApplyState();

    public override bool TryAutoCast(World world) => false;

    private void ApplyState()
    {
        if (Owner == null) return;

        if (Underwater)
        {
            if (!Owner.State.Has(StateMod.Invisible)) Owner.State.Add(StateMod.Invisible);
            if (!Owner.State.Has(StateMod.Disarm)) Owner.State.Add(StateMod.Disarm);
            return;
        }

        if (Owner.State.Has(StateMod.Invisible)) Owner.State.Remove(StateMod.Invisible);
        if (Owner.State.Has(StateMod.Disarm)) Owner.State.Remove(StateMod.Disarm);
    }
}
