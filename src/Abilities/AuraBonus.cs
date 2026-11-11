using Godot;

namespace Launcher;

/// <summary>
/// 一条光环的加成集合。对标客户端 Src/Skills/AuraAbility 的 Buff 数值：
/// 每个字段由不同光环填（专注→Armor、荆棘→ThornsPct、耐久→攻速/移速、辉煌→回蓝…），
/// World.RefreshAuras 每 tick 按「同源取最大」合并到单位身上。
/// </summary>
public struct AuraBonus
{
    public float Armor;
    public float ThornsPct;
    public float LifestealPct;
    public float RangedDamagePct;
    public float AttackSpeedPct;
    public float MoveSpeedPct;
    public float DamagePct;
    public float HpRegenPerSecond;
    public float HpRegenPct;
    public float ManaRegenPerSecond;

    public bool IsZero =>
        Armor == 0f && ThornsPct == 0f && LifestealPct == 0f && RangedDamagePct == 0f
        && AttackSpeedPct == 0f && MoveSpeedPct == 0f && DamagePct == 0f
        && HpRegenPerSecond == 0f && HpRegenPct == 0f && ManaRegenPerSecond == 0f;

    /// <summary>合并两条光环：逐项取大，互不覆盖。</summary>
    public void MergeFrom(AuraBonus other)
    {
        Armor = Mathf.Max(Armor, other.Armor);
        ThornsPct = Mathf.Max(ThornsPct, other.ThornsPct);
        LifestealPct = Mathf.Max(LifestealPct, other.LifestealPct);
        RangedDamagePct = Mathf.Max(RangedDamagePct, other.RangedDamagePct);
        AttackSpeedPct = Mathf.Max(AttackSpeedPct, other.AttackSpeedPct);
        MoveSpeedPct = Mathf.Max(MoveSpeedPct, other.MoveSpeedPct);
        DamagePct = Mathf.Max(DamagePct, other.DamagePct);
        HpRegenPerSecond = Mathf.Max(HpRegenPerSecond, other.HpRegenPerSecond);
        HpRegenPct = Mathf.Max(HpRegenPct, other.HpRegenPct);
        ManaRegenPerSecond = Mathf.Max(ManaRegenPerSecond, other.ManaRegenPerSecond);
    }
}
