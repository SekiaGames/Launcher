namespace OpenWC3;

/// <summary>一次伤害的完整上下文，供技能/物品/光环在结算前后挂钩。</summary>
public sealed class DamageData
{
    public Unit Source;
    public Unit Target;

    public float Raw;
    public float Amount;

    /// <summary>附加伤害（客户端 bonusDamage，监听器可叠加）。</summary>
    public float BonusAmount;

    /// <summary>伤害倍率（客户端 DamageModResult.damageMultiplier）。</summary>
    public float DamageMultiplier = 1f;

    public bool IsRanged
    {
        get => Ranged;
        set => Ranged = value;
    }

    /// <summary>开火用的那套武器（第一或第二套），溅射与命中音都按它来。</summary>
    public Weapon Weapon;
    public AttackType AttackType = AttackType.Normal;
    public DefenseType DefenseType = DefenseType.Unarmored;
    public DamageType DamageType = DamageType.Normal;

    public float Armor;
    public float ThornsBase;
    public float ArmorFactor = 1f;
    public float ClassRatio = 1f;
    public bool UnderConstruction;
    public bool Ethereal;
    public bool IsAttack = true;
    /// <summary>是否远程攻击（武器带弹道）：艾露恩恩泽、尖刺路障、硬化皮肤按它分流。</summary>
    public bool Ranged;
    public bool Cancelled;

    /// <summary>必中：跳过闪避与命中判定（客户端 DamageData.CannotMiss）。</summary>
    public bool CannotMiss;
    /// <summary>反伤造成的二次伤害：不再触发反伤与吸血，避免互相反弹。</summary>
    public bool Reflected;
    /// <summary>这一下被致命一击放大过：飘字用红色大字加感叹号。</summary>
    public bool IsCritical;
}
