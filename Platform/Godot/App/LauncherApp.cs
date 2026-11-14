using Godot;
using OpenWC3.Game.Diagnostics;
using System;
using System.Collections.Generic;
using OpenWC3.Game;
using OpenWC3.Game.UI;

namespace OpenWC3;

public sealed partial class LauncherApp : Node
{
    private MenuScreenHost _menuHost;
    private Node _current
    {
        get => _menuHost?.Current;
        set => _menuHost?.Adopt(value);
    }
    private string _shotPath;
    private float _shotDelay = -1f;
    private float _elapsed;
    private bool _shotTaken;
    private int _cameraPreset = -1;
    private Vector2? _focusMapPoint;
    private string _cinematicModel;
    private bool _orderTest;
    private bool _simTest;
    private bool _clickTest;
    private bool _gatherTest;
    private bool _trainTest;
    private Vector2? _moveTo;
    private bool _pathProbe;
    private bool _lumberTest;
    private bool _buildTest;
    private bool _fast;
    private bool _housekeeping;
    private bool _dumpUnits;
    private bool _creepProbe;
    private bool _heroProbe;
    private bool _combatProbe;
    private bool _stateProbe;
    private string _fdfProbe;
    private string _fdfBindGen;
    private bool _uiHud;
        private int _selectedRace;
    private bool _preGameProbe;
    private string _uiScreen;
    private bool _effectProbe;
    private bool _attachProbe;
    private bool _reviveProbe;
    private bool _skillProbe;
    private bool _morphProbe;
    private bool _cardProbe;
    private bool _hireProbe;
    private bool _boltProbe;
    private bool _rallyProbe;
    private bool _subgroupProbe;
    private readonly List<string> _spawnItems = new();
    private bool _aggro;
    private bool _noAi;
    private int _viewPlayer = -1;
    private bool _fogOff;
    private bool _musicOff;
    private bool _damageNumbers;
    private int _aiDifficulty = 1;
    private float _daySeconds = -1f;
    private int _players;
    private readonly List<int> _slots = new();
    private readonly List<List<int>> _teams = new();
    private int _startGold = -1;
    private int _startLumber = -1;
    private float _timeScale = 1f;

