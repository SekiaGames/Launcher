using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    /// <summary>击杀时给附近同阵营英雄发经验（HeroExpRange），逐级结算升级。</summary>
    public void GrantKillExperience(Unit killer, Unit victim)
    {
        if (victim == null || victim.IsIllusion) return;

        int xp = HeroRules.KillXpFor(victim);
        if (xp <= 0) return;

        int team = killer != null ? killer.Player : -1;
        if (team < 0 || team > 11) return;

        foreach (Unit hero in Units)
        {
            if (!hero.Alive || !hero.IsHero) continue;
            if (hero.Player != team && !(Allied != null && Allied(hero.Player, team))) continue;
            if (hero.DistanceTo(victim) > HeroRules.ExpRange) continue;
            AddExperience(hero, xp);
        }
    }

    public void AddExperience(Unit hero, int amount)
    {
        if (hero == null || !hero.IsHero || !hero.Alive || amount <= 0) return;
        if (hero.Level >= HeroRules.MaxLevel) return;

        hero.Xp += amount;
        while (hero.Level < HeroRules.MaxLevel && hero.Xp >= HeroRules.XpToNext(hero.Level))
        {
            hero.Xp -= HeroRules.XpToNext(hero.Level);
            hero.Level++;
            ApplyHeroLevel(hero);
        }
    }

    /// <summary>升级：属性按成长值累加（含装备/技能加成），生命/法力按 力量*25 / 智力*15 重算并补差额。</summary>
    private void ApplyHeroLevel(Unit hero)
    {
        UnitType type = hero.Type;
        RefreshHeroAttributes(hero);
        hero.SkillPoints++;
        hero.View?.SetHp(hero.Hp / Mathf.Max(1f, hero.MaxHp), true);
        if (_heroLogBudget > 0)
        {
            _heroLogBudget--;
            GD.Print($"[HERO] {type.Name} 升到 {hero.Level} 级 力量{hero.Strength} 敏捷{hero.Agility} 智力{hero.Intelligence}"
                + $" 生命{hero.MaxHp:0} 法力{hero.MaxMana:0} 技能点{hero.SkillPoints}");
        }
    }

    private int _heroLogBudget = 12;
}
