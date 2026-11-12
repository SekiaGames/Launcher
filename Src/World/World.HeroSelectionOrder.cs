using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

public sealed partial class World
{
    /// <summary>
    /// 击杀经验：客户端 World.AwardHeroExperience → ShareKillExperience 是 `赏金 / 收到经验的英雄数`，
    /// 再乘 handicap 与科技加成后四舍五入；召唤物赏金先按 SummonedKillFactor 折算。
    /// Launcher 没有 handicap 表（全 100%）也没有加经验的科技，故只落「均分 + 召唤物折算」两条。
    /// </summary>
    public void GrantKillExperience(Unit killer, Unit victim)
    {
        if (victim == null || victim.IsIllusion) return;
        // 客户端 World.cs:505-511：建筑击杀是否给经验由 BuildingKillsGiveExp 决定（TFT 为 0 = 不给）。
        if (killer != null && killer.IsBuilding && !MiscRules.BuildingKillsGiveExp) return;

        int bounty = KillExperienceBounty(victim);
        if (bounty <= 0) return;

        int team = killer != null ? killer.Player : -1;
        if (team < 0 || team > 11) return;

        // 客户端 660630：先收近距离英雄；近处没人且开了 GlobalExperience 才退回"全体己方英雄"。
        var nearby = new List<Unit>();
        var everyone = new List<Unit>();
        foreach (Unit hero in Units)
        {
            if (!hero.Alive || !hero.IsHero) continue;
            if (hero.Player != team && !(Allied != null && Allied(hero.Player, team))) continue;
            // 满级英雄默认不再吸经验（MaxLevelHeroesDrainExp=0）。
            if (!MiscRules.MaxLevelHeroesDrainExp && hero.Level >= HeroRules.MaxLevel) continue;
            everyone.Add(hero);
            if (hero.DistanceTo(victim) <= HeroRules.ExpRange) nearby.Add(hero);
        }
        List<Unit> receivers = nearby.Count > 0
            ? nearby
            : (MiscRules.GlobalExperience ? everyone : nearby);
        if (receivers.Count == 0) return;

        int share = Mathf.FloorToInt((float)bounty / receivers.Count + 0.5f);
        if (share <= 0) return;
        foreach (Unit hero in receivers) AddExperience(hero, share);
    }

    /// <summary>赏金表按被击杀单位的等级读；召唤物的赏金再乘 SummonedKillFactor 并截断（同客户端 ScaleKillExperience）。</summary>
    private static int KillExperienceBounty(Unit victim)
    {
        int bounty = HeroRules.KillXpFor(victim);
        if (victim.SummonRemaining > 0f) bounty = (int)(bounty * MiscRules.SummonedKillFactor);
        return bounty;
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