    private static string[] MergeAngleBracketedArgs(string[] raw)
    {
        var result = new List<string>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            string argument = raw[i];
            int open = argument.IndexOf('<');
            if (open >= 0 && argument.IndexOf('>', open) < 0)
            {
                int end = i;
                while (end + 1 < raw.Length && !raw[end].Contains('>'))
                {
                    end++;
                }
                if (raw[end].Contains('>'))
                {
                    argument = string.Join(' ', raw, i, end - i + 1);
                    i = end;
                }
            }
            result.Add(argument.Replace("<", string.Empty).Replace(">", string.Empty));
        }
        return result.ToArray();
    }

    public override void _Ready()
    {
        Assets.Init();
        _menuHost = new MenuScreenHost(this);

        string playPath = null;
        foreach (string argument in MergeAngleBracketedArgs(OS.GetCmdlineUserArgs()))
        {
            if (argument.StartsWith("--play="))
            {
                playPath = argument["--play=".Length..];
            }
            else if (argument.StartsWith("--shot="))
            {
                string[] parts = argument["--shot=".Length..].Split('@');
                _shotPath = parts[0];
                _shotDelay = parts.Length > 1 && float.TryParse(parts[1], out float seconds) ? seconds : 3f;
            }
            else if (argument == "--order-test")
            {
                _orderTest = true;
            }
            else if (argument == "--sim-test")
            {
                _simTest = true;
            }
            else if (argument == "--click-test")
            {
                _clickTest = true;
            }
            else if (argument == "--gather-test")
            {
                _gatherTest = true;
            }
            else if (argument == "--train-test")
            {
                _trainTest = true;
            }
            else if (argument == "--path-probe")
            {
                _pathProbe = true;
            }
            else if (argument == "--lumber-test")
            {
                _lumberTest = true;
            }
            else if (argument == "--build-test")
            {
                _buildTest = true;
            }
            else if (argument == "--fast")
            {
                _fast = true;
            }
            else if (argument.StartsWith("--time=") && float.TryParse(argument["--time=".Length..], out float scale))
            {
                _timeScale = Mathf.Clamp(scale, 0.02f, 1f);
            }
            else if (argument == "--aggro")
            {
                _aggro = true;
            }
            else if (argument.StartsWith("--players=") && int.TryParse(argument["--players=".Length..], out int players))
            {
                _players = Mathf.Clamp(players, 1, 12);
            }
            else if (argument.StartsWith("--slots="))
            {
                foreach (string part in argument["--slots=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part.Trim(), out int slot) && slot >= 0 && slot <= 11) _slots.Add(slot);
                }
            }
            else if (argument.StartsWith("--teams="))
            {
                foreach (string group in argument["--teams=".Length..].Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var team = new List<int>();
                    foreach (string part in group.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(part.Trim(), out int slot) && slot >= 0 && slot <= 11) team.Add(slot);
                    }
                    if (team.Count > 0) _teams.Add(team);
                }
            }
            else if (argument.StartsWith("--view=") && int.TryParse(argument["--view=".Length..], out int view))
            {
                _viewPlayer = Mathf.Clamp(view, 0, 11);
            }
            else if (argument.StartsWith("--day-seconds=") && float.TryParse(argument["--day-seconds=".Length..], out float daySeconds))
            {
                _daySeconds = Mathf.Clamp(daySeconds, 60f, 100000f);
            }
            else if (argument == "--no-music")
            {
                _musicOff = true;
            }
            else if (argument == "--fog-off")
            {
                _fogOff = true;
            }
            else if (argument == "--no-ai")
            {
                _noAi = true;
            }
            else if (argument == "--hero-probe")
            {
                _heroProbe = true;
            }
            else if (argument == "--creep-probe")
            {
                _creepProbe = true;
            }
            else if (argument == "--combat-probe")
            {
                _combatProbe = true;
            }
            else if (argument == "--effect-probe")
            {
                _effectProbe = true;
            }
            else if (argument == "--attach-probe")
            {
                _attachProbe = true;
            }
            else if (argument.StartsWith("--ui-screen="))
            {
                _uiScreen = argument["--ui-screen=".Length..];
            }
            else if (argument.StartsWith("--fdf-probe="))
            {
                _fdfProbe = argument["--fdf-probe=".Length..];
            }
            else if (argument.StartsWith("--race=") && int.TryParse(argument["--race=".Length..], out int raceId))
            {
                _selectedRace = Mathf.Clamp(raceId, 0, 4);
            }
            else if (argument == "--pre-game-probe")
            {
                _preGameProbe = true;
            }
            else if (argument == "--ui-hud")
            {
                _uiHud = true;
            }
            else if (argument == "--fdf-bindgen")
            {
                _fdfBindGen = "";
            }
            else if (argument.StartsWith("--fdf-bindgen="))
            {
                _fdfBindGen = argument["--fdf-bindgen=".Length..];
            }
            else if (argument == "--state-probe")
            {
                _stateProbe = true;
            }
            else if (argument == "--revive-probe")
            {
                _reviveProbe = true;
            }
            else if (argument == "--skill-probe")
            {
                _skillProbe = true;
            }
            else if (argument == "--morph-probe")
            {
                _morphProbe = true;
            }
            else if (argument == "--card-probe")
            {
                _cardProbe = true;
            }
            else if (argument == "--hire-probe")
            {
                _hireProbe = true;
            }
            else if (argument == "--bolt-probe")
            {
                _boltProbe = true;
            }
            else if (argument == "--rally-probe")
            {
                _rallyProbe = true;
            }
            else if (argument == "--subgroup-probe")
            {
                _subgroupProbe = true;
            }
            else if (argument.StartsWith("--spawn-item="))
            {
                foreach (string item in argument["--spawn-item=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    _spawnItems.Add(item.Trim());
                }
            }
            else if (argument == "--damage-numbers")
            {
                _damageNumbers = true;
            }
            else if (argument.StartsWith("--ai-difficulty=") && int.TryParse(argument["--ai-difficulty=".Length..], out int difficulty))
            {
                _aiDifficulty = Mathf.Clamp(difficulty, 0, 2);
            }
            else if (argument == "--dump-units")
            {
                _dumpUnits = true;
            }
            else if (argument == "--housekeeping")
            {
                _housekeeping = true;
            }
            else if (argument == "--rich")
            {
                _startGold = 1000000;
                _startLumber = 1000000;
            }
            else if (argument.StartsWith("--gold=") && int.TryParse(argument["--gold=".Length..], out int gold))
            {
                _startGold = gold;
            }
            else if (argument.StartsWith("--lumber=") && int.TryParse(argument["--lumber=".Length..], out int lumber))
            {
                _startLumber = lumber;
            }
            else if (argument.StartsWith("--move-to="))
            {
                string[] parts = argument["--move-to=".Length..].Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], out float mx) && float.TryParse(parts[1], out float my))
                {
                    _moveTo = new Vector2(mx, my);
                }
            }
            else if (argument.StartsWith("--cam=") && int.TryParse(argument["--cam=".Length..], out int preset))
            {
                _cameraPreset = preset;
            }
            else if (argument.StartsWith("--focus="))
            {
                string[] parts = argument["--focus=".Length..].Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], out float mx) && float.TryParse(parts[1], out float my))
                {
                    _focusMapPoint = new Vector2(mx, my);
                }
            }
            else if (argument.StartsWith("--cinematic="))
            {
                _cinematicModel = argument["--cinematic=".Length..];
            }
        }

        if (_fdfBindGen != null)
        {
            int code = FdfBindGen.Run(_fdfBindGen);
            GetTree().Quit(code);
            return;
        }

        if (_dumpUnits)
        {
            DumpUnits(playPath);
            GetTree().Quit();
            return;
        }

        if (_combatProbe)
        {
            int code = CombatProbe.Run();
            GetTree().Quit(code);
            return;
        }

        if (_uiHud)
        {
            Clear();
            var hud = new OpenWC3.Game.UI.GameHudScreen { Name = "Hud" };
            AddChild(hud);
            hud.Load();
            _current = hud;
            GD.Print($"[HUD] 控制台与顶栏已创建，根帧 {hud.FrameCount}");
            return;
        }

        if (!string.IsNullOrEmpty(_uiScreen))
        {
            string[] parts = _uiScreen.Split('|');
            var screen = new OpenWC3.Game.UI.FdfScreen();
            AddChild(screen);
            screen.Load(parts[0], parts.Length > 1 ? parts[1] : "Loading");
            string template = parts.Length > 1 ? parts[1] : "Loading";
            GD.Print($"[UI] 已创建 {template}，帧数 {screen.FrameCount}");
            return;
        }

        if (!string.IsNullOrEmpty(_fdfProbe))
        {
            int fdfCode = FdfProbe.Run(_fdfProbe);
            GetTree().Quit(fdfCode);
            return;
        }

        if (_stateProbe)
        {
            int code = CombatProbe.RunStates();
            GetTree().Quit(code);
            return;
        }

        if (_effectProbe)
        {
            RunEffectProbe();
            GetTree().Quit(0);
            return;
        }

        if (_attachProbe)
        {
            GetTree().Quit(AttachmentProbe.Run());
            return;
        }

        if (_simTest)
        {
            int code = WorldTest.Run(playPath);
            GetTree().Quit(code);
            return;
        }

        if (_preGameProbe)
        {
            string[] probeMaps = Assets.ListGmaps();
            if (probeMaps.Length > 0) StartGame(probeMaps[0]);
            else ShowMapList();
            return;
        }

        if (!string.IsNullOrEmpty(playPath))
        {
            StartGame(playPath);
        }
        else
        {
            ShowMapList();
        }
    }

    public void ShowMapList()
    {
        Clear();
        var screen = new OpenWC3.Game.UI.MapSelectionScreen { Name = "MapSelection" };
        screen.StartRequested += (mapPath, racePreference, preset) =>
        {
            _selectedRace = racePreference == 8 ? 3 : racePreference;
            _aiDifficulty = MeleeAiPresetRules.Difficulty(preset);
            Clear();
            StartGame(mapPath);
        };
        AddChild(screen);
        _current = screen;
    }

    public void StartGame(string gmapPath)
    {
        string full = gmapPath;
        if (!System.IO.Path.IsPathRooted(full))
        {
            full = Assets.GmapDir + "/" + full;
        }

        MapSource map = MapSource.Open(full);
        if (map == null)
        {
            GD.PushError($"[GAME] 打不开 {full}");
            ShowMapList();
            return;
        }

        Clear();

        var game = new War3GameScene { Name = "Game" };
        game.TimeScaleHint = _fast ? Mathf.Min(0.15f, _timeScale) : _timeScale;
        game.StartGold = _startGold;
        game.StartLumber = _startLumber;
        game.Aggression = _aggro;
        game.AiEnabled = !_noAi;
        game.ActivePlayers = _players;
        if (_viewPlayer >= 0) game.ViewPlayer = _viewPlayer;
        game.FogEnabled = !_fogOff;
        game.MusicEnabled = !_musicOff;
        game.ShowDamageNumbers = _damageNumbers;
        game.AiDifficulty = _aiDifficulty;
                game.SelectedRace = _selectedRace;
        if (_daySeconds > 0f) game.DaySeconds = _daySeconds;
        foreach (int slot in _slots) game.ActiveSlots.Add(slot);
        foreach (List<int> team in _teams) game.Teams.Add(team);
        // 先进树再加载: 加载屏要在场景初始化期间就能画出来
        AddChild(game);
        _current = game;
        game.Loaded += AfterLoad;
        game.Setup(map, ShowMapList);

        void AfterLoad()
        {
            if (_cameraPreset >= 0) game.SetCameraPreset(_cameraPreset);
            if (_focusMapPoint.HasValue) game.FocusMapPoint(_focusMapPoint.Value.X, _focusMapPoint.Value.Y);
            if (!string.IsNullOrEmpty(_cinematicModel)) game.SetCinematicModel(_cinematicModel);
            if (_orderTest) game.ScriptedOrderTest();
            if (_clickTest) game.RunClickTest();
            if (_gatherTest) game.ScriptedGatherTest();
            if (_trainTest) game.ScriptedTrainTest();
            if (_moveTo.HasValue) game.ScriptedMoveTo(_moveTo.Value.X, _moveTo.Value.Y);
            if (_lumberTest) game.ScriptedLumberTest();
            if (_buildTest) game.ScriptedBuildTest();
            if (_housekeeping) game.RunHousekeeping();
            if (_creepProbe) game.RunCreepProbe();
            if (_heroProbe) game.RunHeroProbe();
            if (_reviveProbe) game.RunReviveProbe();
            if (_skillProbe) game.RunSkillProbe();
            if (_morphProbe) game.RunMorphProbe();
            if (_cardProbe) game.RunCardProbe();
            if (_hireProbe) game.RunHireProbe();
            if (_boltProbe) game.RunBoltProbe();
            if (_rallyProbe) game.RunRallyProbe();
            if (_subgroupProbe) game.RunSubgroupProbe();
            if (_spawnItems.Count > 0) game.SpawnDebugItems(_spawnItems);
            if (_pathProbe)
            {
                game.PathProbe();
                GetTree().Quit();
                return;
            }
            _elapsed = 0f;
            _shotTaken = false;
        }
    }

    private static void RunEffectProbe()
    {
        var fields = new (string Field, string Raw)[]
        {
            ("CasterArt", "acat"), ("TargetArt", "atat"), ("SpecialArt", "asat"),
            ("EffectArt", "aeat"), ("AreaEffectArt", "aaea"),
        };
        int ids = 0;
        int arts = 0;
        int resolved = 0;
        int missing = 0;
        foreach (string code in AbilityArt.Ids)
        {
            ids++;
            foreach ((string field, string raw) in fields)
            {
                foreach (string art in AbilityArt.Arts(null, code, 1, field, raw))
                {
                    arts++;
                    string path = PathResolve.ModelScene(art);
                    if (path != null)
                    {
                        resolved++;
                        GD.Print($"[FX] {code,-6} {field,-13} → {path.Replace("res://extract/", "")}");
                        continue;
                    }
                    missing++;
                    GD.Print($"[FX] {code,-6} {field,-13} 缺 {art}");
                }
            }
        }
        GD.Print($"[FX] 物编 {ids} 条，美术引用 {arts}，可加载 {resolved}，缺 {missing}");
    }

    private static void DumpUnits(string gmapPath)
    {
        string full = string.IsNullOrEmpty(gmapPath) ? null : (System.IO.Path.IsPathRooted(gmapPath) ? gmapPath : Assets.GmapDir + "/" + gmapPath);
        MapSource map = MapSource.Open(full);
        if (map == null)
        {
            GD.Print("[DUMP] 打不开地图");
            return;
        }

        byte[] rawUnits = Assets.ReadBytes("res://gmap/map/war3mapUnits.doo");
        if (rawUnits != null)
        {
            System.IO.File.WriteAllBytes("/tmp/war3mapUnits.doo", rawUnits);
            GD.Print($"[DUMP] 原始 doo {rawUnits.Length} 字节已存 /tmp/war3mapUnits.doo");
        }

        System.IO.File.WriteAllBytes("/tmp/war3map.w3e", Assets.ReadBytes("res://gmap/map/war3map.w3e") ?? Array.Empty<byte>());
        System.IO.File.WriteAllBytes("/tmp/war3map.w3i", Assets.ReadBytes("res://gmap/map/war3map.w3i") ?? Array.Empty<byte>());
        System.IO.File.WriteAllBytes("/tmp/war3map.wts", Assets.ReadBytes("res://gmap/map/war3map.wts") ?? Array.Empty<byte>());
        foreach (string name in new[] { "FootmanWhat", "PeasantYes", "TownHallWhat", "GruntYes" })
        {
            AudioStream stream = War3SoundTables.Pick(name, out float volume);
            string detail = stream is AudioStreamWav wav ? $"{wav.Data.Length} 字节 {wav.MixRate}Hz" : "缺失";
            GD.Print($"[DUMP] 音效 {name} 音量{volume:0.00} -> {detail}");
        }

        foreach (string candidate in new[] { "war3mapMap.tga", "war3mapMap.blp", "war3mapMap.b00" })
        {
            byte[] data = Assets.ReadBytes("res://gmap/map/" + candidate);
            GD.Print($"[DUMP] 小地图 {candidate} {(data == null ? "缺失" : data.Length + " 字节")}");
        }
        byte[] rawInfo = Assets.ReadBytes("res://gmap/map/war3map.w3i");
        if (rawInfo != null)
        {
            System.IO.File.WriteAllBytes("/tmp/war3map.w3i", rawInfo);
            System.IO.File.WriteAllBytes("/tmp/war3map.w3r", Assets.ReadBytes("res://gmap/map/war3map.w3r") ?? Array.Empty<byte>());
        }

        float offsetX = map.Terrain.OffsetX;
        float offsetY = map.Terrain.OffsetY;
        float startX = 0f;
        float startZ = 0f;
        foreach (W3iPlayer player in map.Players)
        {
            GD.Print($"[DUMP] 玩家 P{player.Id} 种族{player.Race} 控制{player.Controller} 起点({player.StartX:0},{player.StartY:0})"
                + $" 名字={player.Name} ally={player.AllyLow}/{player.AllyHigh} enemy={player.EnemyLow}/{player.EnemyHigh}");
            if (player.Id == 0)
            {
                startX = -(player.StartX - offsetX);
                startZ = player.StartY - offsetY;
            }
        }

        foreach (UnitPlacement entry in map.Units.Units)
        {
            float worldX = -(entry.X - offsetX);
            float worldZ = entry.Y - offsetY;
            float distance = Mathf.Sqrt((worldX - startX) * (worldX - startX) + (worldZ - startZ) * (worldZ - startZ));
            GD.Print($"[DUMP] {entry.Id,-6} player={entry.Player,-3} 地图({entry.X:0},{entry.Y:0}) 世界({worldX:0},{worldZ:0}) 距P0={distance:0} gold={entry.Gold} hp={entry.Hitpoints}");
        }
    }

    private void Clear()
    {
        _menuHost?.Clear();
    }

    public override void _Process(double delta)
    {
        if (string.IsNullOrEmpty(_shotPath) || _shotTaken || _shotDelay < 0f) return;

        _elapsed += (float)delta;
        if (_elapsed < _shotDelay) return;

        _shotTaken = true;
        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(_shotPath);
        GD.Print($"[SHOT] {_shotPath} -> {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}