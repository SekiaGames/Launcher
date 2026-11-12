using Godot;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>卸货（Amed）：把货舱里的尸体全倒在车周围。</summary>
public sealed class RuntimeMeatDropAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        RuntimeMeatLoadAbility cargo = Owner?.FindPassive<RuntimeMeatLoadAbility>() as RuntimeMeatLoadAbility;
        if (cargo == null || cargo.CorpseCount == 0) return false;
        cargo.DropAll();
        return true;
    }

    public override bool TryAutoCast(World world) => false;
}
