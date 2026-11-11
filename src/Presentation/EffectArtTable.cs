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
        ["AHds"] = new() { Directory = "Human/DivineShield", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AHad"] = new() { Directory = "Human/DevotionAura", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHav"] = new() { Directory = "Human/Avatar", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["AHre"] = new() { Directory = "Human/Resurrect", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHbz"] = new() { Directory = "Human/Blizzard", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHab"] = new() { Directory = "Human/Brilliance", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHtb"] = new() { Directory = "Human/StormBolt", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AHtc"] = new() { Directory = "Human/Thunderclap", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["Ahea"] = new() { Directory = "Human/Heal", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aply"] = new() { Directory = "Human/Polymorph", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHmt"] = new() { Directory = "Human/MassTeleport", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["AHbn"] = new() { Directory = "Human/Banish", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHfs"] = new() { Directory = "Human/FlameStrike", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHdr"] = new() { Directory = "Other/Drain", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aclf"] = new() { Directory = "Human/CloudOfFog", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afla"] = new() { Directory = "Human/Flare", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Acmg"] = new() { Directory = "Human/ControlMagic", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Afbk"] = new() { Directory = "Human/Feedback", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Adef"] = new() { Directory = "Human/Defend", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Ainf"] = new() { Directory = "Human/InnerFire", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Adis"] = new() { Directory = "Human/DispelMagic", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Amls"] = new() { Directory = "Human/AerialShackles", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aflk"] = new() { Directory = "Human/FlakCannons", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afsh"] = new() { Directory = "Human/FragmentationShards", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Ahrp"] = new() { Directory = "Other/Repair", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aivs"] = new() { Directory = "Human/Invisibility", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Aslo"] = new() { Directory = "Human/Slow", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Asps"] = new() { Directory = "Human/SpellSteal", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AHta"] = new() { Directory = "Human/MagicSentry", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Amfl"] = new() { Directory = "Human/ManaFlare", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANms"] = new() { Directory = "Human/ManaShield", Suffixes = new[] { "Caster", "Target" }, Mount = "overhead" },
        ["Aam2"] = new() { Directory = "Undead/AntiMagicShell", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },

        ["AOcl"] = new() { Directory = "Orc/LightningBolt", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AOsh"] = new() { Directory = "Orc/Shockwave", Suffixes = new[] { "Missile" }, Mount = "origin" },
        ["AOws"] = new() { Directory = "Orc/WarStomp", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOhw"] = new() { Directory = "Orc/HealingWave", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AOhx"] = new() { Directory = "Human/Polymorph", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AOvd"] = new() { Directory = "Orc/Voodoo", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOre"] = new() { Directory = "Orc/Reincarnation", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOeq"] = new() { Directory = "Orc/EarthQuake", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AOmi"] = new() { Directory = "Orc/MirrorImage", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Ablo"] = new() { Directory = "Orc/Bloodlust", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Alsh"] = new() { Directory = "Orc/LightningShield", Suffixes = new[] { "Buff" }, Mount = "overhead", Stick = true },
        ["Apg2"] = new() { Directory = "Orc/Purge", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Absk"] = new() { Directory = "Orc/TrollBerserk", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Aliq"] = new() { Directory = "Orc/LiquidFire", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aspi"] = new() { Directory = "Orc/SpikeBarrier", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aakb"] = new() { Directory = "Orc/WarDrums", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AIcd"] = new() { Directory = "Orc/CommandAura", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["Ara2"] = new() { Directory = "NightElf/BattleRoar", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aroa"] = new() { Directory = "NightElf/BattleRoar", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Arej"] = new() { Directory = "NightElf/Rejuvenation", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Acyc"] = new() { Directory = "NightElf/Cyclone", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afae"] = new() { Directory = "NightElf/FaerieFire", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Afa2"] = new() { Directory = "NightElf/FaerieFire", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Apsh"] = new() { Directory = "NightElf/FaerieDragonInvis", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Atau"] = new() { Directory = "NightElf/Taunt", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Assk"] = new() { Directory = "NightElf/Barkskin", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Ansk"] = new() { Directory = "NightElf/Barkskin", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Acor"] = new() { Directory = "NightElf/CorrosiveBreath", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aeat"] = new() { Directory = "NightElf/EatTree", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aweb"] = new() { Directory = "Undead/Web", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afrb"] = new() { Directory = "Other/BreathOfFrost", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Afrz"] = new() { Directory = "Undead/FreezingBreath", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANen"] = new() { Directory = "Orc/Ensnare", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Acny"] = new() { Directory = "NightElf/Cyclone", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ACfu"] = new() { Directory = "Undead/FrostArmor", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Aro1"] = new() { Directory = "NightElf/Root", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aro2"] = new() { Directory = "NightElf/Uproot", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Asb1"] = new() { Directory = "Other/Submerge", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Asb2"] = new() { Directory = "Other/Submerge", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Anpa"] = new() { Directory = "Other/Parasite", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Anrc"] = new() { Directory = "Demon/RainOfFire", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Andp"] = new() { Directory = "Demon/DarkPortal", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aren"] = new() { Directory = "Other/Repair", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Arst"] = new() { Directory = "Other/Repair", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Arpl"] = new() { Directory = "Undead/ReplenishHealth", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Arpm"] = new() { Directory = "Undead/ReplenishMana", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Aabr"] = new() { Directory = "Undead/RegenerationAura", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Ashm"] = new() { Directory = "NightElf/ShadowMeld", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANvc"] = new() { Directory = "Other/Volcano", Suffixes = new[] { "Volcano" }, Mount = "origin" },
        ["Acef"] = new() { Directory = "Other/StormEarthFire", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANcf"] = new() { Directory = "Other/BreathOfFire", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Acdh"] = new() { Directory = "Other/StrongDrink", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANht"] = new() { Directory = "Other/HowlOfTerror", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANca"] = new() { Directory = "Other/Cleave", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANdo"] = new() { Directory = "Other/Doom", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Arsp"] = new() { Directory = "Other/Stampede", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANso"] = new() { Directory = "Other/SoulBurn", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANab"] = new() { Directory = "Other/AcidBomb", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANhs"] = new() { Directory = "Other/HealingSpray", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANtm"] = new() { Directory = "Other/Transmute", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANia"] = new() { Directory = "Other/Incinerate", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANba"] = new() { Directory = "Other/BlackArrow", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANdr"] = new() { Directory = "Other/Drain", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANrf"] = new() { Directory = "Demon/RainOfFire", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANto"] = new() { Directory = "Other/Tornado", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANfl"] = new() { Directory = "Other/ForkedLightning", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANfa"] = new() { Directory = "Other/FrostArrows", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AHca"] = new() { Directory = "Other/FrostBolt", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ANch"] = new() { Directory = "Other/Charm", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANsl"] = new() { Directory = "Demon/SoulPreservation", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ANdc"] = new() { Directory = "Demon/DarkConversion", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["AUdc"] = new() { Directory = "Undead/DeathCoil", Suffixes = new[] { "SpecialArt" }, Mount = "target" },
        ["AUdp"] = new() { Directory = "Undead/DeathPact", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUdr"] = new() { Directory = "Undead/DarkRitual", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AUau"] = new() { Directory = "Undead/UnholyAura", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUan"] = new() { Directory = "Undead/AnimateDead", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AUfn"] = new() { Directory = "Undead/FrostNova", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUfa"] = new() { Directory = "Undead/FrostArmor", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AUdd"] = new() { Directory = "Undead/DeathAndDecay", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUcs"] = new() { Directory = "Undead/CarrionSwarm", Suffixes = new[] { "Missile" }, Mount = "origin" },
        ["AUsl"] = new() { Directory = "Undead/Sleep", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AUav"] = new() { Directory = "Undead/VampiricAura", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUim"] = new() { Directory = "Undead/Impale", Suffixes = new[] { "Caster", "HitTarget" }, Mount = "origin" },
        ["AUts"] = new() { Directory = "Undead/ThornyShield", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["AUls"] = new() { Directory = "Undead/LocustSwarm", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["Arai"] = new() { Directory = "Undead/RaiseSkeletonWarrior", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Auhf"] = new() { Directory = "Undead/UnholyFrenzy", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Acri"] = new() { Directory = "Undead/Cripple", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Acrs"] = new() { Directory = "Undead/Curse", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["Aps2"] = new() { Directory = "Undead/Possession", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["Adev"] = new() { Directory = "Undead/Devour", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AUns"] = new() { Directory = "Undead/Unsummon", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aap2"] = new() { Directory = "Undead/PlagueCloud", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Aap1"] = new() { Directory = "Undead/PlagueCloud", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["AEfk"] = new() { Directory = "NightElf/FanOfKnives", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AEer"] = new() { Directory = "NightElf/EntanglingRoots", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AEbl"] = new() { Directory = "NightElf/Blink", Suffixes = new[] { "Caster", "Target" }, Mount = "origin" },
        ["AEsf"] = new() { Directory = "NightElf/Starfall", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["AEtq"] = new() { Directory = "NightElf/Tranquility", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AEmb"] = new() { Directory = "NightElf/ManaBurn", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AEim"] = new() { Directory = "NightElf/Immolation", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["AEsh"] = new() { Directory = "NightElf/Shadowstrike", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["AEsv"] = new() { Directory = "NightElf/SpiritOfVengeance", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ANpi"] = new() { Directory = "Other/ImmolationRed", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["Apig"] = new() { Directory = "Other/ImmolationRed", Suffixes = new[] { "Target" }, Mount = "origin" },

        ["ACbb"] = new() { Directory = "Orc/Bloodlust", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["ACam"] = new() { Directory = "Undead/AntiMagicShell", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["ACmi"] = new() { Directory = "Orc/MirrorImage", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ACcr"] = new() { Directory = "Undead/Cripple", Suffixes = new[] { "Target" }, Mount = "overhead", Stick = true },
        ["ACen"] = new() { Directory = "Orc/Ensnare", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ACpa"] = new() { Directory = "Other/Parasite", Suffixes = new[] { "Target" }, Mount = "origin" },
        ["ACtc"] = new() { Directory = "Human/Thunderclap", Suffixes = new[] { "Caster" }, Mount = "origin" },
        ["ACtb"] = new() { Directory = "Human/StormBolt", Suffixes = new[] { "Target" }, Mount = "overhead" },
        ["ACsw"] = new() { Directory = "Orc/Shockwave", Suffixes = new[] { "Missile" }, Mount = "origin" },
    };

    private static readonly Dictionary<string, string> ExplicitFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Afbk"] = "Human/Feedback/ArcaneTowerAttack",
        ["Adef"] = "Human/Defend/DefendCaster",
        ["Aflk"] = "Human/FlakCannons/FlakTarget",
        ["Afsh"] = "Human/FragmentationShards/FragMissile",
        ["AHta"] = "Human/MagicSentry/MagicSentryCaster",
        ["AOcl"] = "Orc/LightningBolt/LightningBoltMissile",
        ["AOvd"] = "Orc/Voodoo/VoodooAuraTarget",
        ["AOre"] = "Orc/Reincarnation/ReincarnationTarget",
        ["AOeq"] = "Orc/EarthQuake/EarthquakeTarget",
        ["Apg2"] = "Orc/Purge/PurgeBuffTarget",
        ["Absk"] = "Orc/TrollBerserk/TrollBeserkerTarget",
        ["Aakb"] = "Orc/WarDrums/DrumsCasterHeal",
        ["Ara2"] = "NightElf/BattleRoar/RoarTarget",
        ["Aroa"] = "NightElf/BattleRoar/RoarCaster",
        ["Apsh"] = "NightElf/FaerieDragonInvis/FaerieDragon_Invis",
        ["Acor"] = "NightElf/CorrosiveBreath/ChimaeraAcidTargetArt",
        ["Aeat"] = "NightElf/EatTree/EatTreeSprite",
        ["Afrz"] = "Undead/FreezingBreath/FreezingBreathTargetArt",
        ["Arpl"] = "Undead/ReplenishHealth/ReplenishHealthCasterOverhead",
        ["Arpm"] = "Undead/ReplenishMana/ReplenishManaCasterOverhead",
        ["Aabr"] = "Undead/RegenerationAura/ObsidianRegenAura",
        ["Acdh"] = "Other/StrongDrink/BrewmasterTarget",
        ["ANht"] = "Other/HowlOfTerror/HowlTarget",
        ["ANca"] = "Other/Cleave/CleaveDamageTarget",
        ["Arsp"] = "Other/Stampede/StampedeMissile",
        ["ANso"] = "Other/SoulBurn/SoulBurnbuff",
        ["ANab"] = "Other/AcidBomb/BottleImpact",
        ["ANhs"] = "Other/HealingSpray/HealBottleMissile",
        ["ANtm"] = "Other/Transmute/PileofGold",
        ["ANia"] = "Other/Incinerate/IncinerateBuff",
        ["ANba"] = "Other/BlackArrow/BlackArrowMissile",
        ["ANfa"] = "Other/FrostArrows/NagaColdArrowMissile",
        ["AHca"] = "Other/FrostBolt/FrostBoltMissile",
        ["ACcb"] = "Other/FrostBolt/FrostBoltMissile",
        ["ANdc"] = "Demon/DarkConversion/ZombifyTarget",
        ["AUan"] = "Undead/AnimateDead/AnimateDeadTarget",
        ["AUts"] = "Undead/ThornyShield/ThornyShieldTargetChestMountLeft",
        ["Arai"] = "Undead/RaiseSkeletonWarrior/RaiseSkeleton",
        ["Aap2"] = "Undead/PlagueCloud/PlagueCloudCaster",
        ["Aap1"] = "Undead/PlagueCloud/PlagueCloudCaster",
        ["AEsv"] = "NightElf/SpiritOfVengeance/SpiritOfVengeanceBirthMissile",
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

    public static IReadOnlyCollection<string> Codes => Skills.Keys;
}