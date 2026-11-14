using Godot;

using OpenWC3.Game;
namespace OpenWC3;

public sealed partial class World
{
    private void StepRepair(Unit worker)
    {
        Unit target = worker.RepairTarget;
        if (target == null || !target.Alive || target.Hp >= target.MaxHp || worker.Target != null)
        {
            ClearRepair(worker);
            return;
        }

        float reach = target.Type != null
            ? target.Type.FootprintRadius + Mathf.Max(8f, worker.Radius) + 8f
            : 160f;
        if (!Advance(worker, target.Position, reach))
        {
            worker.Repairing = false;
            return;
        }

        worker.Repairing = true;
        worker.View?.Play("Stand Work");
        FaceTarget(worker, target);
    }

    private void TickRepairs()
    {
        for (int index = 0; index < Units.Count; index++)
        {
            Unit worker = Units[index];
            if (worker == null || !worker.Alive || worker.RepairTarget == null || !worker.Repairing) continue;

            Unit target = worker.RepairTarget;
            if (!target.Alive || target.Hp >= target.MaxHp)
            {
                ClearRepair(worker);
                continue;
            }

            RuntimeRepairAbility ability = FindRepairAbility(worker);
            if (ability == null)
            {
                ClearRepair(worker);
                continue;
            }

            float amount = ability.RepairPerSecond(target) * FrameSeconds;
            if (amount <= 0f) continue;

            worker.RepairGoldDebt += amount * ability.GoldPerHitPoint;
            int cost = Mathf.FloorToInt(worker.RepairGoldDebt);
            if (cost > 0)
            {
                if (TrySpendGold == null || !TrySpendGold(worker.Player, cost))
                {
                    ClearRepair(worker);
                    continue;
                }
                worker.RepairGoldDebt -= cost;
            }

            target.Hp = Mathf.Min(target.MaxHp, target.Hp + amount);

            if (target.Hp >= target.MaxHp) ClearRepair(worker);
        }
    }

    private static RuntimeRepairAbility FindRepairAbility(Unit worker)
    {
        foreach (Ability ability in worker.Abilities)
        {
            if (ability is RuntimeRepairAbility repair && repair.Learned) return repair;
        }
        return null;
    }

    private static void ClearRepair(Unit worker)
    {
        if (worker == null) return;
        worker.RepairTarget = null;
        worker.Repairing = false;
        worker.RepairGoldDebt = 0f;
        worker.View?.Play("Stand");
    }
}