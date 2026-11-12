using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 尸体货舱（Amel 装尸体 / Amed 卸尸体 / Aexh 掘尸）。对标客户端 RuntimeMeatCargoAbilities：
/// 绞肉车/憎恶（这里是亡灵绞肉车 umtw）把地面尸体收进货舱，尸体的衰减暂停、模型隐藏、位置跟随车体；
/// 卸货时按螺旋摆回车身周围。掘尸（Aexh）每 Dur 秒自己产一具 UnitID 尸体，最多 DataA 具。
/// </summary>
public sealed class MeatCargo : Ability
{
    public const int DefaultCapacity = 5;

    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    private readonly List<Unit> _corpses = new();

    public int Capacity => DefaultCapacity;
    public int CorpseCount => _corpses.Count;
    public float LoadRadius => Area > 0f ? Area : 600f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        if (_corpses.Count >= Capacity) return false;

        Unit corpse = FindCorpse(world);
        if (corpse == null) return false;

        Store(corpse);
        return true;
    }

    public override void OnWorldTick(World world, float delta)
    {
        for (int index = _corpses.Count - 1; index >= 0; index--)
        {
            Unit corpse = _corpses[index];
            if (corpse == null || corpse.Alive || !corpse.IsCorpse || corpse.CorpseCarrier != Owner)
            {
                _corpses.RemoveAt(index);
                continue;
            }

            corpse.Position = Owner.Position;
            if (corpse.View != null) corpse.View.Root.Position = Owner.Position;
        }
    }

    public override void OnOwnerDeath(World world) => DropAll();

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !Owner.Alive || _corpses.Count >= Capacity) return false;
        if (HasEnemyNearby(world, 700f)) return false;
        if (FindCorpse(world) == null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }

    public Unit FindCorpse(World world)
    {
        Unit best = null;
        float bestDistance = LoadRadius;
        foreach (Unit other in world.Units)
        {
            if (!CanStore(other)) continue;
            float distance = Owner.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        return best;
    }

    public bool CanStore(Unit corpse)
    {
        if (Owner == null || corpse == null || corpse.Alive || !corpse.IsCorpse) return false;
        if (corpse.IsHero || corpse.IsBuilding || corpse.CorpseCarrier != null) return false;
        return Owner.DistanceTo(corpse) <= LoadRadius;
    }

    public bool Store(Unit corpse)
    {
        if (!CanStore(corpse) || _corpses.Count >= Capacity) return false;

        corpse.CorpseCarrier = Owner;
        corpse.Target = null;
        corpse.MoveTarget = null;
        corpse.Position = Owner.Position;
        if (corpse.View != null)
        {
            corpse.View.Root.Visible = false;
            corpse.View.Root.Position = Owner.Position;
        }
        _corpses.Add(corpse);
        return true;
    }

    public void DropAll()
    {
        for (int index = 0; index < _corpses.Count; index++)
        {
            Unit corpse = _corpses[index];
            if (corpse == null) continue;
            corpse.CorpseCarrier = null;
            corpse.CorpseRemaining = 60f;

            float angle = index * 2.399963f;
            Vector3 spot = Owner != null
                ? Owner.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 48f
                : corpse.Position;
            corpse.Position = spot;
            if (corpse.View != null)
            {
                corpse.View.Root.Visible = true;
                corpse.View.Root.Position = spot;
            }
        }
        _corpses.Clear();
    }
}

/// <summary>卸货（Amed）：把货舱里的尸体全倒在车周围。</summary>
public sealed class MeatDrop : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        MeatCargo cargo = Owner?.FindPassive<MeatCargo>() as MeatCargo;
        if (cargo == null || cargo.CorpseCount == 0) return false;
        cargo.DropAll();
        return true;
    }

    public override bool TryAutoCast(World world) => false;
}

/// <summary>掘尸（Aexh）：每 Dur 秒往货舱里补一具 UnitID 尸体，货舱满或到 DataA 上限就停。</summary>
public sealed class Exhume : Ability
{
    public override bool IsPassive => true;

    public float Interval => Duration > 0f ? Duration : 15f;
    public int MaxCorpses => (int)Data('A');
    public string CorpseUnitId => Type?.SummonAt(Level) ?? "ugho";

    private float _elapsed;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        MeatCargo cargo = Owner.FindPassive<MeatCargo>() as MeatCargo;
        if (cargo == null) return;
        if (cargo.CorpseCount >= Mathf.Min(cargo.Capacity, Mathf.Max(1, MaxCorpses))) return;

        _elapsed += delta;
        if (_elapsed < Interval) return;
        _elapsed = 0f;

        Unit corpse = world.CreateFor(Owner, CorpseUnitId, Owner.Position);
        if (corpse == null) return;
        corpse.Hp = 0f;
        corpse.EnterDeath();
        if (!cargo.Store(corpse)) world.RemoveUnit(corpse);
    }

    public override void OnOwnerDeath(World world) => _elapsed = 0f;
}