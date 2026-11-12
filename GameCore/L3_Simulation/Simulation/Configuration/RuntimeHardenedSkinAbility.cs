using Godot;

namespace Launcher;

public sealed class RuntimeHardenedSkinAbility : Ability
{
    public override bool IsPassive => true;

    public float MinDamage => Data('B');
    public float Reduction => Data('C');
    /// <summary>物编 DataA：触发概率百分比（山岭巨人 / 龙龟都是 100，即必发）。</summary>
    public float ChancePercent => Mathf.Clamp(Data('A'), 0f, 100f);

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (Owner == null || info == null || info.DamageType != DamageType.Normal) return;
        if (info.Source == null) return;

        bool allowed = info.Ranged ? Data('D') > 0f : Data('E') > 0f;
        if (!allowed) return;
        if (ChancePercent < 100f && (world == null || !world.RollPercent(ChancePercent))) return;

        info.Amount = Reduction < info.Amount ? info.Amount - Reduction : MinDamage;
    }
}
