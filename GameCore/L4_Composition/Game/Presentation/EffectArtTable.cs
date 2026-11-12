using System;
using System.Collections.Generic;

namespace Launcher;

public static class EffectArtTable
{
    private sealed class Entry
    {
        public string Directory;
        public string[] Suffixes;
        public string Mount;
        public bool Stick;
    }

    private static readonly Dictionary<string, Entry> Skills = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AHhb"] = new() { Directory = "Human/HolyBolt", Suffixes = new[] { "SpecialArt" }, Mount = "origin" },
        ["AHds"] = new() { Directory = "Human/RuntimeDivineShieldAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AHad"] = new() { Directory = "Human/DevotionAura", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHav"] = new() { Directory = "Human/Avatar", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["AHre"] = new() { Directory = "Human/Resurrect", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHbz"] = new() { Directory = "Human/RuntimeBlizzardAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHab"] = new() { Directory = "Human/Brilliance", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHtb"] = new() { Directory = "Human/StormBolt", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AHtc"] = new() { Directory = "Human/Thunderclap", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["Ahea"] = new() { Directory = "Human/Heal", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aply"] = new() { Directory = "Human/RuntimePolymorphAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHmt"] = new() { Directory = "Human/RuntimeMassTeleportAbility", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["AHbn"] = new() { Directory = "Human/RuntimeBanishAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHfs"] = new() { Directory = "Human/RuntimeFlameStrikeAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHdr"] = new() { Directory = "Other/Drain", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aclf"] = new() { Directory = "Human/CloudOfFog", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afla"] = new() { Directory = "Human/RuntimeFlareAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Acmg"] = new() { Directory = "Human/ControlMagic", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Afbk"] = new() { Directory = "Human/RuntimeFeedbackAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Adef"] = new() { Directory = "Human/RuntimeDefendAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Ainf"] = new() { Directory = "Human/RuntimeInnerFireAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Adis"] = new() { Directory = "Human/DispelMagic", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Amls"] = new() { Directory = "Human/RuntimeMagicLeashAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aflk"] = new() { Directory = "Human/FlakCannons", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afsh"] = new() { Directory = "Human/FragmentationShards", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Ahrp"] = new() { Directory = "Other/Repair", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aivs"] = new() { Directory = "Human/Invisibility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Aslo"] = new() { Directory = "Human/Slow", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Asps"] = new() { Directory = "Human/RuntimeSpellStealAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AHta"] = new() { Directory = "Human/MagicSentry", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Amfl"] = new() { Directory = "Human/RuntimeManaFlareAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANms"] = new() { Directory = "Human/RuntimeManaShieldAbility", Suffixes = new[] { "Caster", "Target" }, Mount = "overhead" },
        ["Aam2"] = new() { Directory = "Undead/RuntimeAntiMagicShellAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },

        ["AOcl"] = new() { Directory = "Orc/LightningBolt", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AOsh"] = new() { Directory = "Orc/Shockwave", Suffixes = new[] { "Missile" }, Mount = "origin" },
        ["AOws"] = new() { Directory = "Orc/RuntimeWarStompAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOhw"] = new() { Directory = "Orc/RuntimeHealingWaveAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AOhx"] = new() { Directory = "Human/RuntimePolymorphAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AOvd"] = new() { Directory = "Orc/Voodoo", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOre"] = new() { Directory = "Orc/RuntimeReincarnationAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOeq"] = new() { Directory = "Orc/EarthQuake", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOmi"] = new() { Directory = "Orc/RuntimeMirrorImageAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Ablo"] = new() { Directory = "Orc/RuntimeBloodlustAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Alsh"] = new() { Directory = "Orc/RuntimeLightningShieldAbility", Suffixes = new[] { "Buff" }, Mount = "overhead", Stick = true },
        ["Apg2"] = new() { Directory = "Orc/RuntimePurgeAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Absk"] = new() { Directory = "Orc/TrollBerserk", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Aliq"] = new() { Directory = "Orc/RuntimeLiquidFireAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aspi"] = new() { Directory = "Orc/SpikeBarrier", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aakb"] = new() { Directory = "Orc/RuntimeCommandAuraAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AIcd"] = new() { Directory = "Orc/CommandAura", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["Ara2"] = new() { Directory = "NightElf/BattleRoar", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aroa"] = new() { Directory = "NightElf/BattleRoar", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Arej"] = new() { Directory = "NightElf/RuntimeRejuvenationAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Acyc"] = new() { Directory = "NightElf/RuntimeCycloneAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afae"] = new() { Directory = "NightElf/RuntimeFaerieFireAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Afa2"] = new() { Directory = "NightElf/RuntimeFaerieFireAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Apsh"] = new() { Directory = "NightElf/FaerieDragonInvis", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Atau"] = new() { Directory = "NightElf/RuntimeTauntAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Assk"] = new() { Directory = "NightElf/Barkskin", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Ansk"] = new() { Directory = "NightElf/Barkskin", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Acor"] = new() { Directory = "NightElf/RuntimeCorrosiveBreathAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aeat"] = new() { Directory = "NightElf/RuntimeEatTreeAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aweb"] = new() { Directory = "Undead/Web", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afrb"] = new() { Directory = "Other/BreathOfFrost", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afrz"] = new() { Directory = "Undead/RuntimeFreezingBreathAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANen"] = new() { Directory = "Orc/Ensnare", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Acny"] = new() { Directory = "NightElf/RuntimeCycloneAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ACfu"] = new() { Directory = "Undead/RuntimeFrostArmorAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Aro1"] = new() { Directory = "NightElf/Root", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aro2"] = new() { Directory = "NightElf/Uproot", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Asb1"] = new() { Directory = "Other/RuntimeSubmergeAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Asb2"] = new() { Directory = "Other/RuntimeSubmergeAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Anpa"] = new() { Directory = "Other/RuntimeParasiteAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Anrc"] = new() { Directory = "Demon/RuntimeRainOfFireAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Andp"] = new() { Directory = "Demon/RuntimeDarkPortalAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aren"] = new() { Directory = "Other/Repair", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Arst"] = new() { Directory = "Other/Repair", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Arpl"] = new() { Directory = "Undead/ReplenishHealth", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Arpm"] = new() { Directory = "Undead/ReplenishMana", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aabr"] = new() { Directory = "Undead/RuntimeLifeRegenerationAuraAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Ashm"] = new() { Directory = "NightElf/RuntimeShadowMeldAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANvc"] = new() { Directory = "Other/RuntimeVolcanoAbility", Suffixes = new[] { "RuntimeVolcanoAbility" }, Mount = "origin" },
        ["Acef"] = new() { Directory = "Other/RuntimeSpiritOfVengeanceAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANcf"] = new() { Directory = "Other/RuntimeBreathOfFireAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Acdh"] = new() { Directory = "Other/StrongDrink", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANht"] = new() { Directory = "Other/RuntimeHowlOfTerrorAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANca"] = new() { Directory = "Other/Cleave", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANdo"] = new() { Directory = "Other/RuntimeDoomAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Arsp"] = new() { Directory = "Other/RuntimeStampedeAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANso"] = new() { Directory = "Other/RuntimeSoulBurnAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANab"] = new() { Directory = "Other/RuntimeAcidBombAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANhs"] = new() { Directory = "Other/RuntimeHealingSprayAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANtm"] = new() { Directory = "Other/RuntimeTransmuteAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANia"] = new() { Directory = "Other/RuntimeIncinerateAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANba"] = new() { Directory = "Other/RuntimeBlackArrowAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANdr"] = new() { Directory = "Other/Drain", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANrf"] = new() { Directory = "Demon/RuntimeRainOfFireAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANto"] = new() { Directory = "Other/RuntimeTornadoAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANfl"] = new() { Directory = "Other/RuntimeForkedLightningAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANfa"] = new() { Directory = "Other/FrostArrows", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHca"] = new() { Directory = "Other/FrostBolt", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANch"] = new() { Directory = "Other/RuntimeCharmAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANsl"] = new() { Directory = "Demon/SoulPreservation", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANdc"] = new() { Directory = "Demon/DarkConversion", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["AUdc"] = new() { Directory = "Undead/RuntimeDeathCoilAbility", Suffixes = new[] { "SpecialArt" }, Mount = "target" },
        ["AUdp"] = new() { Directory = "Undead/RuntimeDeathPactAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUdr"] = new() { Directory = "Undead/RuntimeDarkRitualAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AUau"] = new() { Directory = "Undead/RuntimeUnholyAuraAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUan"] = new() { Directory = "Undead/RuntimeAnimateDeadAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AUfn"] = new() { Directory = "Undead/RuntimeFrostNovaAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUfa"] = new() { Directory = "Undead/RuntimeFrostArmorAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AUdd"] = new() { Directory = "Undead/RuntimeDeathAndDecayAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUcs"] = new() { Directory = "Undead/RuntimeCarrionSwarmAbility", Suffixes = new[] { "Missile" }, Mount = "origin" },
        ["AUsl"] = new() { Directory = "Undead/Sleep", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AUav"] = new() { Directory = "Undead/VampiricAura", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUim"] = new() { Directory = "Undead/RuntimeImpaleAbility", Suffixes = new[] { "Caster", "HitTarget" }, Mount = "origin" },
        ["AUts"] = new() { Directory = "Undead/ThornyShield", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AUls"] = new() { Directory = "Undead/RuntimeLocustSwarmAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Arai"] = new() { Directory = "Undead/RaiseSkeletonWarrior", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Auhf"] = new() { Directory = "Undead/RuntimeUnholyFrenzyAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Acri"] = new() { Directory = "Undead/RuntimeCrippleAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Acrs"] = new() { Directory = "Undead/RuntimeCurseAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Aps2"] = new() { Directory = "Undead/RuntimePossessionAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Adev"] = new() { Directory = "Undead/Devour", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUns"] = new() { Directory = "Undead/RuntimeUnsummonAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aap2"] = new() { Directory = "Undead/PlagueCloud", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aap1"] = new() { Directory = "Undead/PlagueCloud", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["AEfk"] = new() { Directory = "NightElf/RuntimeFanOfKnivesAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AEer"] = new() { Directory = "NightElf/RuntimeEntanglingRootsAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AEbl"] = new() { Directory = "NightElf/Blink", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["AEsf"] = new() { Directory = "NightElf/RuntimeStarfallAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AEtq"] = new() { Directory = "NightElf/RuntimeTranquilityAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AEmb"] = new() { Directory = "NightElf/RuntimeManaBurnAbility", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AEim"] = new() { Directory = "NightElf/RuntimeImmolationAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AEsh"] = new() { Directory = "NightElf/Shadowstrike", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AEsv"] = new() { Directory = "NightElf/RuntimeVengeanceAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANpi"] = new() { Directory = "Other/ImmolationRed", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Apig"] = new() { Directory = "Other/ImmolationRed", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["ACbb"] = new() { Directory = "Orc/RuntimeBloodlustAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["ACam"] = new() { Directory = "Undead/RuntimeAntiMagicShellAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["ACmi"] = new() { Directory = "Orc/RuntimeMirrorImageAbility", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ACcr"] = new() { Directory = "Undead/RuntimeCrippleAbility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["ACen"] = new() { Directory = "Orc/Ensnare", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ACpa"] = new() { Directory = "Other/RuntimeParasiteAbility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ACtc"] = new() { Directory = "Human/Thunderclap", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ACtb"] = new() { Directory = "Human/StormBolt", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ACsw"] = new() { Directory = "Orc/Shockwave", Suffixes = new[] { "Missile" }, Mount = "origin" },
    };

    private static readonly Dictionary<string, string> ExplicitFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Afbk"] = "Human/RuntimeFeedbackAbility/ArcaneTowerAttack",
        ["Adef"] = "Human/RuntimeDefendAbility/DefendCaster",
        ["Aflk"] = "Human/FlakCannons/FlakTarget",
        ["Afsh"] = "Human/FragmentationShards/FragMissile",
        ["AHta"] = "Human/MagicSentry/MagicSentryCaster",
        ["AOcl"] = "Orc/LightningBolt/LightningBoltMissile",
        ["AOvd"] = "Orc/Voodoo/VoodooAuraTarget",
        ["AOre"] = "Orc/RuntimeReincarnationAbility/ReincarnationTarget",
        ["AOeq"] = "Orc/EarthQuake/EarthquakeTarget",
        ["Apg2"] = "Orc/RuntimePurgeAbility/PurgeBuffTarget",
        ["Absk"] = "Orc/TrollBerserk/TrollBeserkerTarget",
        ["Aakb"] = "Orc/RuntimeCommandAuraAbility/DrumsCasterHeal",
        ["Ara2"] = "NightElf/BattleRoar/RoarTarget",
        ["Aroa"] = "NightElf/BattleRoar/RoarCaster",
        ["Apsh"] = "NightElf/FaerieDragonInvis/FaerieDragon_Invis",
        ["Acor"] = "NightElf/RuntimeCorrosiveBreathAbility/ChimaeraAcidTargetArt",
        ["Aeat"] = "NightElf/RuntimeEatTreeAbility/EatTreeSprite",
        ["Afrz"] = "Undead/RuntimeFreezingBreathAbility/FreezingBreathTargetArt",
        ["Arpl"] = "Undead/ReplenishHealth/ReplenishHealthCasterOverhead",
        ["Arpm"] = "Undead/ReplenishMana/ReplenishManaCasterOverhead",
        ["Aabr"] = "Undead/RuntimeLifeRegenerationAuraAbility/ObsidianRegenAura",
        ["Acdh"] = "Other/StrongDrink/BrewmasterTarget",
        ["ANht"] = "Other/RuntimeHowlOfTerrorAbility/HowlTarget",
        ["ANca"] = "Other/Cleave/CleaveDamageTarget",
        ["Arsp"] = "Other/RuntimeStampedeAbility/StampedeMissile",
        ["ANso"] = "Other/RuntimeSoulBurnAbility/SoulBurnbuff",
        ["ANab"] = "Other/RuntimeAcidBombAbility/BottleImpact",
        ["ANhs"] = "Other/RuntimeHealingSprayAbility/HealBottleMissile",
        ["ANtm"] = "Other/RuntimeTransmuteAbility/PileofGold",
        ["ANia"] = "Other/RuntimeIncinerateAbility/IncinerateBuff",
        ["ANba"] = "Other/RuntimeBlackArrowAbility/BlackArrowMissile",
        ["ANfa"] = "Other/FrostArrows/NagaColdArrowMissile",
        ["AHca"] = "Other/FrostBolt/FrostBoltMissile",
        ["ACcb"] = "Other/FrostBolt/FrostBoltMissile",
        ["ANdc"] = "Demon/DarkConversion/ZombifyTarget",
        ["AUan"] = "Undead/RuntimeAnimateDeadAbility/AnimateDeadTarget",
        ["AUts"] = "Undead/ThornyShield/ThornyShieldTargetChestMountLeft",
        ["Arai"] = "Undead/RaiseSkeletonWarrior/RaiseSkeleton",
        ["Aap2"] = "Undead/PlagueCloud/PlagueCloudCaster",
        ["Aap1"] = "Undead/PlagueCloud/PlagueCloudCaster",
        ["AEsv"] = "NightElf/RuntimeVengeanceAbility/SpiritOfVengeanceBirthMissile",
    };

    private static readonly Dictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static int Count => Skills.Count;

    public static bool TryResolve(string code, out string resPath, out string mount)
    {
        return TryResolve(code, out resPath, out mount, out _);
    }

    public static bool TryResolve(string code, out string resPath, out string mount, out bool stick)
    {
        resPath = null;
        mount = "origin";
        stick = false;
        if (string.IsNullOrWhiteSpace(code)) return false;

        string key = code.Trim();
        if (Cache.TryGetValue(key, out string cached))
        {
            if (cached == null) return false;
            resPath = cached;
            mount = Skills[key].Mount;
            stick = Skills[key].Stick;
            return true;
        }

        if (!Skills.TryGetValue(key, out Entry entry))
        {
            Cache[key] = null;
            return false;
        }

        string name = entry.Directory[(entry.Directory.LastIndexOf('/') + 1)..];
        string basePath = $"Abilities\\Spells\\{entry.Directory.Replace('/', '\\')}\\{name}";

        string picked = null;
        if (ExplicitFiles.TryGetValue(key, out string explicitFile) && !string.IsNullOrEmpty(explicitFile))
        {
            picked = PathResolve.ModelScene("Abilities\\Spells\\" + explicitFile.Replace('/', '\\'));
        }
        if (picked == null)
        {
            foreach (string suffix in entry.Suffixes)
            {
                picked = PathResolve.ModelScene(basePath + suffix);
                if (picked != null) break;
            }
        }
        if (picked == null) picked = PathResolve.ModelScene(basePath);
        if (picked == null)
        {
            Cache[key] = null;
            return false;
        }

        resPath = picked;
        Cache[key] = resPath;
        mount = entry.Mount;
        stick = entry.Stick;
        return true;
    }

    public static bool TryResolveMissile(string code, out string resPath)
    {
        resPath = null;
        if (string.IsNullOrWhiteSpace(code)) return false;
        string key = code.Trim();

        if (ExplicitFiles.TryGetValue(key, out string file) && !string.IsNullOrEmpty(file))
        {
            if (file.IndexOf("Missile", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                resPath = PathResolve.ModelScene("Abilities\\Spells\\" + file.Replace('/', '\\'));
                if (resPath != null) return true;
            }

            int slash = file.LastIndexOf('/');
            if (slash <= 0) return false;
            string directory = file[..slash];
            string directoryName = directory[(directory.LastIndexOf('/') + 1)..];
            resPath = PathResolve.ModelScene($"Abilities\\Spells\\{directory.Replace('/', '\\')}\\{directoryName}Missile");
            return resPath != null;
        }

        if (!Skills.TryGetValue(key, out Entry entry)) return false;
        string name = entry.Directory[(entry.Directory.LastIndexOf('/') + 1)..];
        resPath = PathResolve.ModelScene($"Abilities\\Spells\\{entry.Directory.Replace('/', '\\')}\\{name}Missile");
        return resPath != null;
    }

    public static IReadOnlyCollection<string> Codes => Skills.Keys;
}