using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 武器系统：攻击时序（前摇 → 伤害点 → 后摇）、投射物飞行命中、溅射。
/// 对标客户端 Components/WeaponSystem.cs。
/// </summary>
public sealed partial class World
{
    private sealed class PendingHit
    {
        public DamageInfo Info;
        public float Remaining;
    }

    private readonly List<PendingHit> _pendingHits = new();

    public int PendingHitCount => _pendingHits.Count;

    /// <summary>每 tick 推进所有单位的攻击时序，再结算飞到点的投射物。</summary>
    public void TickWeapons()
    {
        foreach (Unit unit in Units)
        {
            if (!unit.Alive)
            {
                unit.CancelAttack();
                continue;
            }

            if (unit.AttackWindupActive)
            {
                unit.AttackWindup -= Tick;
                if (unit.AttackWindup <= 0f) FireWeapon(unit);
            }

            if (unit.Backswing > 0f) unit.Backswing -= Tick;
            if (unit.AttackCooldown > 0f) unit.AttackCooldown -= Tick;
            if (unit.InMineRemaining > 0f) unit.InMineRemaining -= Tick;
        }

        for (int index = _pendingHits.Count - 1; index >= 0; index--)
        {
            PendingHit hit = _pendingHits[index];
            hit.Remaining -= Tick;
            if (hit.Remaining > 0f) continue;

            _pendingHits.RemoveAt(index);
            Unit victim = hit.Info.Target;
            if (victim == null || !victim.Alive) continue;
            ResolveHit(hit.Info);
        }
    }

    /// <summary>进入射程后由单位 Step 调用：满足条件就起手。冷却从起手开始算，前摇在冷却之内。</summary>
    public void BeginAttack(Unit unit, Unit target)
    {
        if (target == null || !target.Alive) return;
        if (!unit.ReadyToAttack) return;

        WeaponProfile weapon = unit.Type.WeaponFor(target.Type.Kind);
        if (weapon == null) return;

        unit.ActiveWeapon = weapon;
        unit.AttackCooldown = AttackCooldownOf(unit, weapon);
        unit.BeginAttack(target, weapon.DamagePoint);
    }

    /// <summary>伤害点：算伤害、决定是立刻结算还是抛出投射物。</summary>
    private void FireWeapon(Unit unit)
    {
        Unit target = unit.WindupTarget;
        WeaponProfile weapon = unit.ActiveWeapon ?? unit.Type.Weapon;
        unit.EndWindup();

        unit.Backswing = Mathf.Max(0f, weapon.Backswing);

        if (target == null || !target.Alive) return;

        float raw = Mathf.Lerp(weapon.MinDamage, weapon.MaxDamage, (float)_random.NextDouble())
            + DamageBonus(unit);
        raw = ApplyAttackModifiers(unit, target, raw);
        DamageInfo info = MakeAttackDamage(unit, target, raw, weapon);

        bool flies = weapon.MissileScene != null && weapon.MissileSpeed > 0f;
        if (flies)
        {
            MissileFired?.Invoke(unit, target);
            float travel = Mathf.Max(Tick, unit.DistanceTo(target) / weapon.MissileSpeed);
            _pendingHits.Add(new PendingHit { Info = info, Remaining = travel });
            return;
        }

        ResolveHit(info);
        AttackLanded?.Invoke(unit, target);
        if (target.AutoAcquire && target.Alive && target.Target == null) EngageTarget(target, unit);
    }

    /// <summary>命中：主目标结算，再按 Farea/Harea/Qarea 三档扩散（区域内全部命中）。</summary>
    private void ResolveHit(DamageInfo info)
    {
        DealDamage(info);
        ApplySplash(info);
        AttackLanded?.Invoke(info.Source, info.Target);
    }

    /// <summary>溅射：距离主目标 ≤ Farea 全额、≤ Harea 半额、≤ Qarea 四分之一。</summary>
    public void ApplySplash(DamageInfo primary)
    {
        Unit attacker = primary.Source;
        Unit center = primary.Target;
        if (attacker?.Type == null || center == null) return;

        WeaponProfile weapon = primary.Weapon ?? attacker.Type.Weapon;
        float full = weapon.SplashFull;
        float half = weapon.SplashHalf;
        float quarter = weapon.SplashQuarter;
        if (quarter <= 0f) return;

        var victims = new List<(Unit Unit, float Ratio, float Distance)>();
        foreach (Unit other in Units)
        {
            if (other == center || !other.Alive) continue;
            if (IsAllied(attacker.Player, other.Player)) continue;

            float distance = center.DistanceTo(other);
            float ratio = distance <= full ? 1f
                : distance <= half ? 0.5f
                : distance <= quarter ? 0.25f
                : 0f;
            if (ratio <= 0f) continue;
            victims.Add((other, ratio, distance));
        }

        victims.Sort((left, right) => left.Distance.CompareTo(right.Distance));
        foreach ((Unit victim, float ratio, float _) in victims)
        {
            DealDamage(MakeAttackDamage(attacker, victim, primary.Raw * ratio, weapon));
        }
    }
}
