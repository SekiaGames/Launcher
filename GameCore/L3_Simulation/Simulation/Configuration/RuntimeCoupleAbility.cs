using Godot;

namespace Launcher;

/// <summary>
/// 骑乘合成（Aco2 弓箭手 / Aco3 角鹰兽）。对标客户端 RuntimeCoupleAbility：
/// DataA 是要找的同伴类型、UnitID 是合成结果、DataB=1 的那个是「主动飞过去的一方」。
/// 原版是等着的一方不下令、飞过去的一方贴身后合成；这里两边都能点，
/// 施放后同伴会朝自己飞，贴到一起就换成角鹰兽骑士，血量按两者比例平均保留。
/// </summary>
public sealed class Couple : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    /// <summary>DataA：同伴的单位类型（弓箭手 ↔ 角鹰兽）。</summary>
    public string PartnerType => (Type?.DataTextAt(Level, 'A') ?? "").Trim();
    /// <summary>UnitID：合成出来的单位（角鹰兽骑士）。</summary>
    public string ResultType => Type?.SummonAt(Level) ?? "";
    /// <summary>DataB=1 表示这个形态是「飞过去的那一方」。</summary>
    public bool MovingMember => Data('B') > 0f;

    private Unit _partner;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        if (UnitFactory.Get(ResultType) == null) return false;

        Unit partner = target != null && IsLegal(world, target) ? target : FindPartner(world);
        if (partner == null) return false;

        if (Owner.DistanceTo(partner) <= MergeDistance(partner)) return Merge(world, partner);

        _partner = partner;
        partner.Target = null;
        partner.MoveTarget = Owner.Position;
        return true;
    }

    public override void OnWorldTick(World world, float delta)
    {
        if (_partner == null || Owner == null) return;

        if (!IsLegal(world, _partner) || Owner.DistanceTo(_partner) > Area * 2f)
        {
            _partner = null;
            return;
        }

        if (Owner.DistanceTo(_partner) > MergeDistance(_partner)) return;

        Unit partner = _partner;
        _partner = null;
        Merge(world, partner);
    }

    public override void OnOwnerDeath(World world) => _partner = null;
    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.Morphed) return false;
        if (Owner.Target != null || HasEnemyNearby(world, 500f)) return false;
        if (FindPartner(world) == null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }

    private Unit FindPartner(World world)
    {
        Unit best = null;
        float bestDistance = Mathf.Max(1f, Area);
        foreach (Unit other in world.Units)
        {
            if (!IsLegal(world, other)) continue;
            float distance = Owner.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        return best;
    }

    private bool IsLegal(World world, Unit unit)
    {
        if (unit == null || unit == Owner || !unit.Alive) return false;
        if (unit.Player != Owner.Player) return false;
        return string.Equals(unit.Type?.Id, PartnerType, System.StringComparison.OrdinalIgnoreCase);
    }

    private float MergeDistance(Unit partner) => Owner.Radius + Mathf.Max(24f, partner.Radius) + 32f;

    private bool Merge(World world, Unit partner)
    {
        if (world == null || Owner == null || partner == null) return false;

        float ratio = (Owner.Hp / Mathf.Max(1f, Owner.MaxHp) + partner.Hp / Mathf.Max(1f, partner.MaxHp)) * 0.5f;
        Vector3 spot = Owner.Position;
        float facing = Owner.Facing;

        Unit merged = world.CreateFor(Owner, ResultType, spot);
        if (merged == null) return false;

        world.RemoveUnit(Owner);
        world.RemoveUnit(partner);

        merged.Facing = facing;
        merged.Hp = Mathf.Clamp(merged.MaxHp * Mathf.Clamp(ratio, 0.05f, 1f), 1f, merged.MaxHp);
        merged.View?.Root.Position = new Vector3(spot.X, merged.Position.Y, spot.Z);

        Ability decouple = merged.FindAbility("Adec");
        if (decouple != null) decouple.CooldownRemaining = decouple.Cooldown;
        return true;
    }
}
