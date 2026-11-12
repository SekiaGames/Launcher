namespace OpenWC3;

/// <summary>
/// Persistent attack stat effects shared by combat and the unit panel.
/// Implementations must be pure: no random rolls, resource spending, target
/// reactions or other state changes. Conditional hits and illusion damage
/// factors are not displayed attack stats.
/// </summary>
public interface IAttackDamageStatModifier
{
    void ModifyOutgoingDamage(ref DamageData damage);
}
