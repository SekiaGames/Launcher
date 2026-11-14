namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private void UpdateUnitAnimation()
    {
        for (int index = 0; index < _allUnits.Count; index++)
        {
            Unit unit = _allUnits[index];
            if (unit?.View == null) continue;

            if (!unit.Alive)
            {
                UpdateDeathAnimation(unit);
                continue;
            }

            if (unit.IsUnderConstruction) continue;
            UpdateUpgradeAnimation(unit);
        }
    }

    private static void UpdateDeathAnimation(Unit unit)
    {
        UnitSceneComponent view = unit.View;
        if (view?.Root == null || unit.DeathPresentationGone) return;

        if (unit.IsHero)
        {
            if (unit.DeathElapsed < unit.DeathAnimTime)
            {
                view.PlayTimed("Death", unit.DeathAnimTime);
                return;
            }
            view.PlayTimed("Dissipate", 0f);
            return;
        }

        if (unit.IsBone)
        {
            view.PlayTimed("Decay Bone", unit.BoneDecayTime);
            return;
        }

        if (unit.DeathElapsed >= unit.DeathAnimTime)
        {
            view.PlayTimed("Decay Flesh", unit.DecayTime);
            return;
        }

        view.PlayTimed("Death", unit.DeathAnimTime);
    }

    private void UpdateUpgradeAnimation(Unit building)
    {
        if (!building.IsBuilding || _orders.Count == 0) return;

        for (int index = 0; index < _orders.Count; index++)
        {
            BuildingOrder order = _orders[index];
            if (order.Kind != OrderKind.Upgrade || !ReferenceEquals(order.Building, building)) continue;
            float progress = order.Total <= 0f ? 1f : 1f - order.Remaining / order.Total;
            building.View.SetUpgradeProgress(progress);
            return;
        }
    }
}
