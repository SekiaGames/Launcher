using Godot;

namespace Launcher;

public sealed class RuntimeSpikedBarricadesAbility : Ability
{
    public override bool IsPassive => true;
    public override float ReflectFlat => Mathf.Max(0f, Data('A'));
}
