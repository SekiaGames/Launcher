using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 驻军（兽族地洞）：把苦工塞进地洞藏起来，地洞里有人才开火。
/// 对标客户端 CargoTransport + RuntimeBunkerAbility 的最小口径：
/// 装载时把乘客从世界单位表里摘掉（不参与索敌、伤害、寻路），卸货时按螺旋摆回建筑周围。
/// </summary>
public sealed partial class World
{
    private const float CargoDropRadius = 52f;

    /// <summary>能不能把 passenger 装进 holder：同队、活着、非建筑非英雄；地洞只收农民。</summary>
    public bool CanEnterCargo(Unit holder, Unit passenger)
    {
        if (holder == null || passenger == null || holder == passenger) return false;
        if (!holder.Alive || !passenger.Alive || passenger.IsBuilding || passenger.IsHero) return false;
        if (passenger.CargoHolder != null) return false;
        if (passenger.Player != holder.Player) return false;
        if (holder.CargoCapacity <= 0 || holder.Cargo.Count >= holder.CargoCapacity) return false;
        return !holder.GarrisonOnly || passenger.IsWorker;
    }

    /// <summary>把半径内最近的几个能装的单位挨个装进去，返回装了几个。</summary>
    public int LoadNearbyWorkers(Unit holder, float radius)
    {
        if (holder == null || holder.CargoCapacity <= 0) return 0;

        var candidates = new List<Unit>();
        foreach (Unit unit in Units)
        {
            if (!CanEnterCargo(holder, unit)) continue;
            if (holder.DistanceTo(unit) > radius) continue;
            candidates.Add(unit);
        }
        candidates.Sort((left, right) => holder.DistanceTo(left).CompareTo(holder.DistanceTo(right)));

        int loaded = 0;
        foreach (Unit passenger in candidates)
        {
            if (holder.Cargo.Count >= holder.CargoCapacity) break;
            if (!LoadCargo(holder, passenger)) continue;
            loaded++;
        }
        return loaded;
    }

    /// <summary>装载：乘客从世界单位表摘掉、模型隐藏、位置贴到载具身上。</summary>
    public bool LoadCargo(Unit holder, Unit passenger)
    {
        if (!CanEnterCargo(holder, passenger)) return false;

        passenger.CargoHolder = holder;
        passenger.Target = null;
        passenger.MoveTarget = null;
        passenger.Tree = null;
        passenger.GoldMine = null;
        passenger.BuildSite = null;
        passenger.RepairTarget = null;
        passenger.Returning = false;
        passenger.CancelAttack();
        passenger.Position = holder.Position;
        if (passenger.View != null)
        {
            passenger.View.Root.Visible = false;
            passenger.View.Root.Position = holder.Position;
        }

        Units.Remove(passenger);
        holder.Cargo.Add(passenger);
        return true;
    }

    /// <summary>卸货：全部摆回建筑周围，重新进世界单位表。</summary>
    public bool UnloadAllCargo(Unit holder)
    {
        if (holder == null || holder.Cargo.Count == 0) return false;

        for (int index = 0; index < holder.Cargo.Count; index++)
        {
            PlaceCargo(holder, holder.Cargo[index], index);
        }
        holder.Cargo.Clear();
        return true;
    }

    /// <summary>载具死了：肚子里的人就地倒出来（不能把人一起带走）。</summary>
    public void ReleaseCargoOnDeath(Unit holder)
    {
        if (holder != null && holder.Cargo.Count > 0) UnloadAllCargo(holder);
    }

    private void PlaceCargo(Unit holder, Unit passenger, int slot)
    {
        if (passenger == null) return;

        passenger.CargoHolder = null;
        float angle = slot * 2.399963f;
        Vector3 spot = holder.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * CargoDropRadius;
        passenger.Position = spot;
        passenger.Home = holder.Home;
        passenger.CancelAttack();
        passenger.Target = null;
        passenger.MoveTarget = null;

        if (passenger.View != null)
        {
            passenger.View.Root.Position = spot;
            passenger.View.Root.Visible = true;
            passenger.View.Play("Stand");
        }

        if (!Units.Contains(passenger)) Units.Add(passenger);
    }
}