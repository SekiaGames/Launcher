using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class War3UiSoundService
{
    private static readonly string[] Races = { "Human", "Orc", "Undead", "NightElf", "Naga" };

    private static readonly Dictionary<string, double> Cooldowns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TownAttackSound"] = 30.0,
        ["UnderAttackSound"] = 30.0,
        ["NoGoldSound"] = 2.0,
        ["NoLumberSound"] = 2.0,
        ["NoFoodSound"] = 2.0,
        ["NoManaSound"] = 2.0,
        ["JobDoneSound"] = 5.0,
        ["UpgradeCompleteSound"] = 5.0,
        ["ResearchCompleteSound"] = 5.0,
        ["CantPlaceSound"] = 2.0,
        ["SubGroupSelectionChange"] = 0.12,
    };

    private static readonly Dictionary<string, string> Prefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NoGoldSound"] = "NoGold",
        ["NoLumberSound"] = "NoLumber",
        ["NoFoodSound"] = "NoFood",
        ["NoManaSound"] = "NoMana",
        ["CantPlaceSound"] = "CantPlace",
        ["InventoryFullSound"] = "InventoryFull",
        ["HeroDiesSound"] = "HeroDies",
        ["AllyHeroDiesSound"] = "AllyHeroDies",
        ["TownAttackSound"] = "TownAttack",
        ["UnderAttackSound"] = "UnderAttack",
        ["JobDoneSound"] = "JobDoneSound",
        ["UpgradeCompleteSound"] = "UpgradeComplete",
        ["ResearchCompleteSound"] = "ResearchComplete",
        ["SubGroupSelectionChange"] = "SubGroupSelectionChange",
    };

    private readonly War3AudioSystem _audio;
    private readonly Dictionary<string, War3UnitSound> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _lastPlay = new(StringComparer.OrdinalIgnoreCase);
    private string _playerRace = "Human";

    public War3UiSoundService(War3AudioSystem audio)
    {
        _audio = audio;
    }

    public void SetPlayerRace(string race)
    {
        if (!string.IsNullOrWhiteSpace(race)) _playerRace = race;
    }

    public void PlaySelectionChanged() => Play("SubGroupSelectionChange");

    public void Play(string key)
    {
        if (_audio == null || string.IsNullOrWhiteSpace(key)) return;
        double now = War3AudioSystem.Now;
        if (Cooldowns.TryGetValue(key, out double cooldown))
        {
            if (_lastPlay.TryGetValue(key, out double last) && now - last < cooldown) return;
            _lastPlay[key] = now;
        }
        string cacheKey = key + "_" + _playerRace;
        if (!_cache.TryGetValue(cacheKey, out War3UnitSound sound))
        {
            sound = War3UnitSound.Create(Resolve(key));
            _cache[cacheKey] = sound;
        }
        sound?.Play2D(_audio);
    }

    private War3SoundTables.Entry Resolve(string key)
    {
        if (War3SoundTables.TryGet(key, out War3SoundTables.Entry direct)) return direct;
        if (!Prefixes.TryGetValue(key, out string prefix)) return null;
        string suffixed = prefix.EndsWith("Sound", StringComparison.OrdinalIgnoreCase)
            ? null : prefix + "Sound";
        var candidates = new List<string> { prefix + _playerRace };
        if (suffixed != null) candidates.Add(suffixed + _playerRace);
        foreach (string race in Races)
        {
            if (race == _playerRace) continue;
            candidates.Add(prefix + race);
            if (suffixed != null) candidates.Add(suffixed + race);
        }
        candidates.Add(prefix + "Generic");
        if (suffixed != null) candidates.Add(suffixed + "Generic");
        if (key == "CantPlaceSound") candidates.Add("PlaceBuildingDefault");
        foreach (string candidate in candidates)
        {
            if (War3SoundTables.TryGet(candidate, out War3SoundTables.Entry entry)) return entry;
        }
        return null;
    }
}
