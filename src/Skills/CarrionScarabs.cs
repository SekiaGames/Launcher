using Godot;

namespace Launcher;

/// <summary>
/// 腐尸甲虫 AUcb（地穴领主）。从目标尸体里爬出 DataA(1) 只甲虫（UnitID=ucs1/ucs2/ucs3），
/// 尸体当场消失；甲虫不限期，直到被杀（DataE=5 是上限，这里暂不做上限）。
/// 物编：Cool=6 Cost=30 Rng=900 Area=900。
/// </summary>
public sealed class CarrionScarabs : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || target.Alive || !target.IsCorpse) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || target.Alive) return false;

        Vector3 center = target.Position;
        world.ConsumeCorpse(target);

        string unitId = Type.DataTextAt(Level, 'C');
        if (string.IsNullOrEmpty(unitId) || unitId == "-") unitId = Type.SummonAt(Level);
        int count = Mathf.Max(1, Mathf.RoundToInt(Data('A')));
        for (int index = 0; index < count; index++)
        {
            float angle = Mathf.Tau * index / count;
            Vector3 offset = new(Mathf.Cos(angle) * 48f, 0f, Mathf.Sin(angle) * 48f);
            world.Summon(Owner, unitId, center + offset, 0f);
        }
        return true;
    }
}
