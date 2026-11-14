using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class War3AckSoundService
{
    private readonly War3AudioSystem _audio;
    private readonly Dictionary<string, War3UnitSoundset> _sets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, War3UnitSound> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly NativeUnitAckState _ackState = new();

    public War3AckSoundService(War3AudioSystem audio)
    {
        _audio = audio;
    }

    private War3UnitSoundset SoundsetFor(Unit unit)
    {
        if (unit?.Type == null) return null;
        string name = unit.Type.SoundSet;
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (_sets.TryGetValue(name, out War3UnitSoundset cached)) return cached;
        War3UnitSoundset set = War3UnitSoundset.Create(name);
        _sets[name] = set;
        return set;
    }

    public float PlaySelection(Unit unit, bool wasSelected = true)
        => PlayAck(unit, selection: true, attack: false, wasSelected);

    public void ResetSelection() => _ackState.ResetSelection();

    public int SelectionCount => _ackState.SelectionCount;

    public float PlayOrderAck(Unit unit, bool attack)
    {
        _ackState.ResetSelection();
        return PlayAck(unit, selection: false, attack);
    }

    public float PlayReady(Unit unit)
        => SoundsetFor(unit)?.Ready.Play2D(_audio) ?? 0f;

    public static string CombatSoundLabel(WeaponSound weaponSound, string armor)
    {
        if ((uint)weaponSound >= 24) return null;
        string material = armor is "Flesh" or "Metal" or "Wood" or "Ethereal" or "Stone"
            ? armor : "Unknown";
        return (weaponSound == WeaponSound.Whoknows ? "Unknown" : weaponSound.ToString()) + material;
    }

    public void PlayCombatHit(Unit source, Unit target, Vector3 position, string armorOverride = null)
    {
        if (_audio == null || source?.Type == null) return;
        string armor = armorOverride ?? target?.Type?.ArmorSound;
        if (string.IsNullOrWhiteSpace(armor))
            armor = target != null && target.IsBuilding ? "Wood" : "Flesh";
        string key = CombatSoundLabel(source.Type.WeaponSoundCode, armor);
        if (key == null) return;
        if (!_cache.TryGetValue(key, out War3UnitSound sound))
        {
            War3SoundTables.TryGet(key, out War3SoundTables.Entry entry);
            sound = War3UnitSound.Create(entry);
            _cache[key] = sound;
        }
        sound.Play3D(_audio, position);
    }

    public void PlaySpellCast(Unit caster, string abilityRawcode, Vector3 position,
        IRuntimeObjectData definition = null)
    {
        if (_audio == null || string.IsNullOrWhiteSpace(abilityRawcode)) return;
        string key = abilityRawcode;
        string[] soundFields = { "Aas", "Asp", "Ahe", "Acs", "Acst", "Sounds", "Ubz" };
        foreach (string field in soundFields)
        {
            string value = definition?.GetString(field, 1) ?? "";
            if (!string.IsNullOrWhiteSpace(value))
            {
                key = value;
                break;
            }
        }
        if (!_cache.TryGetValue(key, out War3UnitSound sound))
        {
            bool found = War3SoundTables.TryGet(key, out War3SoundTables.Entry entry);
            if (!found) found = War3SoundTables.TryGet(abilityRawcode, out entry);
            sound = War3UnitSound.Create(found ? entry : null);
            _cache[key] = sound;
        }
        if (!sound.IsSilent)
        {
            sound.Play3D(_audio, position, .8f);
            return;
        }
        if (!SpellSoundFallback.TryGetValue(abilityRawcode, out string path)) return;
        if (!War3AudioSystem.Exists(path))
        {
            GD.PushWarning($"[AUDIO] 施法音缺失 {path}");
            return;
        }
        AudioStream stream = War3SoundTables.LoadClip(path);
        _audio.Play3D(stream, abilityRawcode, position, .8f, 1f, 128f, 900f);
    }

private static readonly Dictionary<string, string> SpellSoundFallback = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ACbc"] = @"Abilities\Spells\Other\BreathOfFire\BreathOfFire1.wav",
        ["ACc2"] = @"Abilities\Spells\Other\CrushingWave\CrushingWaveCaster1.wav",
        ["ACc3"] = @"Abilities\Spells\Other\CrushingWave\CrushingWaveCaster1.wav",
        ["ACca"] = @"Abilities\Spells\Undead\CarrionSwarm\CarrionSwarmLaunch1.wav",
        ["ACcv"] = @"Abilities\Spells\Other\CrushingWave\CrushingWaveCaster1.wav",
        ["ACd2"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["ACdc"] = @"Abilities\Spells\Undead\DeathCoil\DeathCoilMissileLaunch1.wav",
        ["ACdm"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["ACfn"] = @"Abilities\Spells\Undead\FrostNova\FrostNovaTarget1.wav",
        ["ACfs"] = @"Abilities\Spells\Human\FlameStrike\FlameStrikeBirth1.wav",
        ["AChV"] = @"Abilities\Spells\Orc\HealingWave\HealingWave.wav",
        ["AChx"] = @"Abilities\Spells\Human\Polymorph\PolymorphDone.wav",
        ["ACif"] = @"Abilities\Spells\Human\InnerFire\PriestCastAttack1.wav",
        ["ACmp"] = @"Abilities\Spells\Undead\Impale\ImpaleLaunch1.wav",
        ["ACpu"] = @"Abilities\Spells\Orc\Purge\PurgeTarget1.wav",
        ["ACpy"] = @"Abilities\Spells\Human\Polymorph\PolymorphDone.wav",
        ["ACr1"] = @"Abilities\Spells\NightElf\BattleRoar\BattleRoar.wav",
        ["ACrd"] = @"Abilities\Spells\Undead\RaiseSkeletonWarrior\RaiseSkeleton.wav",
        ["ACrn"] = @"Abilities\Spells\Orc\Reincarnation\Reincarnation.wav",
        ["ACro"] = @"Abilities\Spells\NightElf\BattleRoar\BattleRoar.wav",
        ["ACs7"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ACs8"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ACs9"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ACsf"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ACsi"] = @"Abilities\Spells\Other\Silence\Silence1.wav",
        ["ACsp"] = @"Abilities\Spells\Other\CreepSleep\CreepSleepWhat1.wav",
        ["ACsw"] = @"Abilities\Spells\Human\Slow\SorceressCastAttack1.wav",
        ["ACt2"] = @"Abilities\Spells\Human\Thunderclap\ThunderclapCaster.wav",
        ["ACtc"] = @"Abilities\Spells\Human\Thunderclap\ThunderclapCaster.wav",
        ["AEbl"] = @"Abilities\Spells\NightElf\Blink\BlinkBirth1.wav",
        ["AEer"] = @"Abilities\Spells\NightElf\EntanglingRoots\EntanglingRootsTarget1.wav",
        ["AEfk"] = @"Abilities\Spells\NightElf\FanOfKnives\FanOfKnivesMissileHit1.wav",
        ["AEim"] = @"Abilities\Spells\NightElf\Immolation\ImmolationTarget1.wav",
        ["AEmb"] = @"Abilities\Spells\NightElf\ManaBurn\ManaDrainTarget1.wav",
        ["AEme"] = @"Abilities\Spells\Demon\Metamorphosis\Metamorphosis.wav",
        ["AEsd"] = @"Abilities\Spells\NightElf\Starfall\StarfallCaster1.wav",
        ["AEsf"] = @"Abilities\Spells\NightElf\Starfall\StarfallCaster1.wav",
        ["AEsv"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AEtq"] = @"Abilities\Spells\NightElf\Tranquility\Tranquility.wav",
        ["AEtr"] = @"Abilities\Spells\NightElf\Tranquility\Tranquility.wav",
        ["AHav"] = @"Abilities\Spells\Human\Avatar\Avatar.wav",
        ["AHbn"] = @"Abilities\Spells\Human\Banish\BanishCaster.wav",
        ["AHbz"] = @"Abilities\Spells\Human\Blizzard\BlizzardLoop1.wav",
        ["AHds"] = @"Abilities\Spells\Human\DivineShield\DivineShield.wav",
        ["AHer"] = @"Abilities\Spells\Other\Levelup\Levelupcaster.wav",
        ["AHfb"] = @"Abilities\Spells\Demon\RainOfFire\RainOfFireLoop1.wav",
        ["AHfs"] = @"Abilities\Spells\Human\FlameStrike\FlameStrikeBirth1.wav",
        ["AHhb"] = @"Abilities\Spells\Human\HolyBolt\HolyBolt.wav",
        ["AHmt"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["AHpx"] = @"Abilities\Spells\Human\MarkOfChaos\MarkOfChaos.wav",
        ["AHre"] = @"Abilities\Spells\Human\Resurrect\ResurrectTarget.wav",
        ["AHtb"] = @"Abilities\Spells\Human\StormBolt\StormBoltLaunch.wav",
        ["AHtc"] = @"Abilities\Spells\Human\Thunderclap\ThunderclapCaster.wav",
        ["AIam"] = @"Abilities\Spells\Items\AIam\Tomes.wav",
        ["AIbk"] = @"Abilities\Spells\NightElf\Blink\BlinkBirth1.wav",
        ["AIcb"] = @"Abilities\Spells\Items\OrbCorruption\OrbOfCorruptionMissile.wav",
        ["AIcm"] = @"Abilities\Spells\Human\SpellSteal\SpellStealMissile.wav",
        ["AIco"] = @"Abilities\Spells\Items\AIco\CharmTarget1.wav",
        ["AIdb"] = @"Abilities\Spells\Items\AIre\RestorationPotion.wav",
        ["AIdd"] = @"Abilities\Spells\Human\Defend\DefendCaster.wav",
        ["AIdi"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["AIdp"] = @"Abilities\Spells\Undead\DeathPact\DeathPactTargetBirth1.wav",
        ["AIds"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["AIfa"] = @"Abilities\Spells\Human\Flare\FlareTarget1.wav",
        ["AIfd"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIff"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIfh"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIfr"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIfs"] = @"Abilities\Spells\Undead\RaiseSkeletonWarrior\RaiseSkeleton.wav",
        ["AIfu"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIgm"] = @"Abilities\Spells\Items\AIam\Tomes.wav",
        ["AIgo"] = @"Abilities\Spells\Items\ResourceItems\BundleOfLumber.wav",
        ["AIhl"] = @"Abilities\Spells\Human\HolyBolt\HolyBolt.wav",
        ["AIil"] = @"Abilities\Spells\Items\AIil\WandOfIllusionTarget1.wav",
        ["AIir"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIlu"] = @"Abilities\Spells\Items\ResourceItems\BundleOfLumber.wav",
        ["AIm1"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["AIm2"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["AIma"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["AImr"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["AImt"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["AIos"] = @"Abilities\Spells\Human\Slow\SorceressCastAttack1.wav",
        ["AIp5"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["AIp6"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["AIpl"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["AIpr"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["AIps"] = @"Abilities\Spells\Orc\Purge\PurgeTarget1.wav",
        ["AIra"] = @"Abilities\Spells\Items\AIre\RestorationPotion.wav",
        ["AIrc"] = @"Abilities\Spells\Orc\Reincarnation\Reincarnation.wav",
        ["AIre"] = @"Abilities\Spells\Items\AIre\RestorationPotion.wav",
        ["AIrl"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["AIrr"] = @"Abilities\Spells\NightElf\BattleRoar\BattleRoar.wav",
        ["AIrs"] = @"Abilities\Spells\Human\Resurrect\ResurrectTarget.wav",
        ["AIrt"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["AIrx"] = @"Abilities\Spells\Human\Resurrect\ResurrectTarget.wav",
        ["AIsh"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIsl"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["AIso"] = @"Abilities\Spells\Items\AIso\SoulGem.wav",
        ["AIte"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["AItp"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["AIuw"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AIwm"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ANbl"] = @"Abilities\Spells\NightElf\Blink\BlinkBirth1.wav",
        ["ANbr"] = @"Abilities\Spells\NightElf\BattleRoar\BattleRoar.wav",
        ["ANcl"] = @"Abilities\Spells\Undead\DeathPact\DeathPactTargetBirth1.wav",
        ["ANef"] = @"Abilities\Spells\Human\Polymorph\PolymorphDone.wav",
        ["ANfs"] = @"Abilities\Spells\Human\FlameStrike\FlameStrikeBirth1.wav",
        ["ANht"] = @"Abilities\Spells\Other\HowlOfTerror\HowlOfTerror.wav",
        ["ANhw"] = @"Abilities\Spells\Orc\HealingWave\HealingWave.wav",
        ["ANhx"] = @"Abilities\Spells\Human\Polymorph\PolymorphDone.wav",
        ["ANmd"] = @"Abilities\Spells\Other\Monsoon\MonsoonLightningHit.wav",
        ["ANpr"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["ANr2"] = @"Abilities\Spells\Orc\Reincarnation\Reincarnation.wav",
        ["ANrl"] = @"Abilities\Spells\Other\ANrl\FountainOfLifeLoop1.wav",
        ["ANrm"] = @"Abilities\Spells\Other\ANrl\FountainOfLifeLoop1.wav",
        ["ANrn"] = @"Abilities\Spells\Orc\Reincarnation\Reincarnation.wav",
        ["ANsa"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["ANsg"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ANsi"] = @"Abilities\Spells\Other\Silence\Silence1.wav",
        ["ANsq"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ANss"] = @"Abilities\Spells\Items\SpellShieldAmulet\SpellShieldImpact1.wav",
        ["ANst"] = @"Abilities\Spells\Other\Stampede\StampedeCaster1.wav",
        ["ANsw"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["ANta"] = @"Abilities\Spells\NightElf\Taunt\Taunt.wav",
        ["ANwk"] = @"Abilities\Spells\Orc\WindWalk\WindWalk.wav",
        ["ANwm"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AOcl"] = @"Abilities\Spells\Orc\LightningBolt\LightningBolt.wav",
        ["AOeq"] = @"Abilities\Spells\Orc\EarthQuake\EarthquakeLoop1.wav",
        ["AOhw"] = @"Abilities\Spells\Orc\HealingWave\HealingWave.wav",
        ["AOhx"] = @"Abilities\Spells\Human\Polymorph\PolymorphDone.wav",
        ["AOmi"] = @"Abilities\Spells\Orc\MirrorImage\MirrorImage.wav",
        ["AOr3"] = @"Abilities\Spells\Orc\Reincarnation\Reincarnation.wav",
        ["AOre"] = @"Abilities\Spells\Orc\Reincarnation\Reincarnation.wav",
        ["AOsf"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["AOsh"] = @"Abilities\Spells\Orc\Shockwave\Shockwave.wav",
        ["AOwk"] = @"Abilities\Spells\Orc\WindWalk\WindWalk.wav",
        ["APdi"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["APmg"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["APmr"] = @"Abilities\Spells\Items\AIma\ManaPotion.wav",
        ["APra"] = @"Abilities\Spells\Items\AIre\RestorationPotion.wav",
        ["APrl"] = @"Abilities\Spells\Human\Resurrect\ResurrectTarget.wav",
        ["APrr"] = @"Abilities\Spells\Human\Resurrect\ResurrectTarget.wav",
        ["AUcs"] = @"Abilities\Spells\Undead\CarrionSwarm\CarrionSwarmLaunch1.wav",
        ["AUdc"] = @"Abilities\Spells\Undead\DeathCoil\DeathCoilMissileLaunch1.wav",
        ["AUdd"] = @"Abilities\Spells\Undead\DeathandDecay\DeathAndDecayLoop1.wav",
        ["AUdp"] = @"Abilities\Spells\Undead\DeathPact\DeathPactTargetBirth1.wav",
        ["AUdr"] = @"Abilities\Spells\Undead\DarkRitual\DarkRitualTarget1.wav",
        ["AUds"] = @"Abilities\Spells\Undead\DarkSummoning\DarkSummoningLaunch1.wav",
        ["AUfa"] = @"Abilities\Spells\Undead\FrostArmor\FrostArmorTarget1.wav",
        ["AUfn"] = @"Abilities\Spells\Undead\FrostNova\FrostNovaTarget1.wav",
        ["AUim"] = @"Abilities\Spells\Undead\Impale\ImpaleLaunch1.wav",
        ["AUin"] = @"Abilities\Spells\Undead\DarkSummoning\DarkSummoningLaunch1.wav",
        ["AUsl"] = @"Abilities\Spells\Undead\Sleep\SleepBirth1.wav",
        ["Aadm"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["Aaha"] = @"Abilities\Spells\Undead\UndeadMine\AcolyteMining.wav",
        ["Aall"] = @"Abilities\Spells\Other\Aneu\Aneu.wav",
        ["Aams"] = @"Abilities\Spells\Undead\AntiMagicShell\AntiMagicShellBirth1.wav",
        ["Aast"] = @"Abilities\Spells\Orc\AncestralSpirit\AncestralSpirit.wav",
        ["Abgm"] = @"Abilities\Spells\Undead\UndeadMine\MineDomeLoop1.wav",
        ["Ablo"] = @"Abilities\Spells\Orc\Bloodlust\BloodlustTarget.wav",
        ["Abtl"] = @"Abilities\Spells\Orc\BattleStations\OrcBurrowBattleStationsWhat1.wav",
        ["Abun"] = @"Abilities\Spells\Other\LoadUnload\Loading.wav",
        ["Acar"] = @"Abilities\Spells\Other\LoadUnload\Loading.wav",
        ["Acef"] = @"Abilities\Spells\Human\Polymorph\PolymorphDone.wav",
        ["Acht"] = @"Abilities\Spells\Other\HowlOfTerror\HowlOfTerror.wav",
        ["Acmg"] = @"Abilities\Spells\Human\ControlMagic\ControlMagic.wav",
        ["Aco3"] = @"Abilities\Spells\NightElf\HippogryphTaming\TamingTarget1.wav",
        ["Acoa"] = @"Abilities\Spells\NightElf\HippogryphTaming\TamingTarget1.wav",
        ["Acoh"] = @"Abilities\Spells\NightElf\HippogryphTaming\TamingTarget1.wav",
        ["Acpf"] = @"Abilities\Spells\Orc\EtherealForm\SpiritWalkerMorph.wav",
        ["Acri"] = @"Abilities\Spells\Undead\Cripple\CrippleTarget1.wav",
        ["Acrs"] = @"Abilities\Spells\Undead\Curse\CurseTarget1.wav",
        ["Adch"] = @"Abilities\Spells\Orc\Disenchant\Disenchant.wav",
        ["Adcn"] = @"Abilities\Spells\Orc\Disenchant\Disenchant.wav",
        ["Adec"] = @"Abilities\Spells\NightElf\HippogryphTaming\TamingTarget1.wav",
        ["Adef"] = @"Abilities\Spells\Human\Defend\DefendCaster.wav",
        ["Adis"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["Adsm"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["Adtn"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["Advm"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["Aeat"] = @"Abilities\Spells\NightElf\EatTree\EatTreeTarget1.wav",
        ["Aenc"] = @"Abilities\Spells\NightElf\EntangleMine\WispEntangleMine.wav",
        ["Aent"] = @"Abilities\Spells\NightElf\EntangleMine\TreeOfLifeGoldMineBirth1.wav",
        ["Aetf"] = @"Abilities\Spells\Orc\EtherealForm\SpiritWalkerMorph.wav",
        ["Afbb"] = @"Abilities\Spells\Human\Feedback\Feedback.wav",
        ["Afbk"] = @"Abilities\Spells\Human\Feedback\Feedback.wav",
        ["Afbt"] = @"Abilities\Spells\Human\Feedback\Feedback.wav",
        ["Afla"] = @"Abilities\Spells\Human\Flare\FlareTarget1.wav",
        ["Aflk"] = @"Abilities\Spells\Human\FlakCannons\FlakCannon.wav",
        ["Ahea"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["Ahid"] = @"Abilities\Spells\NightElf\ShadowMeld\ShadowMeld1.wav",
        ["Ainf"] = @"Abilities\Spells\Human\InnerFire\InnerFireBirth.wav",
        ["Aivs"] = @"Abilities\Spells\Human\Invisibility\InvisibilityTarget.wav",
        ["Alsh"] = @"Abilities\Spells\Orc\LightningShield\LightningShieldTarget.wav",
        ["Amb2"] = @"Abilities\Spells\Undead\ReplenishMana\SpiritTouch.wav",
        ["Ambb"] = @"Abilities\Spells\NightElf\ManaBurn\ManaDrainTarget1.wav",
        ["Ambd"] = @"Abilities\Spells\NightElf\ManaBurn\ManaDrainTarget1.wav",
        ["Ambt"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["Amdf"] = @"Abilities\Spells\Human\Defend\DefendCaster.wav",
        ["Amec"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["Amic"] = @"Abilities\Spells\Human\CallToArms\HumanCallToArmsWhat1.wav",
        ["Amnb"] = @"Abilities\Spells\NightElf\ManaBurn\ManaDrainTarget1.wav",
        ["Amov"] = @"Abilities\Spells\Human\MassTeleport\MassTeleportTarget.wav",
        ["Amtc"] = @"Abilities\Spells\Other\LoadUnload\Loading.wav",
        ["Andm"] = @"Abilities\Spells\Human\DispelMagic\DispelMagicTarget.wav",
        ["Ane2"] = @"Abilities\Spells\Other\Aneu\Aneu.wav",
        ["Aneu"] = @"Abilities\Spells\Other\Aneu\Aneu.wav",
        ["Anh1"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["Anh2"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["Anhe"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["Apg2"] = @"Abilities\Spells\Orc\Purge\PurgeTarget1.wav",
        ["Aply"] = @"Abilities\Spells\Human\Polymorph\PolymorphDone.wav",
        ["Aprg"] = @"Abilities\Spells\Orc\Purge\PurgeTarget1.wav",
        ["Apsh"] = @"Abilities\Spells\NightElf\ShadowMeld\ShadowMeld1.wav",
        ["Ara2"] = @"Abilities\Spells\NightElf\BattleRoar\BattleRoar.wav",
        ["Arai"] = @"Abilities\Spells\Undead\RaiseSkeletonWarrior\RaiseSkeleton.wav",
        ["Arej"] = @"Abilities\Spells\NightElf\Rejuvenation\RejuvenationTarget1.wav",
        ["Aren"] = @"Abilities\Spells\NightElf\Renew\CityScapeMagicRunesLoop1.wav",
        ["Aroa"] = @"Abilities\Spells\NightElf\BattleRoar\BattleRoar.wav",
        ["Arpb"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["Arpl"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
        ["Arpm"] = @"Abilities\Spells\Undead\ReplenishMana\SpiritTouch.wav",
        ["Arsg"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["Arsp"] = @"Abilities\Spells\Other\Stampede\StampedeCaster1.wav",
        ["Arsq"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["Arst"] = @"Abilities\Spells\Undead\Restore\CityscapeCrystalLampLoop1.wav",
        ["Ashm"] = @"Abilities\Spells\NightElf\ShadowMeld\ShadowMeld1.wav",
        ["Asla"] = @"Abilities\Spells\Other\CreepSleep\CreepSleepWhat1.wav",
        ["Aslo"] = @"Abilities\Spells\Human\Slow\SlowTarget.wav",
        ["Aslp"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["Aspl"] = @"Abilities\Spells\Orc\SpiritLink\SpiritLink1.wav",
        ["Asps"] = @"Abilities\Spells\Human\SpellSteal\SpellStealMissile.wav",
        ["Aste"] = @"Abilities\Spells\Other\Drain\LifeDrain.wav",
        ["Atau"] = @"Abilities\Spells\NightElf\Taunt\Taunt.wav",
        ["Auhf"] = @"Abilities\Spells\Undead\UnholyFrenzy\UnholyFrenzyBirth1.wav",
        ["Avng"] = @"Abilities\Spells\Orc\FeralSpirit\FeralSpiritTarget1.wav",
        ["Aweb"] = @"Abilities\Spells\Undead\Web\WebMissileLaunch1.wav",
        ["Awh2"] = @"Abilities\Spells\NightElf\TargetArtLumber\WispHarvestLumber1.wav",
        ["Awha"] = @"Abilities\Spells\NightElf\TargetArtLumber\WispHarvestLumber1.wav",
        ["Awhe"] = @"Abilities\Spells\Human\Heal\HealTarget.wav",
    };
    private float PlayAck(Unit unit, bool selection, bool attack, bool wasSelected = true)
    {
        if (unit == null) return 0f;
        War3UnitSoundset set = SoundsetFor(unit);
        int simId = unit.SimulationId;
        if (selection) _ackState.BeginSelection(simId, wasSelected);
        if (_audio == null || set == null) return 0f;
        double now = War3AudioSystem.Now;
        if (!_ackState.CanRespond(simId, now)) return 0f;
        int index = selection ? _ackState.PissedIndex(set.Pissed.Count) : -1;
        War3UnitSound ack = selection ? (index >= 0 ? set.Pissed : set.What)
            : attack ? set.YesAttack : set.Yes;
        if (!_ackState.CanPlayUsername(ack.ResponseUsername, now)) return 0f;
        float duration = ack.Play2D(_audio, 1f, index);
        if (duration <= 0f) return 0f;
        _ackState.Played(simId, now, duration, selection, ack.ResponseUsername);
        return duration;
    }
}
