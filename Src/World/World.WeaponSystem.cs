using Godot;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 武器系统：攻击时序（前摇 → 伤害点 → 后摇）、投射物飞行命中、溅射。
/// 对标客户端 Components/WeaponSystem.cs。
/// </summary>
public sealed partial class World
{
    private sealed class PendingHit
    {
        public DamageData Info;
        public float Remaining;
    }

    private readonly List<PendingHit> _pendingHits = new();

    public event System.Action<Unit> AttackMissed;

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
                unit.AttackWindup -= FrameSeconds;
                if (unit.AttackWindup <= 0f) FireWeapon(unit);
            }

            if (unit.Backswing > 0f) unit.Backswing -= FrameSeconds;
            if (unit.AttackCooldown > 0f) unit.AttackCooldown -= FrameSeconds;
            if (unit.InMineRemaining > 0f) unit.InMineRemaining -= FrameSeconds;
        }

        for (int index = _pendingHits.Count - 1; index >= 0; index--)
        {
            PendingHit hit = _pendingHits[index];
            hit.Remaining -= FrameSeconds;
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

        Weapon weapon = unit.Type.WeaponFor(target.Type.Kind);
        if (weapon == null) return;

        unit.ActiveWeapon = weapon;
        unit.AttackCooldown = AttackCooldownOf(unit, weapon);
        unit.BeginAttack(target, weapon.DamagePoint);
        UnitAttacked?.Invoke(unit, target);
    }

    /// <summary>伤害点：算伤害、决定是立刻结算还是抛出投射物。</summary>
    private void FireWeapon(Unit unit)
    {
        Unit target = unit.WindupTarget;
        Weapon weapon = unit.ActiveWeapon ?? unit.Type.Weapon;
        unit.EndWindup();

        unit.Backswing = Mathf.Max(0f, weapon.Backswing);

        if (target == null || !target.Alive) return;

        if (unit.AttackMissChance > 0f && _random.NextDouble() < unit.AttackMissChance)
        {
            AttackMissed?.Invoke(unit);
            return;
        }

        // 客户端 Weapon.Roll：底伤 + N 个 sides 面骰，不是 min/max 连续插值。
        float raw = weapon.Roll(_random)
            + DamageBonus(unit) + unit.OrbDamage;
        raw *= 1f + unit.BonusDamagePct + unit.AuraDamagePct;
        unit.PaidArrowShot = false;
        string arrowScene = PrepareArrowMissile(unit, target);
        // 强击光环只加远程攻击（物编 AEar 的 DataA 是百分比）
        if (!string.IsNullOrEmpty(weapon.MissileScene)) raw *= 1f + unit.AuraRangedDamagePct;
        raw = ApplyAttackModifiers(unit, target, raw);
        unit.PaidArrowShot = false;
        raw = ApplyLaunchModifiers(unit, target, raw);
        DamageData info = MakeAttackDamage(unit, target, raw, weapon);
        info.IsCritical = LastAttackWasCritical;
        ApplyDamagePacketModifiers(info);
        if (unit.IsIllusion)
        {
            info.Raw *= unit.IllusionDamageDealt;
            info.Amount *= unit.IllusionDamageDealt;
        }

        bool flies = (arrowScene ?? weapon.MissileScene) != null && weapon.MissileSpeed > 0f;
        if (flies)
        {
            unit.NextMissileScene = arrowScene;
            MissileFired?.Invoke(unit, target);
            float travel = Mathf.Max(FrameSeconds, unit.DistanceTo(target) / weapon.MissileSpeed);
            _pendingHits.Add(new PendingHit { Info = info, Remaining = travel });
            return;
        }

        ResolveHit(info);
        if (target.AutoAcquire && target.Alive && target.Target == null) EngageTarget(target, unit);
    }

    public string PrepareArrowMissile(Unit unit, Unit target)
    {
        if (unit == null) return null;
        unit.PaidArrowShot = false;
        if (unit.Type == null || target == null) return null;
        foreach (Ability ability in unit.Abilities)
        {
            if (!ability.Learned) continue;
            string art = ability.PrepareAttackMissile(this, target);
            if (string.IsNullOrEmpty(art)) continue;
            string scene = PathResolve.ModelScene(art);
            if (scene == null) continue;
            return scene;
        }
        return null;
    }

    /// <summary>命中：主目标结算，再按 Farea/Harea/Qarea 三档扩散（区域内全部命中）。</summary>
    private void ResolveHit(DamageData info)
    {
        DealDamage(info);
        ApplyOrbEffect(info);
        ApplyLandedModifiers(info);
        ApplySplash(info);
        AttackLanded?.Invoke(info.Source, info.Target);
    }

    /// <summary>法球命中后的附加效果：火焰溅射、腐化减甲、霜冻/冰冻减速、闪电净化、法术抽蓝、毒液中毒。</summary>
    public void ApplyOrbEffect(DamageData info)
    {
        Unit attacker = info?.Source;
        Unit target = info?.Target;
        if (attacker == null || target == null || !target.Alive || info.Reflected) return;
        if (attacker.OrbEffectKind == OrbEffect.None) return;

        float seconds = attacker.OrbSeconds > 0f ? attacker.OrbSeconds : 5f;
        float secondary = attacker.OrbSecondary > 0f ? attacker.OrbSecondary : attacker.OrbDamage;

        switch (attacker.OrbEffectKind)
        {
            case OrbEffect.Fire:
                foreach (Unit victim in EnemiesInRange(attacker, target.Position, 140f))
                {
                    DealDamage(MakeAbilityDamage(attacker, victim, attacker.OrbDamage, AttackType.Spells, DamageType.Magic));
                }
                break;
            case OrbEffect.Corruption:
                ApplyBuff(target, new FaerieFireBuff { ArmorPenalty = 2f }, seconds);
                break;
            case OrbEffect.Freezing:
                target.ApplySlow(0.5f, 0.25f, seconds);
                break;
            case OrbEffect.Frost:
                target.ApplySlow(0.3f, 0.3f, seconds);
                break;
            case OrbEffect.Lightning:
                target.ApplySlow(0.5f, 0f, seconds);
                if (target.IsSummon)
                {
                    DealDamage(MakeAbilityDamage(attacker, target, secondary, AttackType.Spells, DamageType.Magic));
                }
                break;
            case OrbEffect.Spells:
            {
                float drained = Mathf.Min(target.Mana, secondary);
                target.Mana -= drained;
                if (drained > 0f)
                {
                    DealDamage(MakeAbilityDamage(attacker, target, drained, AttackType.Spells, DamageType.Magic));
                }
                break;
            }
            case OrbEffect.Venom:
                ApplyBuff(target, new VenomBuff
                {
                    DamageSource = attacker,
                    DamagePerTick = attacker.OrbDamage,
                    Interval = 1f,
                }, seconds);
                break;
        }
    }

    /// <summary>普攻命中钩子：腐蚀吐息/液体火焰/毒矛/月刃这类「中了之后再生数」的被动。</summary>
    public void ApplyLandedModifiers(DamageData info)
    {
        Unit attacker = info?.Source;
        if (info == null || !info.IsAttack || attacker?.Abilities == null) return;

        foreach (Ability ability in attacker.Abilities)
        {
            if (!ability.Learned) continue;
            ability.OnAttackLanded(this, info);
        }
    }

    /// <summary>溅射：距离主目标 ≤ Farea 全额、≤ Harea 中型、≤ Qarea 小型，系数取 Hfact/Qfact；范围与归属按 splashTargs。</summary>
    public void ApplySplash(DamageData primary)
    {
        Unit attacker = primary.Source;
        Unit center = primary.Target;
        if (attacker?.Type == null || center == null) return;

        Weapon weapon = primary.Weapon ?? attacker.Type.Weapon;
        float full = weapon.SplashFull;
        float half = weapon.SplashHalf;
        float quarter = weapon.SplashQuarter;
        if (quarter <= 0f) return;

        TargetKind spillShapes = weapon.SpillTargets != TargetKind.None ? weapon.SpillTargets : weapon.Targets;

        var victims = new List<(Unit Unit, float Ratio, float Distance)>();
        foreach (Unit other in Units)
        {
            if (other == center || !other.Alive || other.Type == null) continue;
            if ((other.Type.Kind & spillShapes) == 0) continue;
            if ((RelationOf(attacker, other) & weapon.SpillRelations) == 0) continue;

            float distance = center.DistanceTo(other);
            float ratio = distance <= full ? 1f
                : distance <= half ? weapon.SplashHalfRatio
                : distance <= quarter ? weapon.SplashQuarterRatio
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
