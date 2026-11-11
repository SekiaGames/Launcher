using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>变形链路（妖术 / 变身）与三个引导大招（宁静 / 群星坠落 / 剑刃风暴 / 巫毒）。</summary>
    private static void CheckMorphSkills()
    {
        GD.Print("==== 变形与引导大招自检 ====");

        UnitType footmanType = UnitCatalog.Get("hfoo");
        UnitType shadowHunterType = UnitCatalog.Get("Oshd");
        UnitType demonHunterType = UnitCatalog.Get("Edem");
        if (footmanType == null || shadowHunterType == null || demonHunterType == null) return;

        // —— 妖术：变随机小动物、禁攻击禁施法、到期还原 ——
        var hexWorld = new World(null, null);
        Unit hexer = MakeHero(shadowHunterType, 0, 3, hexWorld);
        Unit hexed = MakeUnit(footmanType, 1);
        hexed.Position = new Vector3(300f, 0f, 0f);
        hexed.Hp = hexed.MaxHp * 0.5f;
        hexWorld.Units.Add(hexer);
        hexWorld.Units.Add(hexed);

        UnitType footmanOrigin = hexed.Type;
        float hexedRatio = hexed.Hp / hexed.MaxHp;
        Ability hex = hexer.GiveAbility("AOhx", 1);
        bool hexCast = hexWorld.TryCast(hexer, hex, hexed, hexed.Position);

        Verify("妖术施放成功", 1f, hexCast ? 1f : 0f, 0f);
        Verify("妖术变成小动物", 1f, hexed.Morphed ? 1f : 0f, 0f);
        Verify("妖术期间禁攻击", 0f, hexed.State.CanAttack ? 1f : 0f, 0f);
        Verify("妖术期间禁施法", 0f, hexed.State.CanCast ? 1f : 0f, 0f);
        Verify("妖术期间仍能移动", 1f, hexed.State.CanMove ? 1f : 0f, 0f);
        Verify("妖术保留生命比例", hexedRatio, hexed.Hp / Mathf.Max(1f, hexed.MaxHp), 0.01f);
        GD.Print($"[自检] 妖术 {footmanOrigin.Id} → {hexed.Type.Id}（{hexed.Type.Name}）"
            + $" 生命 {hexed.Hp:0}/{hexed.MaxHp:0} 持续 {hex.Duration:0}s");

        for (int index = 0; index < 305; index++) hexWorld.TickMorph();
        Verify("妖术到期变回原形", 1f, ReferenceEquals(hexed.Type, footmanOrigin) ? 1f : 0f, 0f);
        Verify("妖术到期解除禁用", 1f, hexed.State.CanAttack && hexed.State.CanCast ? 1f : 0f, 0f);
        Verify("妖术到期生命上限还原", footmanOrigin.Hp, hexed.MaxHp, 0.5f);
        GD.Print($"[自检] 妖术到期 {hexed.Type.Id} 生命 {hexed.Hp:0}/{hexed.MaxHp:0}（原比例 {hexedRatio:0.00}）");

        // —— 变形术：女巫把目标固定变成羊（nshe）60 秒 ——
        var polyWorld = new World(null, null);
        Unit sorceress = MakeUnit(UnitCatalog.Get("hsor") ?? footmanType, 0);
        sorceress.Mana = 300f;
        sorceress.MaxMana = 300f;
        Unit polymorphed = MakeUnit(footmanType, 1);
        polymorphed.Position = new Vector3(250f, 0f, 0f);
        polyWorld.Units.Add(sorceress);
        polyWorld.Units.Add(polymorphed);

        Ability polymorph = sorceress.GiveAbility("Aply", 1);
        bool polyCast = polyWorld.TryCast(sorceress, polymorph, polymorphed, polymorphed.Position);
        Verify("变形术施放成功", 1f, polyCast ? 1f : 0f, 0f);
        Verify("变形术变成羊", 1f,
            string.Equals(polymorphed.Type.Id, "nshe", StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("变形术持续 60 秒", 60f, polymorphed.MorphRemaining, 0.01f);
        GD.Print($"[自检] 变形术 {footmanType.Id} → {polymorphed.Type.Id}（{polymorphed.Type.Name}）"
            + $" 持续 {polymorph.Duration:0}s");

        // —— 变身：换成恶魔形态、+500 生命、照常战斗，到期还原 ——
        var morphWorld = new World(null, null);
        Unit demon = MakeHero(demonHunterType, 0, 6, morphWorld);
        morphWorld.Units.Add(demon);

        UnitType demonOrigin = demon.Type;
        float demonOriginHp = demon.MaxHp;
        Ability metamorphosis = demon.GiveAbility("AEme", 1);
        bool morphCast = morphWorld.TryCast(demon, metamorphosis, null, demon.Position);

        UnitType demonForm = UnitCatalog.Get("Edmm");
        Verify("变身施放成功", 1f, morphCast ? 1f : 0f, 0f);
        Verify("变身换成恶魔形态", 1f,
            demonForm != null && string.Equals(demon.Type.Id, "Edmm", StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("变身照常能攻击", 1f, demon.State.CanAttack ? 1f : 0f, 0f);
        Verify("变身 +500 生命上限", demonOriginHp + 500f, demon.MaxHp, 0.5f);
        Verify("变身持续 60 秒", 60f, demon.MorphRemaining, 0.01f);
        GD.Print($"[自检] 变身 {demonOrigin.Id} → {demon.Type.Id}（{demon.Type.Name}）"
            + $" 生命 {demonOriginHp:0}→{demon.MaxHp:0}");

        for (int index = 0; index < 1210; index++) morphWorld.TickMorph();
        Verify("变身到期回原形", 1f, ReferenceEquals(demon.Type, demonOrigin) ? 1f : 0f, 0f);
        Verify("变身到期恢复英雄身份", 1f, demon.IsHero ? 1f : 0f, 0f);
        Verify("变身到期生命上限还原", demonOriginHp, demon.MaxHp, 0.5f);
        GD.Print($"[自检] 变身到期 {demon.Type.Id} 生命上限 {demon.MaxHp:0} 英雄={demon.IsHero}");

        // —— 宁静：引导 30 秒，每秒给范围内友军回 20 ——
        var calmWorld = new World(null, null);
        Unit keeper = MakeHero(UnitCatalog.Get("Ekee") ?? shadowHunterType, 0, 6, calmWorld);
        var calmTargets = new List<Unit>();
        for (int index = 0; index < 2; index++)
        {
            Unit ally = MakeUnit(footmanType, 0);
            ally.Position = new Vector3(120f + index * 120f, 0f, 0f);
            ally.Hp = 100f;
            calmWorld.Units.Add(ally);
            calmTargets.Add(ally);
        }
        calmWorld.Units.Add(keeper);

        Ability tranquility = keeper.GiveAbility("AEtq", 1);
        bool calmCast = calmWorld.TryCast(keeper, tranquility, null, keeper.Position);
        Verify("宁静开始引导", 1f, calmCast && calmWorld.ChannelCount == 1 ? 1f : 0f, 0f);
        Verify("宁静引导 30 秒", 30f, tranquility.ChannelTotal, 0.01f);

        for (int index = 0; index < 21; index++) calmWorld.TickChannels();
        Verify("宁静 1 秒回 20 血", 120f, calmTargets[0].Hp, 0.6f);
        Verify("宁静 1 秒回 20 血（第二个）", 120f, calmTargets[1].Hp, 0.6f);
        GD.Print($"[自检] 宁静 1 秒 {calmTargets[0].Hp - 100f:0} 血 ×2 个友军，引导剩余 {keeper.ChannelRemaining:0}s");

        calmWorld.InterruptChannel(keeper);
        Verify("宁静被打断后停止", 0f, calmWorld.ChannelCount, 0f);

        // —— 群星坠落：引导，每 1.5 秒一波 AoE ——
        var starWorld = new World(null, null);
        Unit priestess = MakeHero(UnitCatalog.Get("Emoo") ?? shadowHunterType, 0, 6, starWorld);
        var starVictims = new List<Unit>();
        for (int index = 0; index < 3; index++)
        {
            Unit victim = MakeUnit(footmanType, 1);
            victim.Position = new Vector3(200f + index * 100f, 0f, 0f);
            starWorld.Units.Add(victim);
            starVictims.Add(victim);
        }
        starWorld.Units.Add(priestess);

        Ability starfall = priestess.GiveAbility("AEsf", 1);
        bool starCast = starWorld.TryCast(priestess, starfall, null, priestess.Position);
        Verify("群星坠落施放成功", 1f, starCast ? 1f : 0f, 0f);
        Verify("群星坠落引导 30 秒", 30f, starfall.ChannelTotal, 0.01f);

        for (int index = 0; index < 31; index++) starWorld.TickChannels();
        int starHit = 0;
        foreach (Unit victim in starVictims)
        {
            if (victim.Hp < victim.MaxHp) starHit++;
        }
        Verify("群星坠落第一波打到 3 个", 3f, starHit, 0f);
        GD.Print($"[自检] 群星坠落 1.5 秒一波，命中 {starHit} 个，单体 -{starVictims[0].MaxHp - starVictims[0].Hp:0}");

        // —— 剑刃风暴：引导，每秒 AoE 110，飞行单位不吃 ——
        var stormWorld = new World(null, null);
        Unit blademaster = MakeHero(UnitCatalog.Get("Obla") ?? shadowHunterType, 0, 6, stormWorld);
        Unit stormVictim = MakeUnit(footmanType, 1);
        stormVictim.Position = new Vector3(120f, 0f, 0f);
        stormWorld.Units.Add(blademaster);
        stormWorld.Units.Add(stormVictim);

        Ability bladestorm = blademaster.GiveAbility("AOww", 1);
        bool stormCast = stormWorld.TryCast(blademaster, bladestorm, null, blademaster.Position);
        Verify("剑刃风暴施放成功", 1f, stormCast ? 1f : 0f, 0f);
        Verify("剑刃风暴引导 5 秒", 5f, bladestorm.ChannelTotal, 0.01f);

        for (int index = 0; index < 21; index++) stormWorld.TickChannels();
        Verify("剑刃风暴 1 秒 110 伤害", 110f, stormVictim.MaxHp - stormVictim.Hp, 2f);
        GD.Print($"[自检] 剑刃风暴 1 秒 -{stormVictim.MaxHp - stormVictim.Hp:0} 血");

        // —— 巫毒：引导期间友军无敌，施法者自己不免 ——
        var voodooWorld = new World(null, null);
        Unit voodooSh = MakeHero(shadowHunterType, 0, 6, voodooWorld);
        Unit voodooAlly = MakeUnit(footmanType, 0);
        voodooAlly.Position = new Vector3(150f, 0f, 0f);
        voodooWorld.Units.Add(voodooSh);
        voodooWorld.Units.Add(voodooAlly);

        Ability voodoo = voodooSh.GiveAbility("AOvd", 1);
        bool voodooCast = voodooWorld.TryCast(voodooSh, voodoo, null, voodooSh.Position);
        for (int index = 0; index < 21; index++) voodooWorld.TickChannels();
        Verify("巫毒施放成功", 1f, voodooCast ? 1f : 0f, 0f);
        Verify("巫毒让友军无敌", 1f, voodooAlly.State.Invulnerable ? 1f : 0f, 0f);
        Verify("巫毒不保护施法者", 0f, voodooSh.State.Invulnerable ? 1f : 0f, 0f);
        GD.Print($"[自检] 巫毒 友军无敌={voodooAlly.State.Invulnerable} 施法者无敌={voodooSh.State.Invulnerable}");
    }
}
