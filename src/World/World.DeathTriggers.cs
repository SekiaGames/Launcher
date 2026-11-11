using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 死亡触发型技能：重生 AOre（延迟复活）与复仇之魂 AEsv（召魂）。
/// 客户端把这类效果挂在死亡前/后的回调上（Death.cs 的 d0153 定时器），这里用同一时机：
/// <see cref="World.Kill"/> 里在 EnterDeath 之后判定，重生期间不算击杀、不掉落、不给经验。
/// </summary>
public sealed partial class World
{
    private sealed class PendingReincarnation
    {
        public Unit Hero;
        public float Remaining;
    }

    private readonly List<PendingReincarnation> _pendingReincarnations = new();

    public int PendingReincarnationCount => _pendingReincarnations.Count;

    /// <summary>死亡触发：返回 true 表示这次死亡会被重生顶掉（不计击杀收益）。</summary>
    private bool TryDeathTriggers(Unit unit)
    {
        if (unit == null || !unit.IsHero) return false;

        var triggering = new List<Ability>();
        foreach (Ability ability in unit.Abilities)
        {
            if (!ability.Learned || !ability.Ready) continue;
            if (ability is Reincarnation || ability is SpiritOfVengeance) triggering.Add(ability);
        }
        if (triggering.Count == 0) return false;

        bool reincarnating = false;
        foreach (Ability ability in triggering)
        {
            if (ability is Reincarnation reincarnation)
            {
                reincarnation.CooldownRemaining = reincarnation.Cooldown;
                unit.AwaitingRevive = false;
                if (string.Equals(ability.Id, "AIrc", StringComparison.OrdinalIgnoreCase)) ConsumeReincarnationCharm(unit);
                _pendingReincarnations.Add(new PendingReincarnation
                {
                    Hero = unit,
                    Remaining = reincarnation.DelaySeconds,
                });
                reincarnating = true;
            }
            else if (ability is SpiritOfVengeance vengeance)
            {
                vengeance.CooldownRemaining = vengeance.Cooldown;
                vengeance.SummonSpirit(this, unit);
            }
        }
        return reincarnating;
    }

    /// <summary>重生十字章（ankh）是一次性的：触发重生时把身上那件吃掉，免得无限复活。</summary>
    private void ConsumeReincarnationCharm(Unit hero)
    {
        foreach (Item item in hero.Inventory)
        {
            if (item?.Type == null) continue;
            if (!item.Type.AbilList.Contains("AIrc", StringComparison.OrdinalIgnoreCase)) continue;
            RemoveItemBonus(hero, item);
            ConsumeItem(item);
            return;
        }
    }

    /// <summary>推进重生倒计时：到点原地满血复活（不走祭坛、不扣金）。</summary>
    public void TickReincarnation()
    {
        for (int index = _pendingReincarnations.Count - 1; index >= 0; index--)
        {
            PendingReincarnation pending = _pendingReincarnations[index];
            pending.Remaining -= Tick;
            if (pending.Remaining > 0f) continue;

            _pendingReincarnations.RemoveAt(index);
            Unit hero = pending.Hero;
            if (hero == null || hero.Alive) continue;

            hero.ReviveAt(hero.Position, hero.Facing);
            GD.Print($"[REVIVE] P{hero.Player} {hero.Type.Name} Lv{hero.Level} 重生，生命 {hero.Hp:0}/{hero.MaxHp:0}");
            HeroRevived?.Invoke(hero);
        }
    }
}