using Godot;

namespace Launcher;

/// <summary>
/// 吞尸 Acan（食尸鬼）。对标客户端：吃掉附近的一具尸体，边吃边回血。
/// 物编：Rng=50 Dur=33 DataA=10（每秒回 10 点），没有冷却与法力。
/// </summary>
public sealed class Cannibalize : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;

    public float HealPerSecond => Data('A');
    public float Radius => CastRange > 0f ? CastRange : 50f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (Owner == null || world == null) return false;

        Unit corpse = FindCorpse(world);
        if (corpse == null) return false;

        world.ConsumeCorpse(corpse);
        Owner.Buffs.Add(new CannibalizeBuff
        {
            Owner = Owner,
            Source = this,
            Name = Name,
            Remaining = Duration > 0f ? Duration : 33f,
            HealPerSecond = HealPerSecond,
        });
        return true;
    }

    /// <summary>自动施法：血量低于 60% 且脚边有尸体就吃（原版 AI 也是这么用）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.Hp >= Owner.MaxHp * 0.6f) return false;
        if (FindCorpse(world) == null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }

    private Unit FindCorpse(World world)
    {
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse || other.IsHero) continue;
            if (Owner.DistanceTo(other) > Radius + other.Radius) continue;
            return other;
        }
        return null;
    }
}

/// <summary>吞尸期间的持续回血。</summary>
public sealed class CannibalizeBuff : Buff
{
    public float HealPerSecond;

    public override bool OnTick(World world, float delta)
    {
        if (Owner != null && Owner.Alive) Owner.Heal(HealPerSecond * delta);
        return base.OnTick(world, delta);
    }
}

/// <summary>
/// 复活死尸 Arai（亡灵巫师）。对标客户端：从附近尸体里拉出 DataA 个骷髅（UnitID=uske）。
/// 物编：Cool=8 Cost=75 Rng=600 Dur=40 DataA=2。
/// </summary>
public sealed class RaiseDead : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;

    public int Count => Mathf.Max(1, (int)Data('A'));
    public float Radius => CastRange > 0f ? CastRange : 600f;
    public string SkeletonId => Type?.SummonAt(Level) ?? "uske";

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (Owner == null || world?.SummonHandler == null) return false;

        var corpses = new System.Collections.Generic.List<Unit>();
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse || other.IsHero) continue;
            if (Owner.DistanceTo(other) > Radius) continue;
            corpses.Add(other);
            if (corpses.Count >= Count) break;
        }
        if (corpses.Count == 0) return false;

        foreach (Unit corpse in corpses)
        {
            Vector3 where = corpse.Position;
            world.ConsumeCorpse(corpse);
            world.Summon(Owner, SkeletonId, where, Duration > 0f ? Duration : 40f);
        }
        return true;
    }

    /// <summary>自动施法：有交战目标、又有尸体可拉时走正常施法流程（扣法力/进冷却）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner?.Target == null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
