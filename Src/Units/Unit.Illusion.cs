using Godot;

namespace OpenWC3;

public sealed partial class Unit
{
    /// <summary>幻象（镜像分身）：造成伤害按 IllusionDamageDealt 缩放、受到伤害按 IllusionDamageReceived 放大。</summary>
    public bool IsIllusion;
    public float IllusionDamageDealt = 1f;
    public float IllusionDamageReceived = 1f;
}