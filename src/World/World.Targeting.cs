using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    private void StepCreep(Unit unit)
    {
        if (unit.Returning)
        {
            if (Advance(unit, unit.Home, 60f))
            {
                unit.Returning = false;
                unit.ReturnDelay = 0f;
                unit.Path = null;
                unit.View?.Play("Stand");
            }
            return;
        }

        if (unit.ReturnDelay > 0f)
        {
            unit.ReturnDelay -= Tick;
            unit.Target = null;
            unit.MoveTarget = null;
            unit.Path = null;
            unit.View?.Play("Stand");
            if (unit.ReturnDelay <= 0f) unit.Returning = true;
            return;
        }

        if (unit.Target != null)
        {
            if (!unit.Target.Alive)
            {
                unit.Target = null;
                unit.MoveTarget = null;
                unit.Path = null;
                return;
            }
            if (GuardDistanceExceeded(unit))
            {
                GiveUpChase(unit);
                return;
            }

            float distance = unit.DistanceTo(unit.Target);
            if (distance <= unit.Type.AttackRange + unit.Target.Radius)
            {
                FaceTarget(unit, unit.Target);
                BeginAttack(unit, unit.Target);
            }
            else
            {
                Advance(unit, unit.Target.Position, unit.Type.AttackRange + unit.Target.Radius);
            }
            return;
        }

        Unit victim = FindAcquireTarget(unit);
        if (victim != null)
        {
            EngageTarget(unit, victim);
            if (unit.Player == 12 && _creepLogBudget > 0)
            {
                _creepLogBudget--;
                GD.Print($"[CREEP] 野怪 {unit.Type.Id} 警戒锁定 {victim.Type.Id} 距离 {unit.DistanceTo(victim):0} 距营地 {DistanceFromGuard(unit):0}");
            }
            return;
        }

        unit.View?.Play("Stand");
    }

    private static int _creepLogBudget = 8;

    private Unit FindAcquireTarget(Unit unit)
    {
        Unit best = null;
        float bestDistance = Mathf.Min(unit.Type.AcquireRange, MiscRules.GuardDistance);
        foreach (Unit other in Units)
        {
            if (!other.Alive) continue;
            if (other.Player == 15) continue;
            if (IsAllied(unit.Player, other.Player)) continue;
            if (!CanStrike(unit, other)) continue;
            if (other.State.Invisible && !unit.State.Has(StateMod.Detect)) continue;
            float distance = unit.DistanceTo(other);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = other;
            }
        }
        return best;
    }

    private static float DistanceFromGuard(Unit unit)
    {
        return new Vector2(unit.Position.X - unit.Home.X, unit.Position.Z - unit.Home.Z).Length();
    }

    private static bool GuardDistanceExceeded(Unit unit)
    {
        if (unit.Player != 12) return DistanceFromGuard(unit) > unit.LeashRange;
        return DistanceFromGuard(unit) >= MiscRules.MaxGuardDistance;
    }

    private void GiveUpChase(Unit unit)
    {
        unit.Target = null;
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.ReturnDelay = MiscRules.GuardReturnTime;
        unit.Returning = false;
        unit.View?.Play("Stand");
        if (_creepLogBudget > 0)
        {
            _creepLogBudget--;
            GD.Print($"[CREEP] 野怪 {unit.Type.Id} 超出最大追击 {DistanceFromGuard(unit):0} >= {MiscRules.MaxGuardDistance:0}，原地等待 {MiscRules.GuardReturnTime:0}s 后回营");
        }
    }

    private void EngageTarget(Unit unit, Unit victim)
    {
        if (victim == null || !victim.Alive) return;
        unit.Target = victim;
        unit.Returning = false;
        unit.ReturnDelay = 0f;
        CallForHelp(unit, victim);
    }

    private void CallForHelp(Unit caller, Unit victim)
    {
        if (caller.Player != 12) return;
        foreach (Unit other in Units)
        {
            if (!other.Alive || other == caller || other.Player != 12) continue;
            if (other.Target != null || other.Returning || other.ReturnDelay > 0f) continue;
            if (caller.DistanceTo(other) > MiscRules.CreepCallForHelp) continue;
            other.Target = victim;
        }
    }

    /// <summary>攻击间隔：按实际使用的那套武器算，英雄再吃 AgiAttackSpeedBonus 提速。</summary>
    public static float AttackCooldownOf(Unit unit, WeaponProfile weapon = null)
    {
        float cooldown = (weapon ?? unit.Type.Weapon).Cooldown;
        if (unit.IsHero) cooldown /= 1f + unit.Agility * HeroRules.AgiAttackSpeedBonus;
        cooldown /= 1f + Mathf.Max(0f, unit.BonusAttackSpeedPct);
        cooldown /= 1f + Mathf.Max(0f, unit.AuraAttackSpeedPct);
        if (unit.Slowed) cooldown /= Mathf.Max(0.1f, 1f - unit.SlowAttackPct);
        return Mathf.Max(0.15f, cooldown);
    }

    /// <summary>目标掩码判定：两套武器任一能打这个类别就能打（地面兵打不到空中，迫击炮靠第二武器砸建筑）。</summary>
    public static bool CanStrike(Unit attacker, Unit target)
    {
        if (attacker?.Type == null || target?.Type == null) return false;
        return attacker.Type.WeaponFor(target.Type.Kind) != null;
    }
}
