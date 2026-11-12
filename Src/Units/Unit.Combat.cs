using Godot;

namespace OpenWC3;

/// <summary>
/// 单位攻击行为：起手 → 伤害点 → 后摇。对标客户端 Units/Unit.Combat.cs 与 Components/WeaponSystem.cs。
/// </summary>
public sealed partial class Unit
{
    /// <summary>距离伤害点还剩多少秒（起手后就进入这个状态）。</summary>
    public float AttackWindup;
    public bool AttackWindupActive;

    /// <summary>后摇剩余；大于 0 时不能再次出手，也不该立刻走开。</summary>
    public float Backswing;

    public Unit WindupTarget;

    /// <summary>本次出手选中的武器（第一或第二套），冷却与伤害都按它算。</summary>
    public Weapon ActiveWeapon;

    public bool ReadyToAttack => !AttackWindupActive && Backswing <= 0f && AttackCooldown <= 0f;

    public void BeginAttack(Unit target, float damagePoint)
    {
        AttackWindupActive = true;
        AttackWindup = Mathf.Max(0.01f, damagePoint);
        WindupTarget = target;
        View?.Play("Attack");
    }

    public void EndWindup()
    {
        AttackWindupActive = false;
        AttackWindup = 0f;
        WindupTarget = null;
    }

    public void CancelAttack()
    {
        AttackWindupActive = false;
        AttackWindup = 0f;
        WindupTarget = null;
        Backswing = 0f;
    }
}
