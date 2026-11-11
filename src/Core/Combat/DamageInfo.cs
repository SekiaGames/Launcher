namespace Launcher;

/// <summary>一次伤害的完整上下文，供技能/物品/光环在结算前后挂钩。</summary>
public sealed class DamageInfo
{
    public Unit Source;
    public Unit Target;

    public float Raw;
    public float Amount;

    /// <summary>开火用的那套武器（第一或第二套），溅射与命中音都按它来。</summary>
    public WeaponProfile Weapon;
    public AttackType AttackType = AttackType.Normal;
    public DefenseType DefenseType = DefenseType.Unarmored;
    public DamageType DamageType = DamageType.Normal;

    public float Armor;
    public float ArmorFactor = 1f;
    public float ClassRatio = 1f;
    public bool UnderConstruction;
    public bool Ethereal;
    public bool IsAttack = true;
    /// <summary>是否远程攻击（武器带弹道）：艾露恩恩泽、尖刺路障、硬化皮肤按它分流。</summary>
    public bool Ranged;
    public bool Cancelled;
    /// <summary>反伤造成的二次伤害：不再触发反伤与吸血，避免互相反弹。</summary>
    public bool Reflected;
}
